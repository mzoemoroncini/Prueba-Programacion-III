using Microsoft.EntityFrameworkCore;
using Offers.Api.Features.OfferBrowsing;
using Offers.Api.Features.OfferPublication;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
var connectionString = builder.Configuration.GetConnectionString("Offers");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<OffersDbContext>(options => options.UseMySQL(connectionString));
}
var app = builder.Build();
app.UseExceptionHandler();
app.MapOfferBrowsing();
app.Run();
