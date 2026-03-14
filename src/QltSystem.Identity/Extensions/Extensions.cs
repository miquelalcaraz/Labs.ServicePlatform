using Core.Persistence.EF.Relational.Extensions;

using Microsoft.EntityFrameworkCore;

using QltSystem.Accounts.Contracts.IntegrationEvents.Events;
using QltSystem.Core.EventBus.Abstractions;
using QltSystem.Core.EventBus.Extensions;
using QltSystem.Core.EventBus.Idempotency.Extensions;
using QltSystem.Core.EventBus.TransactionalOutbox.Extensions;
using QltSystem.Core.EventBusRabbitMQ;
using QltSystem.Core.Persistence.EF.Extensions;
using QltSystem.Identity.Behaviors;
using QltSystem.Identity.Infrastructure;
using QltSystem.Identity.IntegrationEvents.EventHandling;
using QltSystem.Identity.Repositories;
using QltSystem.Identity.Services;

namespace QltSystem.Identity.Extensions
{
    public static class Extensions
    {
        public static void AddApplicationServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("identitydb");

            // Register IdentityContext with QltSystem configuration
            services.AddQltSystemDBContext<IdentityContext>(
                conf => conf
                    .UseIdempotency()
                    .UseAudit()
                    .SetDefaultSchema("identity")
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly);
                })
            );

            services.AddMigration<IdentityContext, IdentityContextSeed>();

            services.AddTransactionalOutbox<IdentityContext>(options =>
            {
                options.RegisterEventTypes(typeof(AssemblyReference).Assembly);
            });

            services.AddEventIdempotency<IdentityContext>();

            services.AddCommandIdempotency<IdentityContext>();

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
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<IUserRepository, UserRepository>();

            // Register services
            services.AddSingleton<IPasswordGenerator, PasswordGenerator>();


        }

        private static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
        {
            eventBus.AddSubscription<OrganizationCreatedIntegrationEvent, OrganizationCreatedIntegrationEventHandler>();
        }
    }
}
