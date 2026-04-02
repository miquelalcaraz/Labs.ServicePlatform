using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Qlt.Platform.EventBus;
using Qlt.Platform.EventBus.Builders;
using Qlt.Platform.Messaging.Builders;
using Qlt.Platform.Persistence.Builders;
using Qlt.Platform.Persistence.Migrations;
using Qlt.Services.Accounts.Domain.Repositories;
using Qlt.Services.Accounts.Infrastructure.Persistence;
using Qlt.Services.Accounts.Infrastructure.Persistence.Repositories;
using Qlt.Services.Accounts.Infrastructure.Persistence.Seed;

namespace Qlt.Services.Accounts.Infrastructure.Extensions
{
    public static class Extensions
    {
        public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("accountdb");
            var readConnectionString = builder.Configuration.GetConnectionString("accountdb-readonly")
                ?? connectionString;

            services.AddQltSDBContext<AccountContext>(
                conf => conf
                    .SetDefaultSchema("accounts")
                    .WithIdempotency()
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly.FullName);
                })
            );

            services.AddQltReadDbContext<AccountReadContext>(
                conf => conf
                    .SetDefaultSchema("accounts")
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(readConnectionString)
            );


            services.AddMigration<AccountContext, QltDBContextSeed>();


            builder.AddServiceBusEventBus("eventbus")
                .AddEventBusSubscriptions();

            services.AddIntegrationEventTelemetryDecorator();
            services.AddDomainEventTelemetryBehavior();
            services.AddCommandTelemetryBehavior();
            services.AddQueryTelemetryBehavior();

            services.AddRedisIdempotencyCache(builder.Configuration)
                   .ForApiRequests()
                .ForIntegrationEventHandlers();

            services.AddIdempotencyEntityFramework<AccountContext>()
                .ForApiRequests()
                .ForIntegrationEventHandlers();

            services.AddTransactionalOutbox<AccountContext>(options =>
            {
                options.RegisterEventTypes(typeof(Contracts.AssemblyReference).Assembly);
            });


            services.AddScoped<IOrganizationRepository, OrganizationRepository>();

        }

        private static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
        {
        }
    }

}

