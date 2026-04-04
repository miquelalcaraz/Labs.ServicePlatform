using Microsoft.EntityFrameworkCore;

using Qlt.Platform.Persistence.Configurations;
using Qlt.Platform.Persistence.Options;

namespace Qlt.Platform.Persistence;

/// <summary>
/// Base DbContext for read-only query operations.
/// NoTracking by default. Prevents accidental writes.
/// Does not participate in transactions and carries no infrastructure tables.
/// </summary>
public abstract class QltReadDbContext : DbContext
{
    private readonly QltReadDbContextConfiguration _configuration;

    protected QltReadDbContext(DbContextOptions options, QltReadDbContextConfiguration configuration) : base(options)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;
        ChangeTracker.AutoDetectChangesEnabled = false;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var assembly in _configuration.AssembliesToRegister)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }

        if (!string.IsNullOrEmpty(_configuration.DefaultSchema))
        {
            modelBuilder.HasDefaultSchema(_configuration.DefaultSchema);
        }

        modelBuilder.ConfigureEntityAuditProperties();
    }

    public sealed override int SaveChanges() =>
        throw new InvalidOperationException("Read-only context. Use the write context to persist changes.");

    public sealed override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new InvalidOperationException("Read-only context. Use the write context to persist changes.");

    public sealed override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Read-only context. Use the write context to persist changes.");

    public sealed override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Read-only context. Use the write context to persist changes.");
}
