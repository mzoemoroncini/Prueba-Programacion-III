using System.ComponentModel.DataAnnotations;

namespace Offers.Api.Features.OfferPublication;

public sealed class OfferBenefit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfferId { get; set; }
    public BenefitType Type { get; set; } = BenefitType.Unknown;
    public string? OriginalDescription { get; set; }
    public string? Description { get; set; }
    public string? Requirements { get; set; }
    public decimal? Percentage { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? PromotionalPrice { get; set; }
    [MaxLength(3)]
    public string? Currency { get; set; }
    public int? PurchaseQuantity { get; set; }
    public int? ReceivedQuantity { get; set; }
}
