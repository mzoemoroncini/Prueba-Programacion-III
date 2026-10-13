namespace Offers.Api.Features.OfferPublication;

public enum RestaurantChain
{
    McDonalds = 1,
    BurgerKing = 2,
    Mostaza = 3
}

public enum OfferStatus
{
    Pending = 0,
    Published = 1,
    Disabled = 2
}

public enum OfferCodeStatus
{
    Unknown = 0,
    NotRequired = 1,
    Available = 2
}

public enum BenefitType
{
    Unknown = 0,
    PercentageDiscount = 1,
    FixedDiscount = 2,
    PromotionalPrice = 3,
    MultiBuy = 4,
    Combo = 5,
    Other = 6
}
