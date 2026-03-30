using Qlt.Platform.Persistence.Migrations;

namespace Qlt.Services.Notifications.Infrastructure
{
    public class NotificationsContextSeed : IDbSeeder<NotificationsContext>
    {
        public async Task SeedAsync(NotificationsContext context)
        {
            // Seed initial data here
        }
    }
}
