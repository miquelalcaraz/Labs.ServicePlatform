using Microsoft.EntityFrameworkCore;

using QltS.Accounts.Contracts.IntegrationEvents.Events;
using QltS.Core.Application.Behaviors;
using QltS.Notifications.Infrastructure;
using QltS.Notifications.IntegrationEvents.EventHandling;
using QltS.Notifications.Repositories;
using QltS.Notifications.Services;
using QltS.Shared.EventBus.Abstractions;
using QltS.Shared.EventBus.Extensions;
using QltS.Shared.EventBus.RabbitMQ;
using QltS.Shared.Persistence.EF.Behaviors;
using QltS.Shared.Persistence.EF.Relational.Extensions;
using QltS.Shared.Persistence.EF.Relational.Migrations;

namespace QltS.Notifications.Extensions
{
    public static class Extensions
    {
        public static void AddApplicationServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("notificationsdb");

            services.AddQltSDBContext<NotificationsContext>(
                conf => conf
                    .WithRequestIdempotency()
                    .WithIntegrationEventInbox()
                    .WithAuditLogs()
                     .WithTransactionalOutbox()
                     .RegisterIntegrationEventsFromAssembly(typeof(Accounts.Contracts.AssemblyReference).Assembly)
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
                cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
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
