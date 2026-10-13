using Microsoft.EntityFrameworkCore;

namespace Offers.Api.Features.OfferPublication;

public sealed class OffersDbContext(DbContextOptions<OffersDbContext> options) : DbContext(options)
{
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<OfferBenefit> OfferBenefits => Set<OfferBenefit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var offer = modelBuilder.Entity<Offer>();
        offer.ToTable("Offers");
        offer.HasKey(x => x.Id);
        offer.Property(x => x.Id).HasColumnType("char(36)");
        offer.Property(x => x.SourceUrl).IsRequired();
        offer.Property(x => x.Status).HasDefaultValue(OfferStatus.Pending);
        offer.Property(x => x.StartsOn).HasConversion(v => v.HasValue ? v.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null, v => v.HasValue ? DateOnly.FromDateTime(v.Value) : (DateOnly?)null).HasColumnType("date");
        offer.Property(x => x.EndsOn).HasConversion(v => v.HasValue ? v.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null, v => v.HasValue ? DateOnly.FromDateTime(v.Value) : (DateOnly?)null).HasColumnType("date");
        offer.Property(x => x.CreatedAt).HasConversion(v => v.UtcDateTime, v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc))).HasColumnType("datetime(6)");
        offer.Property(x => x.LastExtractedAt).HasConversion(v => v.HasValue ? v.Value.UtcDateTime : (DateTime?)null, v => v.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : (DateTimeOffset?)null).HasColumnType("datetime(6)");
        offer.HasIndex(x => x.ExtractionKey).IsUnique();
        offer.HasIndex(x => new { x.Status, x.StartsOn, x.EndsOn });
        offer.HasMany(x => x.Benefits).WithOne().HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.Cascade);

        var benefit = modelBuilder.Entity<OfferBenefit>();
        benefit.ToTable("OfferBenefits");
        benefit.HasKey(x => x.Id);
        benefit.Property(x => x.Id).HasColumnType("char(36)");
        benefit.Property(x => x.OfferId).HasColumnType("char(36)");
        benefit.Property(x => x.Percentage).HasPrecision(7, 4);
        benefit.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        benefit.Property(x => x.PromotionalPrice).HasPrecision(18, 2);
    }
}
