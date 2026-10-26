using System.Linq.Expressions;
using Offers.Api.Features.OfferPublication;

namespace Offers.Api.Features.OfferBrowsing;

public static class OfferAvailability
{
    public static Expression<Func<Offer, bool>> ForDate(DateOnly today) => offer =>
        offer.Status == OfferStatus.Published &&
        (offer.StartsOn == null || offer.StartsOn <= today) &&
        (offer.EndsOn == null || offer.EndsOn >= today);

    public static DateOnly TodayInArgentina(DateTimeOffset instant)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
    }
}
