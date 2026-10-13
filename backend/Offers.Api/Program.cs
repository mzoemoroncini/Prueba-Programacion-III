using Microsoft.EntityFrameworkCore;
using Offers.Api.Features.OfferPublication;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Offers");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<OffersDbContext>(options => options.UseMySQL(connectionString));
}
var app = builder.Build();
app.Run();
