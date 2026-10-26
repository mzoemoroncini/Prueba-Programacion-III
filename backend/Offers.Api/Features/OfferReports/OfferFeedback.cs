using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Offers.Api.Features.OfferBrowsing;
using Offers.Api.Features.OfferPublication;

namespace Offers.Api.Features.OfferReports;

public sealed class OfferReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfferId { get; set; }
    public string Reason { get; set; } = "";
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class OfferInteraction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfferId { get; set; }
    public string Action { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed record ReportInput(string? Reason, string? Comment);
public static class OfferFeedback
{
    public static void MapOfferFeedback(this WebApplication app)
    {
        app.MapPost("/api/offers/{id:guid}/reports", async (Guid id, ReportInput input, HttpContext http, CancellationToken ct) =>
        {
            if (input.Reason is not ("Expired" or "IncorrectData") || input.Comment?.Length > 2000)
                return Results.BadRequest(new { title = "Seleccioná un motivo válido y un comentario de hasta 2000 caracteres." });
            var db = http.RequestServices.GetService<OffersDbContext>();
            if (db is null) return Unavailable();
            try
            {
                if (!await db.Offers.Where(OfferAvailability.ForDate(OfferAvailability.TodayInArgentina(DateTimeOffset.UtcNow))).AnyAsync(x => x.Id == id, ct)) return Results.NotFound();
                var report = new OfferReport { OfferId = id, Reason = input.Reason, Comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim() };
                db.Reports.Add(report); await db.SaveChangesAsync(ct);
                return Results.Created($"/api/admin/reports?offerId={id}", new { report.Id });
            }
            catch (DbException) { return Unavailable(); }
        }).RequireRateLimiting("feedback");
        app.MapPost("/api/offers/{id:guid}/source-access", async (Guid id, HttpContext http, CancellationToken ct) =>
        {
            var db = http.RequestServices.GetService<OffersDbContext>();
            if (db is null) return Unavailable();
            try
            {
                if (!await db.Offers.Where(OfferAvailability.ForDate(OfferAvailability.TodayInArgentina(DateTimeOffset.UtcNow))).AnyAsync(x => x.Id == id, ct)) return Results.NotFound();
                db.Interactions.Add(new OfferInteraction { OfferId = id, Action = "SourceAccess" }); await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
            catch (DbException) { return Unavailable(); }
        }).RequireRateLimiting("feedback");
        app.MapGet("/api/admin/reports", async (Guid? offerId, HttpContext http, CancellationToken ct) =>
        {
            var db = http.RequestServices.GetService<OffersDbContext>();
            if (db is null) return Unavailable();
            try
            {
                var query = db.Reports.AsNoTracking().AsQueryable();
                if (offerId.HasValue) query = query.Where(x => x.OfferId == offerId.Value);
                return Results.Ok(await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(200)
                    .Join(db.Offers, report => report.OfferId, offer => offer.Id,
                        (report, offer) => new { report.Id, report.OfferId, offer.Summary, report.Reason, report.Comment, report.CreatedAt }).ToListAsync(ct));
            }
            catch (DbException) { return Unavailable(); }
        }).RequireAuthorization("Administrator");
    }
    private static IResult Unavailable() => Results.Problem(statusCode:503, title:"No se pudo completar la operación. Intentá nuevamente.");
}
