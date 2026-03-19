using QltS.Core.Persistence.EF.Extensions;

namespace QltS.Notifications.Infrastructure
{
    public class NotificationsContextSeed : IDbSeeder<NotificationsContext>
    {
        public async Task SeedAsync(NotificationsContext context)
        {
            // Seed initial data here
        }
    }
}
