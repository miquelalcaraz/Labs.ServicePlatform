using Microsoft.EntityFrameworkCore;

using Miqa.Platform.Persistence;
using Miqa.Platform.Persistence.Options;

namespace Platform.TestWebApi.Infrastructure;

public class TestReadDbContext : MiqaReadDbContext
{
    public TestReadDbContext(
        DbContextOptions<TestDbContext> options,
        MiqaReadDbContextConfiguration configuration)
        : base(options, configuration)
    {
    }

    public DbSet<TestEntity> TestEntities => Set<TestEntity>();

 
}
