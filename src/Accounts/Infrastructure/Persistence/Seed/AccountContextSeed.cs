using QltSystem.Core.Persistence.EF;
using QltSystem.Core.Persistence.EF.Extensions;

namespace QltSystem.Accounts.Infrastructure.Persistence.Seed
{
    public class QltDBContextSeed : IDbSeeder<DBContextBase>
    {
        public async Task SeedAsync(DBContextBase context)
        {
            // Seed initial data here
        }
    }
}
