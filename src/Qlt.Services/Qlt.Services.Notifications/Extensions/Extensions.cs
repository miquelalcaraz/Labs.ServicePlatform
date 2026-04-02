using Microsoft.EntityFrameworkCore;

using Qlt.Domain.Behaviors;
using Qlt.Platform.EventBus;
using Qlt.Platform.EventBus.Builders;
using Qlt.Platform.EventBus.Extensions;
using Qlt.Platform.Messaging.Behaviors;
using Qlt.Platform.Messaging.Builders;
using Qlt.Platform.Persistence.Builders;
using Qlt.Platform.Persistence.Migrations;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;
using Qlt.Services.Notifications.Infrastructure;
using Qlt.Services.Notifications.IntegrationEvents.EventHandling;
using Qlt.Services.Notifications.Repositories;
using Qlt.Services.Notifications.Services;
namespace Qlt.Services.Notifications.Extensions
{
    public static class Extensions
    {
        public static void AddApplicationServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("notificationsdb");

            services.AddQltSDBContext<NotificationsContext>(
                conf => conf
                    .SetDefaultSchema("notifications")
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly);
                })
            );

            services.AddMigration<NotificationsContext, NotificationsContextSeed>();


            builder.AddServiceBusEventBus("eventbus")
                    .AddEventBusSubscriptions();

            services.AddHttpContextAccessor();

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblyContaining(typeof(AssemblyReference));
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
                cfg.AddOpenBehavior(typeof(IntegrationEventProcessorBehavior<,>));
                cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
                cfg.AddOpenBehavior(typeof(DomainEventProcessorBehavior<,>));
            });



            services.AddIntegrationEventTelemetryDecorator();
            services.AddDomainEventTelemetryBehavior();
            services.AddCommandTelemetryBehavior();
            services.AddQueryTelemetryBehavior();

            services.AddIdempotencyEntityFramework<NotificationsContext>()
                .ForIntegrationEventHandlers();

            services.AddTransactionalOutbox<NotificationsContext>(options =>
            {
                options.RegisterEventTypes(typeof(Accounts.Contracts.AssemblyReference).Assembly);
            });


            // Register repositories (in-memory for now)
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IEmailRepository, EmailRepository>();
        }

        private static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
        {
            eventBus.AddSubscription<OrganizationCreatedIntegrationEvent, OrganizationCreatedIntegrationEventHandler>();
        }
    }
}
