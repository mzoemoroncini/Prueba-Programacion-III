using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Offers.Api.Features.OfferCapture;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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

var source = new CaptureSource { Id = "test-source", Chain = RestaurantChain.Mostaza, Url = "https://www.mostazaweb.com.ar/cuponweb2/" };
var document = new SourceDocument(source.Url, "Oferta de prueba 10%", [1, 2, 3],
    [new SourceAttachment(source.Url + "terms.pdf", "terms.pdf", "application/pdf", [37, 80, 68, 70, 45])], [source.Url]);
var extracted = new ExtractedOffer("Oferta de prueba", "test-123", source.Url, "Oferta de prueba 10%", "Resumen de prueba", null, null,
    "Test-zZ-01", OfferCodeStatus.Available, null, null,
    [new ExtractedBenefit(BenefitType.PercentageDiscount, "10%", null, null, 10m, null, null, null, null, null)]);
var extraction = new ExtractionResult(false, [extracted]);
var serializer = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
using var fakeAi = new HttpClient(new StubHttpHandler(async request => {
    Check(request.RequestUri!.AbsoluteUri == "https://api.openai.com/v1/responses", "Adaptador utiliza Responses API");
    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
    var content = body.RootElement.GetProperty("input")[0].GetProperty("content");
    Check(content.EnumerateArray().Any(x => x.GetProperty("type").GetString() == "input_image"), "Adaptador envía captura para lectura visual");
    Check(content.EnumerateArray().Any(x => x.GetProperty("type").GetString() == "input_file"), "Adaptador envía PDF para lectura documental");
    Check(body.RootElement.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean(), "Extracción solicita esquema JSON estricto");
    return JsonSerializer.Serialize(new { status = "completed", output = new[] { new { type = "message", content = new[] {
        new { type = "output_text", text = JsonSerializer.Serialize(extraction, serializer) } } } } });
}));
var aiSettings = new CaptureSettings { ApiKey = "test-only-not-a-real-key", Model = "test-model" };
var parsed = await new OpenAiOfferExtractor(fakeAi).ExtractAsync(document, aiSettings, CancellationToken.None);
Check(parsed.Offers.Single().Code == "Test-zZ-01" && parsed.Offers.Single().Benefits.Single().Percentage == 10m,
    "Adaptador interpreta salida estructurada sin alterar código o porcentaje");
using var partialAi = new HttpClient(new StubHttpHandler(_ => Task.FromResult("{\"status\":\"incomplete\",\"output\":[]}")));
try { await new OpenAiOfferExtractor(partialAi).ExtractAsync(document, aiSettings, CancellationToken.None); throw new Exception("Respuesta incompleta aceptada"); }
catch (InvalidOperationException) { Console.WriteLine("OK: Respuesta incompleta no genera ofertas"); }
Check(ExtractedOfferWriter.Key(source, extracted) == ExtractedOfferWriter.Key(source, extracted with { Summary = "Resumen modificado" }),
    "Identificador de extracción estable ante cambio de resumen");

if (Environment.GetEnvironmentVariable("OFFERS_CHECKS_LIVE_SOURCES") == "true")
{
    using var sourceHttp = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(60) };
    sourceHttp.DefaultRequestHeaders.UserAgent.ParseAdd("OffersMVP/0.1");
    var reader = new SourceReader(sourceHttp, new CaptureSettings {
        BrowserExecutablePath = Environment.GetEnvironmentVariable("Capture__BrowserExecutablePath") });
    var sources = new[] {
        new CaptureSource { Id = "mcdonalds-ar", Chain = RestaurantChain.McDonalds, Url = "https://www.mcdonalds.com.ar/" },
        new CaptureSource { Id = "burgerking-ar", Chain = RestaurantChain.BurgerKing, Url = "https://burgerking.com.ar/es/rewards/offers" },
        new CaptureSource { Id = "mostaza-ar", Chain = RestaurantChain.Mostaza, Url = "https://www.mostazaweb.com.ar/cuponweb2/" }
    };
    foreach (var liveSource in sources)
    {
        var page = await reader.ReadAsync(liveSource, true, CancellationToken.None);
        Check(page.Screenshot?.Length > 0 && page.Text.Length > 0, "Fuente real legible en navegador: " + liveSource.Id);
    }
}

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
    var writer = new ExtractedOfferWriter(db);
    var extractionKey = ExtractedOfferWriter.Key(source, extracted);
    try
    {
        await writer.SaveAsync(source, document, parsed, CancellationToken.None);
        await writer.SaveAsync(source, document, parsed, CancellationToken.None);
        var stored = await db.Offers.Include(x => x.Benefits).SingleAsync(x => x.ExtractionKey == extractionKey);
        Check(await db.Offers.CountAsync(x => x.ExtractionKey == extractionKey) == 1 && stored.Status == OfferStatus.Pending,
            "Carga automática crea oferta pendiente y no duplica en nuevas lecturas");
        stored.Status = OfferStatus.Disabled; await db.SaveChangesAsync();
        await writer.SaveAsync(source, document, new ExtractionResult(false, [extracted with { Summary = "Nueva información" }]), CancellationToken.None);
        Check(stored.Status == OfferStatus.Disabled && stored.Summary == "Nueva información", "Actualización conserva oferta desactivada");
        stored.Status = OfferStatus.Published; await db.SaveChangesAsync();
        await writer.SaveAsync(source, document, new ExtractionResult(false, [extracted with { Summary = "Información modificada" }]), CancellationToken.None);
        Check(stored.Status == OfferStatus.Pending, "Cambio de información publicada exige nueva revisión");
        try { await writer.SaveAsync(source, document, new ExtractionResult(true, [extracted]), CancellationToken.None); throw new Exception("Extracción incierta aceptada"); }
        catch (InvalidOperationException) { Console.WriteLine("OK: Extracción incierta conserva datos existentes"); }
        Check(stored.Summary == "Información modificada", "Extracción fallida no borra datos anteriores");
        try { await writer.SaveAsync(source, document, new ExtractionResult(false, [extracted with { SourceUrl = "https://example.com/invented" }]), CancellationToken.None); throw new Exception("Enlace inventado aceptado"); }
        catch (InvalidOperationException) { Console.WriteLine("OK: Enlace fuera de evidencia rechazado"); }
    }
    finally
    {
        await db.Offers.Where(x => x.ExtractionKey == extractionKey).ExecuteDeleteAsync();
    }
    var coordinatorSource = new CaptureSource { Id = "test-coordinator", Chain = source.Chain, Url = source.Url };
    var coordinatorSettings = new CaptureSettings { Enabled = true, ApiKey = "test-only-not-a-real-key", Model = "test-model", RenderPages = false, Sources = [coordinatorSource] };
    var uncertainResponse = false;
    using var sourceStub = new HttpClient(new StubHttpHandler(_ => Task.FromResult("<html><body><h1>Oferta de prueba</h1><p>Oferta de prueba 10%: condiciones publicadas para el test.</p></body></html>")));
    using var aiStub = new HttpClient(new StubHttpHandler(_ => Task.FromResult(JsonSerializer.Serialize(new {
        status = "completed", output = new[] { new { type = "message", content = new[] { new {
            type = "output_text", text = JsonSerializer.Serialize(new ExtractionResult(uncertainResponse, [extracted]), serializer) } } } }
    }))));
    var services = new ServiceCollection();
    services.AddDbContext<OffersDbContext>(x => x.UseMySQL(connection));
    services.AddDbContext<CaptureDbContext>(x => x.UseMySQL(connection, mysql => mysql.MigrationsHistoryTable("__CaptureMigrationsHistory")));
    services.AddScoped<ExtractedOfferWriter>();
    services.AddScoped(_ => new SourceReader(sourceStub, coordinatorSettings));
    services.AddScoped(_ => new OpenAiOfferExtractor(aiStub));
    await using var provider = services.BuildServiceProvider();
    var coordinator = new CaptureCoordinator(provider.GetRequiredService<IServiceScopeFactory>(), coordinatorSettings, NullLogger<CaptureCoordinator>.Instance);
    try
    {
        var success = await coordinator.RunAsync(coordinatorSource.Id, CancellationToken.None);
        Check(success.Single().Status == "Succeeded" && success.Single().OfferCount == 1, "Flujo completo registra lectura y carga automáticas");
        uncertainResponse = true;
        var failed = await coordinator.RunAsync(coordinatorSource.Id, CancellationToken.None);
        Check(failed.Single().Status == "Failed" && failed.Single().Stage == "ValidateAndSave", "Extracción incierta queda registrada como fallo");
        Check(await db.Offers.CountAsync(x => x.CaptureSourceId == coordinatorSource.Id) == 1, "Fallo de lectura no elimina ni duplica oferta existente");
    }
    finally
    {
        await using var cleanupOffers = new OffersDbContext(options);
        var createdOffers = await cleanupOffers.Offers.Where(x => x.CaptureSourceId == coordinatorSource.Id).ToListAsync();
        cleanupOffers.Offers.RemoveRange(createdOffers); await cleanupOffers.SaveChangesAsync();
        var captureOptions = new DbContextOptionsBuilder<CaptureDbContext>().UseMySQL(connection, mysql => mysql.MigrationsHistoryTable("__CaptureMigrationsHistory")).Options;
        await using var cleanupRuns = new CaptureDbContext(captureOptions);
        var createdRuns = await cleanupRuns.Runs.Where(x => x.SourceId == coordinatorSource.Id).ToListAsync();
        cleanupRuns.Runs.RemoveRange(createdRuns); await cleanupRuns.SaveChangesAsync();
    }
    var adminPassword = Environment.GetEnvironmentVariable("Admin__Password");
    var adminUsername = Environment.GetEnvironmentVariable("Admin__Username");
    if (!string.IsNullOrWhiteSpace(adminPassword) && !string.IsNullOrWhiteSpace(adminUsername))
    {
        using var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
        using var admin = new HttpClient(handler) { BaseAddress = new Uri(apiUrl) };
        async Task<HttpResponseMessage> PostAdmin(string path, object? body = null)
        {
            var csrf = await admin.GetFromJsonAsync<Dictionary<string, string>>("/api/admin/csrf");
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Add("X-CSRF-TOKEN", csrf!["token"]);
            if (body is not null) request.Content = JsonContent.Create(body);
            return await admin.SendAsync(request);
        }
        Check((await admin.GetAsync("/api/admin/offers")).StatusCode == HttpStatusCode.Unauthorized, "Listado administrativo exige sesión");
        Check((await admin.PostAsJsonAsync("/api/admin/login", new { username = adminUsername, password = adminPassword })).StatusCode == HttpStatusCode.BadRequest,
            "Login rechaza solicitudes sin token CSRF");
        Check((await PostAdmin("/api/admin/login", new { username = adminUsername, password = "incorrect-password" })).StatusCode == HttpStatusCode.Unauthorized,
            "Credenciales incorrectas rechazadas");
        var login = await PostAdmin("/api/admin/login", new { username = adminUsername, password = adminPassword });
        Check(login.StatusCode == HttpStatusCode.NoContent, "Login válido crea sesión");
        Check(login.Headers.GetValues("Set-Cookie").Any(x => x.Contains("httponly", StringComparison.OrdinalIgnoreCase) && x.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase)),
            "Cookie de sesión HttpOnly y SameSite Strict");
        Check((await admin.GetAsync("/api/admin/session")).StatusCode == HttpStatusCode.OK, "Sesión administrativa consultable");
        Check((await admin.GetAsync("/api/admin/capture/status")).StatusCode == HttpStatusCode.OK, "Administrador consulta configuración de lectura sin secretos");
        Check((await admin.GetAsync("/api/admin/capture/runs")).StatusCode == HttpStatusCode.OK, "Administrador consulta registro de lecturas");
        Check((await PostAdmin("/api/admin/capture/run")).StatusCode == HttpStatusCode.Conflict, "Lectura sin clave o habilitación no se ejecuta");
        var pending = hidden[0];
        Check((await admin.GetAsync("/api/admin/offers/" + pending.Id)).StatusCode == HttpStatusCode.OK, "Administrador puede revisar ofertas pendientes");
        Check((await admin.PostAsync("/api/admin/offers/" + pending.Id + "/publish", null)).StatusCode == HttpStatusCode.BadRequest,
            "Publicación rechaza solicitudes sin token CSRF");
        Check((await PostAdmin("/api/admin/offers/" + pending.Id + "/publish")).StatusCode == HttpStatusCode.NoContent, "Administrador publica una oferta pendiente");
        Check((await client.GetAsync("/api/offers/" + pending.Id)).StatusCode == HttpStatusCode.OK, "Publicación habilita el detalle público");
        Check((await PostAdmin("/api/admin/offers/" + pending.Id + "/disable")).StatusCode == HttpStatusCode.NoContent, "Administrador desactiva una oferta");
        Check((await client.GetAsync("/api/offers/" + pending.Id)).StatusCode == HttpStatusCode.NotFound, "Desactivación retira el detalle público");
        pending.SourceUrl = "javascript:alert(1)";
        await db.SaveChangesAsync();
        Check((await PostAdmin("/api/admin/offers/" + pending.Id + "/publish")).StatusCode == HttpStatusCode.BadRequest, "Publicación rechaza fuente no HTTP/HTTPS");
        Check((await PostAdmin("/api/admin/logout")).StatusCode == HttpStatusCode.NoContent, "Cierre de sesión exitoso");
        Check((await admin.GetAsync("/api/admin/offers")).StatusCode == HttpStatusCode.Unauthorized, "Cookie cerrada no autoriza consultas");
        Check((await admin.PostAsync("/api/admin/offers/" + pending.Id + "/disable", null)).StatusCode == HttpStatusCode.Unauthorized,
            "Acción administrativa exige sesión en backend");
    }
}
finally
{
    await using var cleanup = new OffersDbContext(options);
    foreach (var fixture in fixtures)
    {
        var stored = await cleanup.Offers.SingleOrDefaultAsync(x => x.Id == fixture.Id);
        if (stored is not null) cleanup.Offers.Remove(stored);
    }
    await cleanup.SaveChangesAsync();
}
