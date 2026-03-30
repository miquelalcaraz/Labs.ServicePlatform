using Qlt.Platform.Persistence.Migrations;

namespace Qlt.Services.Accounts.Infrastructure.Persistence.Seed
{
    public class QltDBContextSeed : IDbSeeder<AccountContext>
    {
        public async Task SeedAsync(AccountContext context)
        {
            // Seed initial data here
        }
    }
}
