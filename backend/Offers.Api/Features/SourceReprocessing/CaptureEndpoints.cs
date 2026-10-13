using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Offers.Api.Features.OfferCapture;
using Offers.Api.Features.OfferPublication;

namespace Offers.Api.Features.SourceReprocessing;

public static class CaptureEndpoints
{
    public static void MapCapture(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/capture").RequireAuthorization("Administrator");
        group.AddEndpointFilter(async (context, next) => {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (DbException) { return Results.Problem(statusCode: 503, title: "No se pudo consultar el registro de lecturas."); }
        });
        group.MapGet("/status", (CaptureSettings settings, HttpContext http) => Results.Ok(new {
            settings.Enabled, configured = settings.IsReady && http.RequestServices.GetService<CaptureDbContext>() is not null,
            sources = settings.Sources.Select(x => new { x.Id, Chain = x.Chain.ToString(), x.Url }),
            message = settings.IsReady ? "Lectura configurada." : "Lectura deshabilitada o pendiente de configurar la clave y el modelo de IA."
        }));
        group.MapGet("/runs", async (HttpContext http, CancellationToken token) => {
            var db = http.RequestServices.GetService<CaptureDbContext>();
            if (db is null) return Results.Problem(statusCode: 503, title: "La base no está configurada.");
            var runs = await db.Runs.AsNoTracking().OrderByDescending(x => x.StartedAt).Take(50).ToListAsync(token);
            return Results.Ok(runs);
        });
        group.MapPost("/run", async (CaptureCoordinator coordinator, HttpContext http, CancellationToken token) => {
            if (http.RequestServices.GetService<CaptureDbContext>() is null) return Results.Problem(statusCode: 503, title: "La base no está configurada.");
            try { return Results.Ok(await coordinator.RunAsync(null, token)); }
            catch (InvalidOperationException error) { return Results.Problem(statusCode: 409, title: error.Message); }
        }).AddEndpointFilter<AdminCsrfFilter>();
        app.MapPost("/api/admin/offers/{id:guid}/reread", async (Guid id, HttpContext http, CaptureCoordinator coordinator, CancellationToken token) => {
            var db = http.RequestServices.GetService<OffersDbContext>();
            if (db is null) return Results.Problem(statusCode: 503, title: "La base no está configurada.");
            var sourceId = await db.Offers.AsNoTracking().Where(x => x.Id == id).Select(x => x.CaptureSourceId).SingleOrDefaultAsync(token);
            if (sourceId is null) return Results.Problem(statusCode: 409, title: "La oferta no tiene una fuente de lectura registrada.");
            try { return Results.Ok(await coordinator.RunAsync(sourceId, token)); }
            catch (InvalidOperationException error) { return Results.Problem(statusCode: 409, title: error.Message); }
        }).RequireAuthorization("Administrator").AddEndpointFilter<AdminCsrfFilter>();
    }
}
