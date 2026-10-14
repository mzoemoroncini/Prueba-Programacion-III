using Offers.Api.Features.OfferPublication;

namespace Offers.Api.Features.OfferCapture;

public sealed class CaptureSettings
{
    public OcrSettings Ocr { get; set; } = new();
    public bool Enabled { get; set; }
    public int IntervalHours { get; set; } = 24;
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public bool RenderPages { get; set; } = true;
    public string? ProxyServer { get; set; }
    public string? BrowserExecutablePath { get; set; }
    public List<CaptureSource> Sources { get; set; } = [];
    public bool IsReady => Enabled && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model) && Sources.Count > 0 && Ocr.IsReady;
}

public sealed class CaptureSource
{
    public string Id { get; set; } = "";
    public RestaurantChain Chain { get; set; }
    public string Url { get; set; } = "";
}

public sealed record SourceDocument(string Url, string Text, byte[]? Screenshot, IReadOnlyList<SourceAttachment> Attachments, IReadOnlyList<string> Links);
public sealed record SourceAttachment(string Url, string Filename, string MimeType, byte[] Data);
public sealed record ExtractionResult(bool Uncertain, IReadOnlyList<ExtractedOffer> Offers);
public sealed record ExtractedOffer(string OriginalTitle, string? SourceOfferId, string SourceUrl,
    string OriginalDescription, string? Summary, string? OriginalConditions, string? Conditions,
    string? Code, OfferCodeStatus CodeStatus, DateOnly? StartsOn, DateOnly? EndsOn, IReadOnlyList<ExtractedBenefit> Benefits);
public sealed record ExtractedBenefit(BenefitType Type, string OriginalDescription, string? Description,
    string? Requirements, decimal? Percentage, decimal? DiscountAmount, decimal? PromotionalPrice,
    string? Currency, int? PurchaseQuantity, int? ReceivedQuantity);
