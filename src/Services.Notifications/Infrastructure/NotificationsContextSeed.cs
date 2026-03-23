using QltS.Shared.Persistence.EF.Relational.Migrations;

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
