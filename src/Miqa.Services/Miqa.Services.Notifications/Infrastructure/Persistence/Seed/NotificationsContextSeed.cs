using Miqa.Platform.Persistence.Migrations;

namespace Miqa.Services.Notifications.Infrastructure.Persistence.Seed
{
    public class NotificationsContextSeed : IDbSeeder<NotificationsContext>
    {
        public async Task SeedAsync(NotificationsContext context)
        {
            // Seed initial data here
        }
    }
}
