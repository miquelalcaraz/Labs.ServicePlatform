using Sirius.Platform.Persistence.Migrations;

namespace Sirius.Services.Notifications.Infrastructure.Persistence.Seed
{
    public class NotificationsContextSeed : IDbSeeder<NotificationsContext>
    {
        public async Task SeedAsync(NotificationsContext context)
        {
            // Seed initial data here
        }
    }
}
