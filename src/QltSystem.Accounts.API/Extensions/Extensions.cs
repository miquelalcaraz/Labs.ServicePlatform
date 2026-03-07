using Microsoft.EntityFrameworkCore;

using QltSystem.Accounts.Application.Behaviors;
using QltSystem.Accounts.Application.IntegrationEvents;
using QltSystem.Accounts.Application.Queries;
using QltSystem.Accounts.Domain.OrganizationAggregate;
using QltSystem.Accounts.Infrastructure;
using QltSystem.Accounts.Infrastructure.Idempotency;
using QltSystem.Accounts.Infrastructure.Repositories;
using QltSystem.Accounts.Infrastructure.Seed;
using QltSystem.Core.EventBus.Abstractions;
using QltSystem.Core.EventBusRabbitMQ;
using QltSystem.Core.Infrastructure.Extensions;
using QltSystem.Core.IntegrationEventLogEF.Services;

namespace QltSystem.Accounts.Extensions
{
    public static class Extensions
    {
        public static void AddApplicationServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("accountdb");
            services.AddDbContext<AccountContext>(options =>
            {
                var connectionString = builder.Configuration.GetConnectionString("accountdb");

                options.UseSqlServer(connectionString, options =>
                {
                    options.MigrationsAssembly(typeof(Program).Assembly.FullName);
                });

            });
            services.AddMigration<AccountContext, AccountContextSeed>();

            // Add the integration services that consume the DbContext
            services.AddTransient<IIntegrationEventLogService, IntegrationEventLogService<AccountContext>>();
            services.AddTransient<IAccountIntegrationEventService, AccountIntegrationEventService>();

            builder.AddRabbitMqEventBus("eventbus")
                .AddEventBusSubscriptions();

            services.AddHttpContextAccessor();

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblyContaining(typeof(Program));
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
            });

            services.AddScoped<IOrganizationQueries, OrganizationQueries>();
            services.AddScoped<IOrganizationRepository, OrganizationRepository>();
            services.AddScoped<IRequestManager, RequestManager>();

        }

        private static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
        {
        }
    }
}
