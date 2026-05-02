using Microsoft.EntityFrameworkCore;

using Qlt.Platform.Persistence;
using Qlt.Platform.Persistence.Options;

namespace Platform.TestWebApi.Infrastructure;

public class TestReadDbContext : QltReadDbContext
{
    public TestReadDbContext(
        DbContextOptions<TestDbContext> options,
        QltReadDbContextConfiguration configuration)
        : base(options, configuration)
    {
    }

    public DbSet<TestEntity> TestEntities => Set<TestEntity>();

 
}
