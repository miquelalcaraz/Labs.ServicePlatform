using Core.Persistence.EF.Relational.Extensions;
using Core.Persistence.EF.Relational.Migrations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using QltSystem.Accounts.Domain.Repositories;
using QltSystem.Accounts.Infrastructure.Behaviors;
using QltSystem.Accounts.Infrastructure.Persistence;
using QltSystem.Accounts.Infrastructure.Persistence.Repositories;
using QltSystem.Accounts.Infrastructure.Persistence.Seed;
using QltSystem.Core.EventBus.Abstractions;
using QltSystem.Core.EventBus.Idempotency.Extensions;
using QltSystem.Core.EventBus.TransactionalOutbox.Extensions;
using QltSystem.Core.EventBusRabbitMQ;
using QltSystem.Core.Persistence.EF.Extensions;

namespace QltSystem.Accounts.Infrastructure.Extensions
{
    public static class Extensions
    {
        public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("accountdb");

            // Register AccountContext with QltSystem configuration
            services.AddQltSystemDBContext<AccountContext>(
                conf => conf
                    .UseIdempotency()
                    .UseAudit()
                    .SetDefaultSchema("accounts")
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly.FullName);
                })
            );

            // Use Core.Persistence.EF.Relational migration extensions
            services.AddMigration<AccountContext, QltDBContextSeed>();

            services.AddTransactionalOutbox<AccountContext>(options =>
            {
                options.RegisterEventTypes(typeof(Contracts.AssemblyReference).Assembly);
            });

            services.AddEventIdempotency<AccountContext>();

            services.AddCommandIdempotency<AccountContext>();

            builder.AddRabbitMqEventBus("eventbus")
                .AddEventBusSubscriptions();

            services.AddHttpContextAccessor();

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblyContaining(typeof(AssemblyReference));
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddTransactionalOutboxBehavior();
            });

            services.AddScoped<IOrganizationRepository, OrganizationRepository>();

        }

        private static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
        {
        }
    }

}

