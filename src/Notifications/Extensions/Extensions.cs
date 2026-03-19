using Microsoft.EntityFrameworkCore;

using QltS.Accounts.Contracts.IntegrationEvents.Events;
using QltS.Core.EventBus.Abstractions;
using QltS.Core.EventBus.Extensions;
using QltS.Core.EventBus.Idempotency.Extensions;
using QltS.Core.EventBus.TransactionalOutbox.Extensions;
using QltS.Core.EventBusRabbitMQ;
using QltS.Core.Persistence.EF.Extensions;
using QltS.Core.Persistence.EF.Relational.Extensions;
using QltS.Notifications.Behaviors;
using QltS.Notifications.Infrastructure;
using QltS.Notifications.IntegrationEvents.EventHandling;
using QltS.Notifications.Repositories;
using QltS.Notifications.Services;

namespace QltS.Notifications.Extensions
{
    public static class Extensions
    {
        public static void AddApplicationServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("notificationsdb");

            // Register NotificationsContext with QltS configuration
            services.AddQltSDBContext<NotificationsContext>(
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
