using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Miqa.Contracts.Accounts.IntegrationEvents;
using Miqa.Platform.EventBus;
using Miqa.Platform.EventBus.Builders;
using Miqa.Platform.EventBus.Extensions;
using Miqa.Platform.Messaging.Builders;
using Miqa.Platform.Persistence.Builders;
using Miqa.Platform.Persistence.Migrations;
using Miqa.Services.Notifications.Domain.Repositories;
using Miqa.Services.Notifications.Domain.Services;
using Miqa.Services.Notifications.Infrastructure.IntegrationEvents.EventHandling;
using Miqa.Services.Notifications.Infrastructure.Persistence;
using Miqa.Services.Notifications.Infrastructure.Persistence.Repositories;
using Miqa.Services.Notifications.Infrastructure.Persistence.Seed;
using Miqa.Services.Notifications.Infrastructure.Services;



namespace Miqa.Services.Notifications.Infrastructure.Extensions
{
    public static class Extensions
    {
        public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("MiqaSystemDb");
            var readConnectionString = builder.Configuration.GetConnectionString("MiqaSystemDbReadonly")
                ?? connectionString;

            services.AddMiqaSDBContext<NotificationsContext>(
                conf => conf
                    .SetDefaultSchema("Notifications")
                    .WithIdempotency()
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly.FullName);
                })
            );

            services.AddMiqaReadDbContext<NotificationsReadContext>(
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

