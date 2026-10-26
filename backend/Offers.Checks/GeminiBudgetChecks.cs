using System.Net;
using Microsoft.EntityFrameworkCore;
using Offers.Api.Configuration;
using Offers.Api.Features.OfferCapture;

internal sealed class ControlledClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Current { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Current;
}
internal static class GeminiBudgetChecks
{
    public static async Task RunAsync(ConfigurationDbContext db, Action<bool,string> check)
    {
        var original = await db.GeminiBudgets.AsNoTracking().SingleAsync(x => x.Id == 1);
        var clock = new ControlledClock(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
        var settings = new CaptureSettings { Model = "test-model", ApiKey = "test-only", MaxRequestsPerDay = 2, MinRequestIntervalSeconds = 60 };
        try
        {
            await db.GeminiBudgets.ExecuteUpdateAsync(x => x.SetProperty(p => p.QuotaDay, new DateTime(2000,1,1)).SetProperty(p => p.RequestCount, 0).SetProperty(p => p.NextRequestAtUtc, (DateTime?)null).SetProperty(p => p.BlockedUntilUtc, (DateTime?)null));
            var budget = new GeminiRequestBudget(db, clock);
            await budget.ReserveAsync(settings, default);
            var first = await db.GeminiBudgets.AsNoTracking().SingleAsync();
            check(first.RequestCount == 1 && first.NextRequestAtUtc == clock.Current.UtcDateTime.AddSeconds(60), "Gemini reserva intento y próximo minuto en MySQL antes del HTTP");
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            try { await budget.ReserveAsync(settings, canceled.Token); throw new Exception("Reserva cancelada aceptada"); }
            catch (OperationCanceledException) { check(true, "Cancelar una reserva no dispara una llamada adicional"); }
            clock.Current = clock.Current.AddSeconds(61); await budget.ReserveAsync(settings, default);
            try { await budget.EnsureAvailableAsync(settings, default); throw new Exception("Presupuesto diario excedido"); }
            catch (GeminiProviderException error) { check(error.Code == "GeminiDailyBudget", "Gemini detiene nuevas llamadas al llegar al presupuesto diario persistido"); }
            var restarted = new GeminiRequestBudget(db, clock);
            try { await restarted.EnsureAvailableAsync(settings, default); throw new Exception("Reiniciar servicio reinició la cuota"); }
            catch (GeminiProviderException) { check(true, "Reiniciar el servicio no reinicia el contador diario"); }
            clock.Current = clock.Current.AddDays(1); await restarted.ReserveAsync(settings, default);
            check((await db.GeminiBudgets.AsNoTracking().SingleAsync()).RequestCount == 1, "Nuevo día de cuota restablece el contador sin perder historial de ofertas");
            clock.Current = clock.Current.AddSeconds(61);
            var calls = 0;
            using var http = new HttpClient(new StubHttpHandler(_ => { calls++; return Task.FromResult("private-provider-error"); }, HttpStatusCode.TooManyRequests, TimeSpan.FromMinutes(4)));
            var document = new SourceDocument("https://www.mostazaweb.com.ar/", "Texto controlado", null, [], []);
            try { await new GeminiOfferExtractor(http, restarted).ExtractAsync(document, settings, default); throw new Exception("429 aceptado"); }
            catch (GeminiProviderException error)
            {
                check(error.Code == "GeminiQuota" && calls == 1 && !error.Message.Contains("private-provider-error"), "HTTP 429 genera un solo intento sin reintentos ni exposición del cuerpo privado");
            }
            var blocked = await db.GeminiBudgets.AsNoTracking().SingleAsync();
            check(blocked.BlockedUntilUtc >= clock.Current.UtcDateTime.AddMinutes(4), "Retry-After de Gemini se conserva en MySQL");
            try { await new GeminiRequestBudget(db, clock).EnsureAvailableAsync(settings, default); throw new Exception("Cooldown ignorado"); }
            catch (GeminiProviderException error) { check(error.Code == "GeminiQuota", "Cooldown persistido impide leer más fuentes hasta la fecha permitida"); }
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.GeminiBudgets.ExecuteUpdateAsync(x => x.SetProperty(p => p.QuotaDay, original.QuotaDay).SetProperty(p => p.RequestCount, original.RequestCount).SetProperty(p => p.NextRequestAtUtc, original.NextRequestAtUtc).SetProperty(p => p.BlockedUntilUtc, original.BlockedUntilUtc));
        }
    }
}
