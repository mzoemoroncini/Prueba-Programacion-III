using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Offers.Api.Features.OfferPublication;

namespace Offers.Api.Features.OfferBrowsing;

public static class OfferBrowsingEndpoints
{
    public static void MapOfferBrowsing(this WebApplication app)
    {
        app.MapGet("/api/offers", async (HttpContext http, CancellationToken cancellationToken) =>
        {
            var db = http.RequestServices.GetService<OffersDbContext>();
            if (db is null) return Unavailable();
            try
            {
                var today = OfferAvailability.TodayInArgentina(DateTimeOffset.UtcNow);
                var offers = await db.Offers.AsNoTracking()
                    .Where(OfferAvailability.ForDate(today))
                    .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
                    .Select(x => new { x.Id, x.Chain, Summary = x.Summary ?? x.OriginalDescription })
                    .ToListAsync(cancellationToken);
                return Results.Ok(offers.Select(x => new OfferListDto(x.Id, ChainName(x.Chain), x.Summary)));
            }
            catch (DbException)
            {
                app.Logger.LogWarning("No se pudo consultar MySQL para el listado de ofertas.");
                return Unavailable();
            }
        });

        app.MapGet("/api/offers/{id:guid}", async (Guid id, HttpContext http, CancellationToken cancellationToken) =>
        {
            var db = http.RequestServices.GetService<OffersDbContext>();
            if (db is null) return Unavailable();
            try
            {
                var today = OfferAvailability.TodayInArgentina(DateTimeOffset.UtcNow);
                var offer = await db.Offers.AsNoTracking()
                    .Where(OfferAvailability.ForDate(today))
                    .Include(x => x.Benefits)
                    .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (offer is null) return Results.NotFound();
                var benefits = offer.Benefits.OrderBy(x => x.Id).Select(x => new OfferBenefitDto(
                    x.Type.ToString(), x.Description, x.OriginalDescription, x.Requirements,
                    x.Percentage, x.DiscountAmount, x.PromotionalPrice,
                    x.Currency, x.PurchaseQuantity, x.ReceivedQuantity)).ToList();
                return Results.Ok(new OfferDetailDto(offer.Id, ChainName(offer.Chain),
                    offer.Summary ?? offer.OriginalDescription, offer.OriginalDescription,
                    offer.Conditions ?? offer.OriginalConditions,
                    offer.CodeStatus == OfferCodeStatus.Available ? offer.Code : null,
                    offer.CodeStatus.ToString(), offer.StartsOn, offer.EndsOn, offer.SourceUrl, benefits));
            }
            catch (DbException)
            {
                app.Logger.LogWarning("No se pudo consultar MySQL para el detalle de oferta.");
                return Unavailable();
            }
        });
    }

    private static IResult Unavailable() => Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "No se pueden consultar las ofertas en este momento.",
        detail: "Intentá nuevamente más tarde.");

    private static string ChainName(RestaurantChain chain) => chain switch
    {
        RestaurantChain.McDonalds => "McDonald’s",
        RestaurantChain.BurgerKing => "Burger King",
        RestaurantChain.Mostaza => "Mostaza",
        _ => "No informado por la fuente"
    };
}
