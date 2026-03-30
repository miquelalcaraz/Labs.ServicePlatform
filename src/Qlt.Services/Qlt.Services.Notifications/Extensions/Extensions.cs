using Microsoft.EntityFrameworkCore;

using Qlt.Domain.Behaviors;
using Qlt.Platform.EventBus;
using Qlt.Platform.EventBus.Abstractions;
using Qlt.Platform.EventBus.Extensions;
using Qlt.Platform.Messaging.Behaviors;
using Qlt.Platform.Messaging.Extensions;
using Qlt.Platform.Persistence.Behaviors;
using Qlt.Platform.Persistence.Extensions;
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


            builder.AddRabbitMqEventBus("eventbus")
                    .AddEventBusSubscriptions();

            services.AddHttpContextAccessor();

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblyContaining(typeof(AssemblyReference));
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(MessagingBehavior<,>));
                cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
                cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
            });



            // ✅ Register decorators for event handlers (order matters!)
            // 1. Telemetry decorator (outer) - Measures total execution time including idempotency checks
            services.AddIntegrationEventTelemetryDecorator();
            services.AddDomainEventTelemetryDecorator();

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
