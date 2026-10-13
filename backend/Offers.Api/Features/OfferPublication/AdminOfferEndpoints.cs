using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Offers.Api.Features.OfferBrowsing;

namespace Offers.Api.Features.OfferPublication;

public static class AdminOfferEndpoints
{
    public static void MapAdminOffers(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/offers").RequireAuthorization("Administrator");
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (DbException)
            {
                app.Logger.LogWarning("No se pudo acceder a MySQL para la gestión de publicaciones.");
                return Unavailable();
            }
        });
        group.MapGet("", async (HttpContext http, CancellationToken token) =>
        {
            var db = http.RequestServices.GetService<OffersDbContext>();
            if (db is null) return Unavailable();
            var items = await db.Offers.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
                .Select(x => new { x.Id, x.Chain, Summary = x.Summary ?? x.OriginalDescription, x.Status, x.EndsOn }).ToListAsync(token);
            return Results.Ok(items.Select(x => new { x.Id, Chain = x.Chain.ToString(), x.Summary, Status = x.Status.ToString(), x.EndsOn }));
        });
        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, CancellationToken token) =>
        {
            var db = http.RequestServices.GetService<OffersDbContext>();
            if (db is null) return Unavailable();
            var offer = await db.Offers.AsNoTracking().Include(x => x.Benefits).SingleOrDefaultAsync(x => x.Id == id, token);
            if (offer is null) return Results.NotFound();
            return Results.Ok(new { Status = offer.Status.ToString(), offer.CaptureSourceId, offer.LastExtractedAt, Detail = OfferBrowsingEndpoints.ToDetailDto(offer) });
        });
        group.MapPost("/{id:guid}/publish", async (Guid id, HttpContext http, CancellationToken token) =>
        {
            var db = http.RequestServices.GetService<OffersDbContext>();
            if (db is null) return Unavailable();
            var offer = await db.Offers.Include(x => x.Benefits).SingleOrDefaultAsync(x => x.Id == id, token);
            if (offer is null) return Results.NotFound();
            var errors = PublicationErrors(offer);
            if (errors.Count != 0) return Results.ValidationProblem(errors.ToDictionary(x => x.Key, x => new[] { x.Value }));
            offer.Status = OfferStatus.Published;
            await db.SaveChangesAsync(token);
            return Results.NoContent();
        }).AddEndpointFilter<AdminCsrfFilter>();
        group.MapPost("/{id:guid}/disable", async (Guid id, HttpContext http, CancellationToken token) =>
        {
            var db = http.RequestServices.GetService<OffersDbContext>();
            if (db is null) return Unavailable();
            var offer = await db.Offers.SingleOrDefaultAsync(x => x.Id == id, token);
            if (offer is null) return Results.NotFound();
            offer.Status = OfferStatus.Disabled;
            await db.SaveChangesAsync(token);
            return Results.NoContent();
        }).AddEndpointFilter<AdminCsrfFilter>();
    }

    public static Dictionary<string, string> PublicationErrors(Offer offer)
    {
        var errors = new Dictionary<string, string>();
        if (!Enum.IsDefined(offer.Chain)) errors["chain"] = "La cadena no es válida.";
        if (!Uri.TryCreate(offer.SourceUrl, UriKind.Absolute, out var url) || (url.Scheme != "http" && url.Scheme != "https") || !string.IsNullOrEmpty(url.UserInfo))
            errors["sourceUrl"] = "La oferta necesita un enlace HTTP o HTTPS válido.";
        if (offer.StartsOn.HasValue && offer.EndsOn.HasValue && offer.EndsOn < offer.StartsOn)
            errors["dates"] = "La finalización no puede ser anterior al inicio.";
        if (!Enum.IsDefined(offer.CodeStatus) || (offer.CodeStatus == OfferCodeStatus.Available && string.IsNullOrWhiteSpace(offer.Code)))
            errors["code"] = "El estado del código no es válido o falta el código informado.";
        if (offer.Benefits.Any(x => !Enum.IsDefined(x.Type) || x.Percentage < 0 || x.Percentage > 100 || x.DiscountAmount < 0 || x.PromotionalPrice < 0 || x.PurchaseQuantity <= 0 || x.ReceivedQuantity <= 0))
            errors["benefits"] = "Los valores de los beneficios no son válidos.";
        return errors;
    }
    private static IResult Unavailable() => Results.Problem(statusCode: 503, title: "No se pueden consultar las publicaciones en este momento.");
}
