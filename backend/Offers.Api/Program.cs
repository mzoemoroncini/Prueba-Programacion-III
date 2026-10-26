using Microsoft.EntityFrameworkCore;
using Offers.Api.Configuration;
using Offers.Api.Features.OfferReports;
using System.Threading.RateLimiting;
using Offers.Api.Features.OfferBrowsing;
using Offers.Api.Features.OfferCapture;
using Offers.Api.Features.SourceReprocessing;
using Offers.Api.Features.OfferPublication;

var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("feedback", http => RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.AddAdminAccess();
var captureSettings = new CaptureSettings();
builder.Configuration.GetSection("Capture").Bind(captureSettings);
builder.Configuration.GetSection("Ocr").Bind(captureSettings.Ocr);
builder.Services.AddSingleton(captureSettings);
builder.Services.AddHttpClient<OcrSpaceClient>(client => client.Timeout = TimeSpan.FromMinutes(2));
builder.Services.AddHttpClient<SourceReader>(client => { client.Timeout = TimeSpan.FromSeconds(60); client.DefaultRequestHeaders.UserAgent.ParseAdd("OffersMVP/0.1"); }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = System.Net.DecompressionMethods.All });
builder.Services.AddHttpClient<GeminiOfferExtractor>(client => client.Timeout = TimeSpan.FromMinutes(3));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTransient<IOfferExtractor>(services => services.GetRequiredService<GeminiOfferExtractor>());
builder.Services.AddSingleton<CaptureCoordinator>();
builder.Services.AddHostedService<CaptureWorker>();
var connectionString = builder.Configuration.GetConnectionString("Offers");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddScoped<GeminiRequestBudget>();
    builder.Services.AddScoped<ExtractedOfferWriter>();
    builder.Services.AddDbContext<ConfigurationDbContext>(options => options.UseMySQL(connectionString, x => x.MigrationsHistoryTable("__ConfigurationMigrationsHistory")));
    builder.Services.AddDbContext<CaptureDbContext>(options => options.UseMySQL(connectionString, x => x.MigrationsHistoryTable("__CaptureMigrationsHistory")));
    builder.Services.AddDbContext<OffersDbContext>(options => options.UseMySQL(connectionString));
}
var app = builder.Build();
if (args.Contains("--migrate"))
{
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Falta la conexión MySQL para aplicar migraciones.");
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<OffersDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<CaptureDbContext>().Database.MigrateAsync();
    var configurationDb = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
    await configurationDb.Database.MigrateAsync();
    await configurationDb.InitializeAsync(captureSettings, builder.Configuration);
    return;
}
app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapAdminAccess();
app.MapAdminOffers();
app.MapOfferBrowsing();
app.MapOfferFeedback();
app.MapCapture();
app.MapGet("/health", async (IServiceProvider services) =>
{
    try
    {
        var offers = services.GetService<OffersDbContext>();
        var capture = services.GetService<CaptureDbContext>();
        var configuration = services.GetService<ConfigurationDbContext>();
        if (offers is null || capture is null || configuration is null || !await offers.Database.CanConnectAsync()
            || (await offers.Database.GetPendingMigrationsAsync()).Any() || (await capture.Database.GetPendingMigrationsAsync()).Any()
            || (await configuration.Database.GetPendingMigrationsAsync()).Any()) return Results.Problem(statusCode:503, title:"La base de datos no está disponible o faltan migraciones.");
        return Results.Ok(new { status = "ok" });
    }
    catch { return Results.Problem(statusCode:503, title:"La base de datos no está disponible."); }
});
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
app.Run();
