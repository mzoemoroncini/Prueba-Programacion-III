using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Offers.Api.Features.OfferBrowsing;
using Offers.Api.Features.OfferPublication;

var today = new DateOnly(2026, 10, 13);
var available = OfferAvailability.ForDate(today).Compile();
Offer Make(OfferStatus status, DateOnly? start = null, DateOnly? end = null) => new()
{
    Chain = RestaurantChain.McDonalds, SourceUrl = "https://example.com/test-only",
    Summary = "Oferta de prueba automatizada", Status = status, StartsOn = start, EndsOn = end
};
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    Console.WriteLine("OK: " + message);
}
Check(available(Make(OfferStatus.Published)), "Oferta publicada sin fechas conocidas");
Check(!available(Make(OfferStatus.Pending)), "Oferta pendiente oculta");
Check(!available(Make(OfferStatus.Disabled)), "Oferta desactivada oculta");
Check(!available(Make(OfferStatus.Published, today.AddDays(1))), "Inicio futuro oculto");
Check(!available(Make(OfferStatus.Published, end: today.AddDays(-1))), "Vencimiento pasado oculto");
Check(available(Make(OfferStatus.Published, today, today)), "Inicio y finalización inclusivos");
Check(OfferAvailability.TodayInArgentina(new DateTimeOffset(2026, 10, 14, 1, 0, 0, TimeSpan.Zero)) == today,
    "Vigencia evaluada en fecha argentina, no UTC");

var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Offers");
var apiUrl = Environment.GetEnvironmentVariable("OFFERS_CHECKS_API_URL");
if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(apiUrl))
{
    Console.WriteLine("Prueba MySQL/HTTP omitida: requiere ConnectionStrings__Offers y OFFERS_CHECKS_API_URL.");
    return;
}
var options = new DbContextOptionsBuilder<OffersDbContext>().UseMySQL(connection).Options;
await using var db = new OffersDbContext(options);
using var client = new HttpClient { BaseAddress = new Uri(apiUrl) };
var actualToday = OfferAvailability.TodayInArgentina(DateTimeOffset.UtcNow);
var visible = Make(OfferStatus.Published, actualToday, actualToday);
visible.CodeStatus = OfferCodeStatus.Available;
visible.Code = "Test-aB-01";
visible.Benefits.Add(new OfferBenefit { Type = BenefitType.PercentageDiscount, Percentage = 12.3456m, OriginalDescription = "Texto de prueba" });
visible.Benefits.Add(new OfferBenefit { Type = BenefitType.PromotionalPrice, PromotionalPrice = 1234.56m, Currency = "ARS" });
var noCode = Make(OfferStatus.Published);
noCode.CodeStatus = OfferCodeStatus.NotRequired;
noCode.Code = "No debe exponerse";
var uncertain = Make(OfferStatus.Published);
uncertain.CodeStatus = OfferCodeStatus.Unknown;
uncertain.Code = "No debe exponerse";
var hidden = new[] { Make(OfferStatus.Pending), Make(OfferStatus.Disabled),
    Make(OfferStatus.Published, actualToday.AddDays(1)), Make(OfferStatus.Published, end: actualToday.AddDays(-1)) };
var fixtures = new[] { visible, noCode, uncertain }.Concat(hidden).ToArray();
try
{
    db.Offers.AddRange(fixtures);
    await db.SaveChangesAsync();
    var list = await client.GetFromJsonAsync<List<OfferListDto>>("/api/offers") ?? [];
    Check(list.Any(x => x.Id == visible.Id) && list.Any(x => x.Id == noCode.Id), "Listado incluye ofertas publicadas vigentes");
    Check(!list.Any(x => hidden.Any(h => h.Id == x.Id)), "SQL excluye pendientes, desactivadas, futuras y vencidas");
    foreach (var offer in hidden)
    {
        var response = await client.GetAsync("/api/offers/" + offer.Id);
        Check(response.StatusCode == HttpStatusCode.NotFound, "Detalle oculta oferta no disponible");
    }
    var detail = await client.GetFromJsonAsync<OfferDetailDto>("/api/offers/" + visible.Id)
        ?? throw new InvalidOperationException("Detalle vacío");
    Check(detail.Code == "Test-aB-01", "Código conserva sus caracteres");
    Check(detail.Benefits.Count == 2 && detail.Benefits.Any(x => x.Percentage == 12.3456m)
        && detail.Benefits.Any(x => x.PromotionalPrice == 1234.56m), "Beneficios y decimales sobreviven MySQL y HTTP");
    Check(detail.StartsOn == actualToday && detail.EndsOn == actualToday, "Fechas sobreviven MySQL y HTTP");
    foreach (var offer in new[] { noCode, uncertain })
    {
        var result = await client.GetFromJsonAsync<OfferDetailDto>("/api/offers/" + offer.Id);
        Check(result is not null && result.Code == null && result.CodeStatus == offer.CodeStatus.ToString(), "Código ausente o incierto no expone texto residual");
    }
}
finally
{
    db.Offers.RemoveRange(fixtures);
    await db.SaveChangesAsync();
}
