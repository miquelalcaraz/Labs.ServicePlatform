using Sirius.Platform.Persistence.Migrations;

namespace Sirius.Services.Accounts.Infrastructure.Persistence.Seed
{
    public class SiriusDBContextSeed : IDbSeeder<AccountContext>
    {
        public async Task SeedAsync(AccountContext context)
        {
            // Seed initial data here
        }
    }
}
