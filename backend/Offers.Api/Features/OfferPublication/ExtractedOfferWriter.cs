using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Offers.Api.Features.OfferCapture;

namespace Offers.Api.Features.OfferPublication;

public sealed class ExtractedOfferWriter(OffersDbContext db)
{
    public static string Key(CaptureSource source, ExtractedOffer item) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(source.Id + "|" + (string.IsNullOrWhiteSpace(item.SourceOfferId)
            ? "title:" + Regex.Replace(item.OriginalTitle, @"\s+", " ").Trim().ToUpperInvariant()
            : "id:" + item.SourceOfferId)))).ToLowerInvariant();

    public async Task<int> SaveAsync(CaptureSource source, SourceDocument document, ExtractionResult result, CancellationToken token)
    {
        if (result.Uncertain || result.Offers is null) throw new InvalidOperationException("La extracción es incierta.");
        var candidates = result.Offers.Select(item => Map(source, document, item)).ToList();
        if (candidates.Select(x => x.ExtractionKey).Distinct().Count() != candidates.Count)
            throw new InvalidOperationException("La fuente contiene identidades ambiguas; no se actualizarán registros.");
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        foreach (var candidate in candidates)
        {
            var existing = await db.Offers.Include(x => x.Benefits).SingleOrDefaultAsync(x => x.ExtractionKey == candidate.ExtractionKey, token);
            if (existing is null) { db.Offers.Add(candidate); continue; }
            var changed = Snapshot(existing) != Snapshot(candidate);
            existing.LastExtractedAt = DateTimeOffset.UtcNow;
            if (!changed) continue;
            if (existing.Status == OfferStatus.Published) existing.Status = OfferStatus.Pending;
            existing.OriginalTitle = candidate.OriginalTitle;
            existing.SourceUrl = candidate.SourceUrl;
            existing.SourceOfferId = candidate.SourceOfferId;
            existing.OriginalDescription = candidate.OriginalDescription;
            existing.Summary = candidate.Summary;
            existing.OriginalConditions = candidate.OriginalConditions;
            existing.Conditions = candidate.Conditions;
            existing.Code = candidate.Code;
            existing.CodeStatus = candidate.CodeStatus;
            existing.StartsOn = candidate.StartsOn;
            existing.EndsOn = candidate.EndsOn;
            db.OfferBenefits.RemoveRange(existing.Benefits);
            existing.Benefits = candidate.Benefits;
            foreach (var benefit in existing.Benefits) benefit.OfferId = existing.Id;
            db.OfferBenefits.AddRange(existing.Benefits);
        }
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return candidates.Count;
    }

    private static Offer Map(CaptureSource source, SourceDocument document, ExtractedOffer item)
    {
        if (string.IsNullOrWhiteSpace(item.OriginalTitle) || item.OriginalTitle.Length > 512 || string.IsNullOrWhiteSpace(item.OriginalDescription)
            || item.Benefits is null || item.SourceOfferId?.Length > 512 || item.Code?.Length > 256 || item.SourceUrl?.Length > 2048
            || !document.Links.Contains(item.SourceUrl) || !Uri.TryCreate(item.SourceUrl, UriKind.Absolute, out var uri) || !SourceReader.IsOfficial(uri, source.Chain))
            throw new InvalidOperationException("La extracción contiene datos o enlaces inválidos.");
        var offer = new Offer {
            Chain = source.Chain, SourceUrl = item.SourceUrl, SourceOfferId = item.SourceOfferId,
            CaptureSourceId = source.Id, ExtractionKey = Key(source, item), OriginalTitle = item.OriginalTitle,
            OriginalDescription = item.OriginalDescription, Summary = item.Summary,
            OriginalConditions = item.OriginalConditions, Conditions = item.Conditions,
            Code = item.CodeStatus == OfferCodeStatus.Available ? item.Code : null, CodeStatus = item.CodeStatus,
            StartsOn = item.StartsOn, EndsOn = item.EndsOn, LastExtractedAt = DateTimeOffset.UtcNow,
            Benefits = item.Benefits.Select(x => new OfferBenefit { Type = x.Type, OriginalDescription = x.OriginalDescription,
                Description = x.Description, Requirements = x.Requirements, Percentage = x.Percentage,
                DiscountAmount = x.DiscountAmount, PromotionalPrice = x.PromotionalPrice,
                Currency = x.Currency?.ToUpperInvariant(), PurchaseQuantity = x.PurchaseQuantity, ReceivedQuantity = x.ReceivedQuantity }).ToList()
        };
        if (AdminOfferEndpoints.PublicationErrors(offer).Count != 0 || offer.Benefits.Any(x => string.IsNullOrWhiteSpace(x.OriginalDescription) || x.Currency?.Length > 3))
            throw new InvalidOperationException("La extracción contiene valores inválidos.");
        return offer;
    }

    private static string Snapshot(Offer x) => JsonSerializer.Serialize(new {
        x.SourceUrl, x.SourceOfferId, x.OriginalTitle, x.OriginalDescription, x.Summary, x.OriginalConditions, x.Conditions,
        x.Code, x.CodeStatus, x.StartsOn, x.EndsOn,
        Benefits = x.Benefits.Select(b => new { b.Type, b.OriginalDescription, b.Description, b.Requirements,
            b.Percentage, b.DiscountAmount, b.PromotionalPrice, b.Currency, b.PurchaseQuantity, b.ReceivedQuantity })
            .OrderBy(b => b.OriginalDescription).ThenBy(b => b.Type).ThenBy(b => b.PromotionalPrice).ToArray()
    });
}
