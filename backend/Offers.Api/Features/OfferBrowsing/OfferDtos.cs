namespace Offers.Api.Features.OfferBrowsing;

public sealed record OfferListDto(Guid Id, string Chain, string? Summary);

public sealed record OfferDetailDto(
    Guid Id, string Chain, string? Summary, string? OriginalDescription,
    string? Conditions, string? Code, string CodeStatus,
    DateOnly? StartsOn, DateOnly? EndsOn, string SourceUrl,
    IReadOnlyList<OfferBenefitDto> Benefits);

public sealed record OfferBenefitDto(
    string Type, string? Description, string? OriginalDescription, string? Requirements,
    decimal? Percentage, decimal? DiscountAmount, decimal? PromotionalPrice,
    string? Currency, int? PurchaseQuantity, int? ReceivedQuantity);
