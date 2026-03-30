using Qlt.Platform.Persistence.Migrations;

namespace Platform.TestWebApi.Infrastructure
{
    public class TestDbContextSeed : IDbSeeder<TestDbContext>
    {
        public async Task SeedAsync(TestDbContext context)
        {
            // Seed initial data here
        }
    }
}
