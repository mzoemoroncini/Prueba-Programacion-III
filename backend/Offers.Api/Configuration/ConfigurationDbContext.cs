using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Offers.Api.Features.OfferCapture;
using Offers.Api.Features.OfferPublication;

namespace Offers.Api.Configuration;

public sealed class Administrator
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}
public sealed class ChainRegistration
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
public sealed class SourceRegistration
{
    public string Id { get; set; } = "";
    public int ChainId { get; set; }
    public string Url { get; set; } = "";
    public bool Enabled { get; set; }
}
public sealed class ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options) : DbContext(options)
{
    public DbSet<GeminiBudget> GeminiBudgets => Set<GeminiBudget>();
    public DbSet<Administrator> Administrators => Set<Administrator>();
    public DbSet<ChainRegistration> Chains => Set<ChainRegistration>();
    public DbSet<SourceRegistration> Sources => Set<SourceRegistration>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        var budget = builder.Entity<GeminiBudget>();
        budget.HasKey(x => x.Id); budget.Property(x => x.Id).ValueGeneratedNever();
        budget.Property(x => x.QuotaDay).HasColumnType("date");
        budget.Property(x => x.NextRequestAtUtc).HasColumnType("datetime(6)");
        budget.Property(x => x.BlockedUntilUtc).HasColumnType("datetime(6)");
        var admin = builder.Entity<Administrator>();
        admin.HasKey(x => x.Id); admin.Property(x => x.Id).HasColumnType("char(36)");
        admin.Property(x => x.Username).HasMaxLength(256).UseCollation("utf8mb4_bin");
        admin.Property(x => x.PasswordHash).HasMaxLength(1024);
        admin.HasIndex(x => x.Username).IsUnique();
        var chain = builder.Entity<ChainRegistration>();
        chain.HasKey(x => x.Id); chain.Property(x => x.Id).ValueGeneratedNever(); chain.Property(x => x.Name).HasMaxLength(100);
        var source = builder.Entity<SourceRegistration>();
        source.HasKey(x => x.Id); source.Property(x => x.Id).HasMaxLength(80);
        source.Property(x => x.Url).HasMaxLength(2048);
        source.HasOne<ChainRegistration>().WithMany().HasForeignKey(x => x.ChainId).OnDelete(DeleteBehavior.Restrict);
    }
    public async Task InitializeAsync(CaptureSettings settings, IConfiguration configuration, CancellationToken token = default)
    {
        await using var transaction = await Database.BeginTransactionAsync(token);
        if (!await GeminiBudgets.AnyAsync(token)) GeminiBudgets.Add(new GeminiBudget());
        foreach (var chain in Enum.GetValues<RestaurantChain>())
        {
            var record = await Chains.FindAsync([(int)chain], token);
            if (record is null) Chains.Add(new ChainRegistration { Id = (int)chain, Name = chain.ToString() });
        }
        await SaveChangesAsync(token);
        foreach (var record in await Sources.ToListAsync(token)) record.Enabled = false;
        if (settings.Sources.Select(x => x.Id).Distinct().Count() != settings.Sources.Count) throw new InvalidOperationException("Hay fuentes duplicadas en la configuración.");
        foreach (var source in settings.Sources)
        {
            if (string.IsNullOrWhiteSpace(source.Id) || source.Id.Length > 80 || !Uri.TryCreate(source.Url, UriKind.Absolute, out var url) || !SourceReader.IsOfficial(url, source.Chain))
                throw new InvalidOperationException("La configuración contiene una fuente no válida.");
            var record = await Sources.FindAsync([source.Id], token);
            if (record is null) { record = new SourceRegistration { Id = source.Id }; Sources.Add(record); }
            record.ChainId = (int)source.Chain; record.Url = source.Url; record.Enabled = true;
        }
        var username = configuration["Admin:Username"]; var password = configuration["Admin:Password"];
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            if (username.Length > 256 || password.Length < 12 || password.Length > 1024) throw new InvalidOperationException("El administrador requiere usuario de hasta 256 caracteres y contraseña de 12 a 1024 caracteres.");
            var admin = await Administrators.SingleOrDefaultAsync(x => x.Username == username, token);
            if (admin is null) { admin = new Administrator { Username = username }; Administrators.Add(admin); }
            var hasher = new PasswordHasher<Administrator>();
            if (string.IsNullOrEmpty(admin.PasswordHash) || hasher.VerifyHashedPassword(admin, admin.PasswordHash, password) != PasswordVerificationResult.Success)
                admin.PasswordHash = hasher.HashPassword(admin, password);
        }
        await SaveChangesAsync(token); await transaction.CommitAsync(token);
    }
}
public sealed class ConfigurationDbContextFactory : IDesignTimeDbContextFactory<ConfigurationDbContext>
{
    public ConfigurationDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ConfigurationDbContext>()
        .UseMySQL(Environment.GetEnvironmentVariable("ConnectionStrings__Offers") ?? "Server=localhost;Database=offers;User=not_configured;", x => x.MigrationsHistoryTable("__ConfigurationMigrationsHistory")).Options);
}
