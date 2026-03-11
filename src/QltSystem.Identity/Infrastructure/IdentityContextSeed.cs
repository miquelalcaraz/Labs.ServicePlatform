using QltSystem.Core.Persistence.EF.Extensions;

namespace QltSystem.Identity.Infrastructure
{
    public class IdentityContextSeed : IDbSeeder<IdentityContext>
    {
        public async Task SeedAsync(IdentityContext context)
        {
            // Seed initial data here
        }
    }
}
