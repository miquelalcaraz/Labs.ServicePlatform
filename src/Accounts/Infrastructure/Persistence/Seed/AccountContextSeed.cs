using QltS.Core.Persistence.EF;
using QltS.Core.Persistence.EF.Extensions;

namespace QltS.Accounts.Infrastructure.Persistence.Seed
{
    public class QltDBContextSeed : IDbSeeder<DBContextBase>
    {
        public async Task SeedAsync(DBContextBase context)
        {
            // Seed initial data here
        }
    }
}
