using QltSystem.Core.Persistence.EF.Extensions;

namespace QltSystem.Notifications.Infrastructure
{
    public class NotificationsContextSeed : IDbSeeder<NotificationsContext>
    {
        public async Task SeedAsync(NotificationsContext context)
        {
            // Seed initial data here
        }
    }
}
