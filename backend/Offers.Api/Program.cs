using Microsoft.EntityFrameworkCore;
using Offers.Api.Features.OfferBrowsing;
using Offers.Api.Features.OfferCapture;
using Offers.Api.Features.SourceReprocessing;
using Offers.Api.Features.OfferPublication;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.AddAdminAccess();
var captureSettings = new CaptureSettings();
builder.Configuration.GetSection("Capture").Bind(captureSettings);
builder.Configuration.GetSection("Ocr").Bind(captureSettings.Ocr);
builder.Services.AddSingleton(captureSettings);
builder.Services.AddHttpClient<OcrSpaceClient>(client => client.Timeout = TimeSpan.FromMinutes(2));
builder.Services.AddHttpClient<SourceReader>(client => { client.Timeout = TimeSpan.FromSeconds(60); client.DefaultRequestHeaders.UserAgent.ParseAdd("OffersMVP/0.1"); }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = System.Net.DecompressionMethods.All });
builder.Services.AddHttpClient<OpenAiOfferExtractor>(client => client.Timeout = TimeSpan.FromMinutes(3));
builder.Services.AddScoped<ExtractedOfferWriter>();
builder.Services.AddSingleton<CaptureCoordinator>();
builder.Services.AddHostedService<CaptureWorker>();
var connectionString = builder.Configuration.GetConnectionString("Offers");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<CaptureDbContext>(options => options.UseMySQL(connectionString, x => x.MigrationsHistoryTable("__CaptureMigrationsHistory")));
    builder.Services.AddDbContext<OffersDbContext>(options => options.UseMySQL(connectionString));
}
var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapAdminAccess();
app.MapAdminOffers();
app.MapOfferBrowsing();
app.MapCapture();
app.Run();
