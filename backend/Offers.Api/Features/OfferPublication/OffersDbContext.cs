using Microsoft.EntityFrameworkCore;
using Offers.Api.Features.OfferReports;

namespace Offers.Api.Features.OfferPublication;

public sealed class OffersDbContext(DbContextOptions<OffersDbContext> options) : DbContext(options)
{
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<OfferBenefit> OfferBenefits => Set<OfferBenefit>();

    public DbSet<OfferReport> Reports => Set<OfferReport>();
    public DbSet<OfferInteraction> Interactions => Set<OfferInteraction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var report = modelBuilder.Entity<OfferReport>();
        report.ToTable("OfferReports"); report.HasKey(x => x.Id);
        report.Property(x => x.Id).HasColumnType("char(36)");
        report.Property(x => x.OfferId).HasColumnType("char(36)");
        report.Property(x => x.Reason).HasMaxLength(30);
        report.Property(x => x.Comment).HasMaxLength(2000);
        report.Property(x => x.CreatedAt).HasColumnType("datetime(6)");
        report.HasOne<Offer>().WithMany().HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.Cascade);
        var interaction = modelBuilder.Entity<OfferInteraction>();
        interaction.ToTable("OfferInteractions"); interaction.HasKey(x => x.Id);
        interaction.Property(x => x.Id).HasColumnType("char(36)");
        interaction.Property(x => x.OfferId).HasColumnType("char(36)");
        interaction.Property(x => x.Action).HasMaxLength(30);
        interaction.Property(x => x.CreatedAt).HasColumnType("datetime(6)");
        interaction.HasOne<Offer>().WithMany().HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.Cascade);
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
