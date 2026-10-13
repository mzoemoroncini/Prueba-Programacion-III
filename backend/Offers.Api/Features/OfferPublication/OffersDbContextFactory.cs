using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Offers.Api.Features.OfferPublication;

public sealed class OffersDbContextFactory : IDesignTimeDbContextFactory<OffersDbContext>
{
    public OffersDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Offers")
            ?? "Server=localhost;Port=3306;Database=offers;User=not_configured;Password=;";
        var options = new DbContextOptionsBuilder<OffersDbContext>().UseMySQL(connectionString).Options;
        return new OffersDbContext(options);
    }
}
