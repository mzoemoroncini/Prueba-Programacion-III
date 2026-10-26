using Microsoft.EntityFrameworkCore;
using Offers.Api.Configuration;

namespace Offers.Api.Features.OfferCapture;

public sealed class GeminiBudget
{
    public int Id { get; set; } = 1;
    public DateTime QuotaDay { get; set; } = new(2000, 1, 1);
    public int RequestCount { get; set; }
    public DateTime? NextRequestAtUtc { get; set; }
    public DateTime? BlockedUntilUtc { get; set; }
}
public sealed class GeminiProviderException(string code, string message, DateTime? retryAfterUtc = null) : InvalidOperationException(message)
{
    public string Code { get; } = code;
    public DateTime? RetryAfterUtc { get; } = retryAfterUtc;
}

// Presupuesto conservador de esta instalación. Las cuotas reales dependen del proyecto/modelo en AI Studio.
public sealed class GeminiRequestBudget(ConfigurationDbContext db, TimeProvider time)
{
    private static readonly TimeZoneInfo Pacific = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
    private static DateTime Day(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, Pacific).Date;
    private static void Validate(CaptureSettings settings)
    {
        if (settings.MaxRequestsPerDay is < 1 or > 10000 || settings.MinRequestIntervalSeconds is < 60 or > 86400)
            throw new GeminiProviderException("GeminiConfiguration", "El presupuesto requiere de 1 a 10000 solicitudes diarias y un intervalo de al menos 60 segundos. Contrastá los valores con el nivel gratuito de tu proyecto.");
    }
    private static void Check(GeminiBudget row, CaptureSettings settings, DateTime now)
    {
        Validate(settings);
        if (row.BlockedUntilUtc > now) throw new GeminiProviderException("GeminiQuota", "Gemini indicó un límite de cuota. Se suspenden las llamadas hasta la fecha registrada; las ofertas anteriores se conservan.", row.BlockedUntilUtc);
        if (row.QuotaDay == Day(now) && row.RequestCount >= settings.MaxRequestsPerDay)
        {
            var reset = TimeZoneInfo.ConvertTimeToUtc(Day(now).AddDays(1), Pacific);
            throw new GeminiProviderException("GeminiDailyBudget", "Se alcanzó el presupuesto diario local de Gemini. No se realizarán más llamadas hoy ni se cambiará a un proveedor pago.", reset);
        }
    }
    public async Task EnsureAvailableAsync(CaptureSettings settings, CancellationToken token)
    {
        var row = await db.GeminiBudgets.AsNoTracking().SingleAsync(x => x.Id == 1, token);
        Check(row, settings, time.GetUtcNow().UtcDateTime);
    }
    public async Task ReserveAsync(CaptureSettings settings, CancellationToken token)
    {
        while (true)
        {
            TimeSpan delay;
            await using (var transaction = await db.Database.BeginTransactionAsync(token))
            {
                db.ChangeTracker.Clear();
                // Bloqueo de fila también entre procesos que comparten la misma base.
                var row = (await db.GeminiBudgets.FromSqlRaw("SELECT * FROM `GeminiBudgets` WHERE `Id` = 1 FOR UPDATE").ToListAsync(token)).Single();
                var now = time.GetUtcNow().UtcDateTime;
                Check(row, settings, now);
                delay = row.NextRequestAtUtc.HasValue ? row.NextRequestAtUtc.Value - now : TimeSpan.Zero;
                if (delay <= TimeSpan.Zero)
                {
                    if (row.QuotaDay != Day(now)) { row.QuotaDay = Day(now); row.RequestCount = 0; }
                    row.RequestCount++; // Los intentos fallidos también consumen presupuesto; se reserva antes del HTTP.
                    row.NextRequestAtUtc = now.AddSeconds(settings.MinRequestIntervalSeconds);
                    await db.SaveChangesAsync(token); await transaction.CommitAsync(token); return;
                }
                await transaction.RollbackAsync(token);
            }
            await Task.Delay(delay, time, token); // Nunca mantener un bloqueo SQL mientras se espera.
        }
    }
    public async Task<DateTime> RecordQuotaAsync(TimeSpan? retryAfter, CancellationToken token)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var seconds = Math.Max(60, retryAfter?.TotalSeconds ?? 60);
        var until = now.AddSeconds(Math.Min(seconds, (DateTime.MaxValue - now).TotalSeconds - 1));
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        db.ChangeTracker.Clear();
        var row = (await db.GeminiBudgets.FromSqlRaw("SELECT * FROM `GeminiBudgets` WHERE `Id` = 1 FOR UPDATE").ToListAsync(token)).Single();
        if (row.BlockedUntilUtc > until) until = row.BlockedUntilUtc.Value;
        row.BlockedUntilUtc = until;
        await db.SaveChangesAsync(token); await transaction.CommitAsync(token); return until;
    }
}
