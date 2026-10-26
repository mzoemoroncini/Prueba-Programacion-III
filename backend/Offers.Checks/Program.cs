using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Offers.Api.Features.OfferCapture;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
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
string GeminiResult(string finishReason = "STOP", ExtractionResult? value = null) => JsonSerializer.Serialize(new {
    candidates = new[] { new { finishReason, content = new { parts = new[] {
        new { text = JsonSerializer.Serialize(value ?? extraction, serializer) } } } } }
});
using var fakeGemini = new HttpClient(new StubHttpHandler(async request => {
    Check(request.RequestUri!.AbsoluteUri == "https://generativelanguage.googleapis.com/v1beta/models/test-model:generateContent"
        && request.Headers.GetValues("x-goog-api-key").Single() == aiSettings.ApiKey, "Gemini usa endpoint fijo y clave en cabecera");
    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
    var parts = body.RootElement.GetProperty("contents")[0].GetProperty("parts");
    Check(parts.EnumerateArray().Count(x => x.TryGetProperty("inlineData", out _)) == 2, "Gemini recibe captura y PDF cuando OCR está desactivado");
    Check(body.RootElement.GetProperty("generationConfig").GetProperty("responseJsonSchema").GetProperty("required").GetArrayLength() == 2, "Gemini solicita el contrato estructurado de ofertas");
    return GeminiResult();
}));
var geminiParsed = await new GeminiOfferExtractor(fakeGemini).ExtractAsync(document, aiSettings, CancellationToken.None);
Check(geminiParsed.Offers.Single().Code == extracted.Code && geminiParsed.Offers.Single().Benefits.Single().Percentage == 10m,
    "Gemini conserva códigos y valores publicados");
foreach (var invalid in new[] { GeminiResult("MAX_TOKENS"), GeminiResult("SAFETY"), "{}", "{\"candidates\":[]}",
    "{\"candidates\":[{\"finishReason\":\"STOP\",\"content\":{\"parts\":[{\"text\":\"{\\\"uncertain\\\":false,\\\"offers\\\":null}\"}]}}]}" })
{
    using var incompleteGemini = new HttpClient(new StubHttpHandler(_ => Task.FromResult(invalid)));
    try { await new GeminiOfferExtractor(incompleteGemini).ExtractAsync(document, aiSettings, CancellationToken.None); throw new Exception("Gemini incompleto aceptado"); }
    catch (InvalidOperationException) { Console.WriteLine("OK: Gemini bloqueado, vacío o incompleto no genera ofertas"); }
}
foreach (var statusCode in new[] { HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable })
{
    using var unavailableGemini = new HttpClient(new StubHttpHandler(_ => Task.FromResult("private-provider-error"), statusCode));
    try { await new GeminiOfferExtractor(unavailableGemini).ExtractAsync(document, aiSettings, CancellationToken.None); throw new Exception("Fallo Gemini aceptado"); }
    catch (InvalidOperationException error) { Check(!error.Message.Contains("private-provider-error") && !error.Message.Contains(aiSettings.ApiKey!),
        "Fallo de cuota o disponibilidad no devuelve ofertas ni revela datos del proveedor"); }
}
if (Environment.GetEnvironmentVariable("OFFERS_CHECKS_LIVE_GEMINI") == "true")
{
    var configuration = new ConfigurationBuilder().AddUserSecrets(typeof(GeminiOfferExtractor).Assembly).AddEnvironmentVariables().Build();
    var liveSettings = new CaptureSettings();
    configuration.GetSection("Capture").Bind(liveSettings);
    using var liveHttp = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
    var liveDocument = new SourceDocument(source.Url,
        "Oferta de prueba: 10% de descuento. Código Test-zZ-01. Válida del 2026-10-13 al 2026-10-31. Solo en mostrador.",
        null, [], [source.Url]);
    var liveResult = await new GeminiOfferExtractor(liveHttp).ExtractAsync(liveDocument, liveSettings, CancellationToken.None);
    Check(!liveResult.Uncertain && liveResult.Offers.Count == 1 && liveResult.Offers[0].Code == "Test-zZ-01"
        && liveResult.Offers[0].EndsOn == new DateOnly(2026, 10, 31)
        && liveResult.Offers[0].Benefits.Any(x => x.Percentage == 10m),
        "Gemini real extrae código, fecha y porcentaje de un texto de prueba sin guardar ofertas");
}
var ocrSettings = new OcrSettings { Enabled = true, ApiKey = "test-only-ocr-key" };
string OcrResult(int exitCode = 1, int pageCode = 1) => JsonSerializer.Serialize(new {
    IsErroredOnProcessing = false, OCRExitCode = exitCode.ToString(),
    ParsedResults = new[] { new { FileParseExitCode = pageCode, ParsedText = "Código Test-zZ-01, descuento 10%" } }
});
var ocrCalls = 0;
using var fakeOcr = new HttpClient(new StubHttpHandler(async request => {
    ocrCalls++;
    Check(request.RequestUri!.AbsoluteUri == "https://api.ocr.space/parse/image" && request.Headers.GetValues("apikey").Single() == ocrSettings.ApiKey,
        "OCR usa endpoint fijo y clave en cabecera");
    var multipart = (MultipartFormDataContent)request.Content!;
    Check(await multipart.Single(x => x.Headers.ContentDisposition!.Name!.Trim('"') == "OCREngine").ReadAsStringAsync() == "2"
        && await multipart.Single(x => x.Headers.ContentDisposition!.Name!.Trim('"') == "language").ReadAsStringAsync() == "auto",
        "OCR selecciona motor 2 e idioma automático");
    return OcrResult();
}));
var ocrDocument = await new OcrSpaceClient(fakeOcr).ReadAsync(document, ocrSettings, CancellationToken.None);
Check(ocrCalls == 2 && ocrDocument.Text.Contains(document.Text) && ocrDocument.Text.Contains("Código Test-zZ-01")
    && ocrDocument.Screenshot is null && ocrDocument.Attachments.Count == 0 && ocrDocument.Links.SequenceEqual(document.Links),
    "OCR combina texto de imagen y PDF, conserva enlaces y retira archivos para IA");
using var textAi = new HttpClient(new StubHttpHandler(async request => {
    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
    Check(body.RootElement.GetProperty("input")[0].GetProperty("content").EnumerateArray()
        .All(x => x.GetProperty("type").GetString() == "input_text"), "Tras OCR, IA recibe únicamente texto");
    return JsonSerializer.Serialize(new { status = "completed", output = new[] { new { type = "message", content = new[] {
        new { type = "output_text", text = JsonSerializer.Serialize(extraction, serializer) } } } } });
}));
await new OpenAiOfferExtractor(textAi).ExtractAsync(ocrDocument, aiSettings, CancellationToken.None);

using var textGemini = new HttpClient(new StubHttpHandler(async request => {
    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
    Check(body.RootElement.GetProperty("contents")[0].GetProperty("parts").EnumerateArray().All(x => x.TryGetProperty("text", out _)),
        "Tras OCR, Gemini recibe únicamente texto");
    return GeminiResult();
}));
await new GeminiOfferExtractor(textGemini).ExtractAsync(ocrDocument, aiSettings, CancellationToken.None);
var oversized = document with { Attachments = [new SourceAttachment(source.Url, "large.pdf", "application/pdf", new byte[OcrSpaceClient.MaxFileBytes + 1])] };
try { await new OcrSpaceClient(fakeOcr).ReadAsync(oversized, ocrSettings, CancellationToken.None); throw new Exception("Archivo grande aceptado"); }
catch (InvalidOperationException) { Check(ocrCalls == 2, "Archivos grandes se rechazan antes de cualquier llamada OCR"); }
foreach (var result in new[] { OcrResult(2), OcrResult(1, 3) })
{
    using var badOcr = new HttpClient(new StubHttpHandler(_ => Task.FromResult(result)));
    try { await new OcrSpaceClient(badOcr).ReadAsync(document, ocrSettings, CancellationToken.None); throw new Exception("OCR parcial aceptado"); }
    catch (InvalidOperationException) { Console.WriteLine("OK: OCR parcial o página fallida no genera extracción"); }
}
Check(!new CaptureSettings { Enabled = true, ApiKey = "test", Model = "test", Sources = [source], Ocr = new OcrSettings { Enabled = true } }.IsReady,
    "Sin clave OCR la captación queda deshabilitada");

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
    Check(await db.Interactions.CountAsync(x => x.OfferId == visible.Id && x.Action == "PublicView") == 1, "Detalle público registra una consulta sin contar el listado");
    foreach (var hiddenOffer in hidden)
        Check(!await db.Interactions.AnyAsync(x => x.OfferId == hiddenOffer.Id), "Detalle fallido no genera consultas");
    Check(!await db.Interactions.AnyAsync(x => x.OfferId == visible.Id && x.Action == "SourceAccess"), "Abrir detalle no registra acceso al origen");
    Check((await client.PostAsync($"/api/offers/{visible.Id}/source-access", null)).StatusCode == HttpStatusCode.NoContent, "Activación del origen registrada");
    Check(await db.Interactions.CountAsync(x => x.OfferId == visible.Id && x.Action == "SourceAccess") == 1, "Un acceso conserva oferta y fecha");
    Check((await client.GetAsync("/api/admin/reports")).StatusCode == HttpStatusCode.Unauthorized, "Reportes requieren sesión administrativa");
    Check((await client.PostAsJsonAsync($"/api/offers/{visible.Id}/reports", new { reason = "invalid" })).StatusCode == HttpStatusCode.BadRequest, "Reporte exige motivo válido");
    Check((await client.PostAsJsonAsync($"/api/offers/{visible.Id}/reports", new { reason = "Expired", comment = new string('x', 2001) })).StatusCode == HttpStatusCode.BadRequest, "Comentario extenso rechazado");
    Check(!await db.Reports.AnyAsync(x => x.OfferId == visible.Id), "Envíos fallidos no cuentan como reportes");
    Check((await client.PostAsJsonAsync($"/api/offers/{visible.Id}/reports", new { reason = "Expired" })).StatusCode == HttpStatusCode.Created, "Reporte anónimo sin comentario aceptado");
    Check((await client.PostAsJsonAsync($"/api/offers/{visible.Id}/reports", new { reason = "IncorrectData", comment = " El precio difiere " })).StatusCode == HttpStatusCode.Created, "Reporte con comentario aceptado");
    var feedback = await db.Reports.Where(x => x.OfferId == visible.Id).ToListAsync();
    Check(feedback.Count == 2 && feedback.Any(x => x.Comment == null) && feedback.Any(x => x.Comment == "El precio difiere") && feedback.All(x => x.CreatedAt != default), "Un registro por envío conserva motivo, comentario y fecha");
    Check((await db.Offers.AsNoTracking().SingleAsync(x => x.Id == visible.Id)).Status == OfferStatus.Published, "Reportar no desactiva ofertas");
    Check((await client.PostAsJsonAsync($"/api/offers/{hidden[0].Id}/reports", new { reason = "Expired" })).StatusCode == HttpStatusCode.NotFound, "Reporte no expone ofertas ocultas");
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
    var coordinatorSettings = new CaptureSettings { Provider = "OpenAI", Enabled = true, ApiKey = "test-only-not-a-real-key", Model = "test-model", RenderPages = false, Sources = [coordinatorSource], Ocr = ocrSettings };
    var uncertainResponse = false;
    var partialOcr = false;
    using var coordinatorOcr = new HttpClient(new StubHttpHandler(_ => Task.FromResult(OcrResult(partialOcr ? 2 : 1))));
    using var sourceStub = new HttpClient(new StubHttpHandler(request => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith(".pdf")
        ? "%PDF-test" : "<html><body><h1>Oferta de prueba</h1><a href=\"terms.pdf\">Condiciones</a><p>Oferta de prueba 10%</p></body></html>")));
    using var aiStub = new HttpClient(new StubHttpHandler(_ => Task.FromResult(JsonSerializer.Serialize(new {
        status = "completed", output = new[] { new { type = "message", content = new[] { new {
            type = "output_text", text = JsonSerializer.Serialize(new ExtractionResult(uncertainResponse, [extracted]), serializer) } } } }
    }))));
    var services = new ServiceCollection();
    services.AddDbContext<OffersDbContext>(x => x.UseMySQL(connection));
    services.AddDbContext<CaptureDbContext>(x => x.UseMySQL(connection, mysql => mysql.MigrationsHistoryTable("__CaptureMigrationsHistory")));
    services.AddScoped<ExtractedOfferWriter>();
    services.AddScoped(_ => new SourceReader(sourceStub, coordinatorSettings));
    services.AddScoped<IOfferExtractor>(_ => new OpenAiOfferExtractor(aiStub));
    services.AddScoped(_ => new OcrSpaceClient(coordinatorOcr));
    await using var provider = services.BuildServiceProvider();
    var coordinator = new CaptureCoordinator(provider.GetRequiredService<IServiceScopeFactory>(), coordinatorSettings, NullLogger<CaptureCoordinator>.Instance);
    try
    {
        var success = await coordinator.RunAsync(coordinatorSource.Id, CancellationToken.None);
        Check(success.Single().Status == "Succeeded" && success.Single().OfferCount == 1, "Flujo completo registra lectura y carga automáticas");
        uncertainResponse = true;
        var failed = await coordinator.RunAsync(coordinatorSource.Id, CancellationToken.None);
        Check(failed.Single().Status == "Failed" && failed.Single().Stage == "ValidateAndSave", "Extracción incierta queda registrada como fallo");
        partialOcr = true;
        var ocrFailed = await coordinator.RunAsync(coordinatorSource.Id, CancellationToken.None);
        Check(ocrFailed.Single().Status == "Failed" && ocrFailed.Single().Stage == "OCR", "Fallo OCR queda registrado antes de invocar IA");
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
        visible.Status = OfferStatus.Disabled; await db.SaveChangesAsync();
        var reportResponse = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/reports?offerId={visible.Id}");
        Check(reportResponse.GetArrayLength() == 2 && reportResponse[0].GetProperty("offerId").GetGuid() == visible.Id, "Reportes siguen disponibles al desactivar una oferta");
        var beforeAdmin = await db.Interactions.CountAsync(x => x.OfferId == visible.Id);
        Check((await admin.GetAsync($"/api/admin/offers/{visible.Id}")).StatusCode == HttpStatusCode.OK, "Administrador abre oferta reportada");
        Check(await db.Interactions.CountAsync(x => x.OfferId == visible.Id) == beforeAdmin, "Consulta administrativa no cuenta como pública");
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
