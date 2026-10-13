using Microsoft.EntityFrameworkCore;

using Sirius.Platform.Persistence;
using Sirius.Platform.Persistence.Options;

namespace Platform.TestWebApi.Infrastructure;

public class TestReadDbContext : SiriusReadDbContext
{
    public TestReadDbContext(
        DbContextOptions<TestDbContext> options,
        SiriusReadDbContextConfiguration configuration)
        : base(options, configuration)
    {
    }

    public DbSet<TestEntity> TestEntities => Set<TestEntity>();

 
}
