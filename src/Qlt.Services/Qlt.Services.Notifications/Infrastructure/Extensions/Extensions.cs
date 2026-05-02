using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Qlt.Contracts.Accounts.IntegrationEvents;
using Qlt.Platform.EventBus;
using Qlt.Platform.EventBus.Builders;
using Qlt.Platform.EventBus.Extensions;
using Qlt.Platform.Messaging.Builders;
using Qlt.Platform.Persistence.Builders;
using Qlt.Platform.Persistence.Migrations;
using Qlt.Services.Notifications.Domain.Repositories;
using Qlt.Services.Notifications.Domain.Services;
using Qlt.Services.Notifications.Infrastructure.IntegrationEvents.EventHandling;
using Qlt.Services.Notifications.Infrastructure.Persistence;
using Qlt.Services.Notifications.Infrastructure.Persistence.Repositories;
using Qlt.Services.Notifications.Infrastructure.Persistence.Seed;
using Qlt.Services.Notifications.Infrastructure.Services;



namespace Qlt.Services.Notifications.Infrastructure.Extensions
{
    public static class Extensions
    {
        public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("QltSystemDb");
            var readConnectionString = builder.Configuration.GetConnectionString("QltSystemDbReadonly")
                ?? connectionString;

            services.AddQltSDBContext<NotificationsContext>(
                conf => conf
                    .SetDefaultSchema("Notifications")
                    .WithIdempotency()
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly.FullName);
                })
            );

            services.AddQltReadDbContext<NotificationsReadContext>(
                conf => conf
                    .SetDefaultSchema("Notifications")
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(readConnectionString)
            );

            services.AddHealthChecks()
                .AddDbContextCheck<NotificationsContext>(name: "Notifications-db", tags: ["ready"]);

            services.AddMigration<NotificationsContext, NotificationsContextSeed>();


            builder.AddServiceBusEventBus("eventbus")
                .AddEventBusSubscriptions();

            services.AddRedisIdempotencyCache(builder.Configuration)
                   .ForApiRequests()
                    .ForIntegrationEventHandlers();

            services.AddIdempotencyEntityFramework<NotificationsContext>()
                .ForApiRequests()
                .ForIntegrationEventHandlers();

            services.AddTransactionalOutbox<NotificationsContext>(options =>
            {
                options.RegisterEventTypes(
                    typeof(Contracts.Notifications.AssemblyReference).Assembly,
                    typeof(Contracts.Accounts.AssemblyReference).Assembly);
            });


            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IEmailNotificationRepository, EmailNotificationRepository>();

        }

        private static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
        {
            eventBus.AddSubscription<CompanyCreatedIntegrationEvent, CompanyCreatedIntegrationEventHandler>();
            eventBus.AddSubscription<ActivationEmailRequestedIntegrationEvent, ActivationEmailRequestedIntegrationEventHandler>();
        }
    }

}

