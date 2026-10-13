using System.ComponentModel.DataAnnotations;

namespace Offers.Api.Features.OfferPublication;

public sealed class Offer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required RestaurantChain Chain { get; set; }
    [MaxLength(2048)]
    public required string SourceUrl { get; set; }
    [MaxLength(512)]
    public string? SourceOfferId { get; set; }
    public string? OriginalDescription { get; set; }
    public string? Summary { get; set; }
    public string? OriginalConditions { get; set; }
    public string? Conditions { get; set; }
    [MaxLength(256)]
    public string? Code { get; set; }
    public OfferCodeStatus CodeStatus { get; set; } = OfferCodeStatus.Unknown;
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public OfferStatus Status { get; set; } = OfferStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastExtractedAt { get; set; }
    public List<OfferBenefit> Benefits { get; set; } = [];
}
