using Microsoft.EntityFrameworkCore;

using QltSystem.Core.EventBus.Abstractions;
using QltSystem.Core.EventBus.Extensions;
using QltSystem.Core.EventBusRabbitMQ;
using QltSystem.Core.Infrastructure.Extensions;
using QltSystem.Core.IntegrationEventLogEF.Services;
using QltSystem.Identity.Infrastructure;
using QltSystem.Identity.IntegrationEvents.EventHandling;
using QltSystem.Identity.IntegrationEvents.Events;
using QltSystem.Identity.Repositories;
using QltSystem.Identity.Services;

namespace QltSystem.Identity.Extensions
{
    public static class Extensions
    {
        public static void AddApplicationServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            // Add database context
            services.AddDbContext<IdentityContext>(options =>
            {
                var connectionString = builder.Configuration.GetConnectionString("identitydb");
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(Program).Assembly.FullName);
                });
            });
            services.AddMigration<IdentityContext, IdentityContextSeed>();
            builder.AddRabbitMqEventBus("eventbus")
                    .AddEventBusSubscriptions();

            services.AddHttpContextAccessor();

            // Register repositories (in-memory for now)
            services.AddSingleton<ITenantRepository, InMemoryTenantRepository>();
            services.AddSingleton<IUserRepository, InMemoryUserRepository>();

            // Register services
            services.AddSingleton<IPasswordGenerator, PasswordGenerator>();

            // Register idempotency service
            services.AddScoped<IEventIdempotencyService, EventIdempotencyService<IdentityContext>>();
        }

        private static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
        {
            eventBus.AddSubscription<OrganizationCreatedIntegrationEvent, OrganizationCreatedIntegrationEventHandler>();
        }
    }
}
