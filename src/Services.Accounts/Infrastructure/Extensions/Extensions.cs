using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using QltS.Accounts.Domain.Repositories;
using QltS.Accounts.Infrastructure.Persistence;
using QltS.Accounts.Infrastructure.Persistence.Repositories;
using QltS.Accounts.Infrastructure.Persistence.Seed;
using QltS.Shared.EventBus.Abstractions;

using QltS.Shared.EventBus.RabbitMQ;
using QltS.Shared.Persistence.EF.Relational.Extensions;
using QltS.Shared.Persistence.EF.Relational.Migrations;

namespace QltS.Accounts.Infrastructure.Extensions
{
    public static class Extensions
    {
        public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("accountdb");

            // Register AccountContext with QltS configuration
            services.AddQltSDBContext<AccountContext>(
                conf => conf
                    .WithRequestIdempotency()
                    .WithIntegrationEventInbox()
                    .WithAuditLogs()
                    .WithTransactionalOutbox()
                    .SetDefaultSchema("accounts")
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly)
                    .RegisterIntegrationEventsFromAssembly(typeof(Accounts.Contracts.AssemblyReference).Assembly),
                options => options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly.FullName);
                })

            );


            services.AddMigration<AccountContext, QltDBContextSeed>();




            builder.AddRabbitMqEventBus("eventbus")
                .AddEventBusSubscriptions();



            services.AddScoped<IOrganizationRepository, OrganizationRepository>();

        }

        private static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
        {
        }
    }

}

