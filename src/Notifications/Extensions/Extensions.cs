using Core.Persistence.EF.Relational.Extensions;

using Microsoft.EntityFrameworkCore;

using QltSystem.Accounts.Contracts.IntegrationEvents.Events;
using QltSystem.Core.EventBus.Abstractions;
using QltSystem.Core.EventBus.Extensions;
using QltSystem.Core.EventBus.Idempotency.Extensions;
using QltSystem.Core.EventBus.TransactionalOutbox.Extensions;
using QltSystem.Core.EventBusRabbitMQ;
using QltSystem.Core.Persistence.EF.Extensions;
using QltSystem.Notifications.Behaviors;
using QltSystem.Notifications.Infrastructure;
using QltSystem.Notifications.IntegrationEvents.EventHandling;
using QltSystem.Notifications.Repositories;
using QltSystem.Notifications.Services;

namespace QltSystem.Notifications.Extensions
{
    public static class Extensions
    {
        public static void AddApplicationServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("notificationsdb");

            // Register NotificationsContext with QltSystem configuration
            services.AddQltSystemDBContext<NotificationsContext>(
                conf => conf
                    .UseIdempotency()
                    .UseAudit()
                    .SetDefaultSchema("notifications")
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly);
                })
            );

            services.AddMigration<NotificationsContext, NotificationsContextSeed>();

            services.AddTransactionalOutbox<NotificationsContext>(options =>
            {
                options.RegisterEventTypes(typeof(AssemblyReference).Assembly);
            });

            services.AddEventIdempotency<NotificationsContext>();

            services.AddCommandIdempotency<NotificationsContext>();

            builder.AddRabbitMqEventBus("eventbus")
                    .AddEventBusSubscriptions();

            services.AddHttpContextAccessor();

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblyContaining(typeof(AssemblyReference));
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddTransactionalOutboxBehavior();
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
