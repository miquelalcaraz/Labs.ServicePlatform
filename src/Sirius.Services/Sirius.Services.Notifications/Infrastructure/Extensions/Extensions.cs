using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Sirius.Contracts.Accounts.IntegrationEvents;
using Sirius.Platform.EventBus;
using Sirius.Platform.EventBus.Builders;
using Sirius.Platform.EventBus.Extensions;
using Sirius.Platform.Messaging.Builders;
using Sirius.Platform.Persistence.Builders;
using Sirius.Platform.Persistence.Migrations;
using Sirius.Services.Notifications.Domain.Repositories;
using Sirius.Services.Notifications.Domain.Services;
using Sirius.Services.Notifications.Infrastructure.IntegrationEvents.EventHandling;
using Sirius.Services.Notifications.Infrastructure.Persistence;
using Sirius.Services.Notifications.Infrastructure.Persistence.Repositories;
using Sirius.Services.Notifications.Infrastructure.Persistence.Seed;
using Sirius.Services.Notifications.Infrastructure.Services;



namespace Sirius.Services.Notifications.Infrastructure.Extensions
{
    public static class Extensions
    {
        public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("SiriusSystemDb");
            var readConnectionString = builder.Configuration.GetConnectionString("SiriusSystemDbReadonly")
                ?? connectionString;

            services.AddSiriusSDBContext<NotificationsContext>(
                conf => conf
                    .SetDefaultSchema("Notifications")
                    .WithIdempotency()
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly.FullName);
                })
            );

            services.AddSiriusReadDbContext<NotificationsReadContext>(
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

