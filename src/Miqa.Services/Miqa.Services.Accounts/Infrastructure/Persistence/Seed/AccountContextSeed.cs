using Miqa.Platform.Persistence.Migrations;

namespace Miqa.Services.Accounts.Infrastructure.Persistence.Seed
{
    public class MiqaDBContextSeed : IDbSeeder<AccountContext>
    {
        public async Task SeedAsync(AccountContext context)
        {
            // Seed initial data here
        }
    }
}
