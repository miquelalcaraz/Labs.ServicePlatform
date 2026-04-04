using Microsoft.EntityFrameworkCore;

using Qlt.Platform.Persistence;
using Qlt.Platform.Persistence.Options;

namespace Platform.TestWebApi.Infrastructure;

public class TestDbContext : QltDBContext
{
    public TestDbContext(
        DbContextOptions<TestDbContext> options,
        QltDbContextConfiguration configuration)
        : base(options, configuration)
    {
    }

    public DbSet<TestEntity> TestEntities => Set<TestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestEntity>(entity =>
        {
            entity.ToTable("TestEntities", "test");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Value).IsRequired();
        });
    }
}
