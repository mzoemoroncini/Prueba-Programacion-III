using Microsoft.EntityFrameworkCore;
using Offers.Api.Features.OfferPublication;

namespace Offers.Api.Features.OfferCapture;

public sealed class CaptureCoordinator(IServiceScopeFactory scopes, CaptureSettings settings, ILogger<CaptureCoordinator> logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task<IReadOnlyList<CaptureRun>> RunAsync(string? sourceId, CancellationToken token)
    {
        if (!settings.IsReady) throw new InvalidOperationException("La lectura con IA requiere habilitación, clave y modelo configurados.");
        var sources = settings.Sources.Where(x => sourceId is null || x.Id == sourceId).ToList();
        if (sources.Count == 0) throw new InvalidOperationException("La fuente solicitada no está configurada.");
        if (!await gate.WaitAsync(0, token)) throw new InvalidOperationException("Ya hay una lectura en curso.");
        try
        {
            var runs = new List<CaptureRun>();
            foreach (var source in sources)
            {
                token.ThrowIfCancellationRequested();
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CaptureDbContext>();
                var run = new CaptureRun { SourceId = source.Id, SourceUrl = source.Url };
                db.Runs.Add(run); await db.SaveChangesAsync(token);
                try
                {
                    var document = await scope.ServiceProvider.GetRequiredService<SourceReader>().ReadAsync(source, settings.RenderPages, token);
                    run.Stage = "Extract";
                    var result = await scope.ServiceProvider.GetRequiredService<OpenAiOfferExtractor>().ExtractAsync(document, settings, token);
                    run.Stage = "ValidateAndSave";
                    run.OfferCount = await scope.ServiceProvider.GetRequiredService<ExtractedOfferWriter>().SaveAsync(source, document, result, token);
                    run.Status = "Succeeded";
                }
                catch (Exception error) when (error is not OperationCanceledException || !token.IsCancellationRequested)
                {
                    run.Status = "Failed";
                    run.Error = run.Stage switch { "Read" => "No se pudo leer la fuente, sus imágenes o documentos.",
                        "Extract" => "La respuesta de IA falló, fue rechazada o quedó incompleta.",
                        _ => "La extracción es incierta, inválida o no pudo guardarse. Los datos anteriores se conservan." };
                    logger.LogWarning("Lectura de fuente {SourceId} falló en {Stage} ({ErrorType}).", source.Id, run.Stage, error.GetType().Name);
                }
                finally
                {
                    if (run.Status == "Running") { run.Status = "Failed"; run.Error = "La lectura fue interrumpida."; }
                    run.FinishedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(CancellationToken.None);
                }
                runs.Add(run);
            }
            return runs;
        }
        finally { gate.Release(); }
    }
}

public sealed class CaptureWorker(CaptureCoordinator coordinator, CaptureSettings settings, ILogger<CaptureWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.IsReady) { logger.LogInformation("Lectura automática deshabilitada: revisar la configuración de Capture."); return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await coordinator.RunAsync(null, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("No pudo ejecutarse la lectura automática ({ErrorType}).", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromHours(Math.Clamp(settings.IntervalHours, 1, 168)), stoppingToken);
        }
    }
}
