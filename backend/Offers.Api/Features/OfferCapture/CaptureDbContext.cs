using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Offers.Api.Features.OfferCapture;

public sealed class CaptureRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string SourceId { get; set; }
    public required string SourceUrl { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = "Running";
    public string Stage { get; set; } = "Read";
    public string? Error { get; set; }
    public int OfferCount { get; set; }
}

public sealed class CaptureDbContext(DbContextOptions<CaptureDbContext> options) : DbContext(options)
{
    public DbSet<CaptureRun> Runs => Set<CaptureRun>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        var run = builder.Entity<CaptureRun>();
        run.ToTable("CaptureRuns"); run.HasKey(x => x.Id);
        run.Property(x => x.Id).HasColumnType("char(36)");
        run.Property(x => x.SourceId).HasMaxLength(80);
        run.Property(x => x.SourceUrl).HasMaxLength(2048);
        run.Property(x => x.StartedAt).HasColumnType("datetime(6)");
        run.Property(x => x.FinishedAt).HasColumnType("datetime(6)");
        run.Property(x => x.Status).HasMaxLength(20);
        run.Property(x => x.Stage).HasMaxLength(20);
        run.Property(x => x.Error).HasMaxLength(1000);
        run.HasIndex(x => x.StartedAt);
    }
}

public sealed class CaptureDbContextFactory : IDesignTimeDbContextFactory<CaptureDbContext>
{
    public CaptureDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Offers") ?? "Server=localhost;Database=offers;User=not_configured;Password=;";
        var options = new DbContextOptionsBuilder<CaptureDbContext>().UseMySQL(connection, x => x.MigrationsHistoryTable("__CaptureMigrationsHistory")).Options;
        return new CaptureDbContext(options);
    }
}
