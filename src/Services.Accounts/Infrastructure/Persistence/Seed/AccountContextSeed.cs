using QltS.Shared.Persistence.EF;
using QltS.Shared.Persistence.EF.Relational.Migrations;

namespace QltS.Accounts.Infrastructure.Persistence.Seed
{
    public class QltDBContextSeed : IDbSeeder<QltDBContextBase>
    {
        public async Task SeedAsync(QltDBContextBase context)
        {
            // Seed initial data here
        }
    }
}
