using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Qlt.Contracts.Identity.IntegrationEvents;
using Qlt.Contracts.Notifications.IntegrationEvents;
using Qlt.Platform.EventBus;
using Qlt.Platform.EventBus.Builders;
using Qlt.Platform.EventBus.Extensions;
using Qlt.Platform.Messaging.Builders;
using Qlt.Platform.Persistence.Builders;
using Qlt.Platform.Persistence.Migrations;
using Qlt.Services.Accounts.Domain.Repositories;
using Qlt.Services.Accounts.Infrastructure.IntegrationEvents.EventHandling;
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

            var connectionString = builder.Configuration.GetConnectionString("QltSystemDb");
            var readConnectionString = builder.Configuration.GetConnectionString("QltSystemDbReadonly")
                ?? connectionString;

            services.AddQltSDBContext<AccountContext>(
                conf => conf
                    .SetDefaultSchema("Accounts")
                    .WithIdempotency()
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly.FullName);
                })
            );

            services.AddQltReadDbContext<AccountReadContext>(
                conf => conf
                    .SetDefaultSchema("Accounts")
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(readConnectionString)
            );

            services.AddHealthChecks()
                .AddDbContextCheck<AccountContext>(name: "accounts-db", tags: ["ready"]);

            services.AddMigration<AccountContext, QltDBContextSeed>();


            builder.AddServiceBusEventBus("eventbus")
                .AddEventBusSubscriptions();

            services.AddRedisIdempotencyCache(builder.Configuration)
                   .ForApiRequests()
                .ForIntegrationEventHandlers();

            services.AddIdempotencyEntityFramework<AccountContext>()
                .ForApiRequests()
                .ForIntegrationEventHandlers();

            services.AddTransactionalOutbox<AccountContext>(options =>
            {
                options.RegisterEventTypes(
                    typeof(Qlt.Contracts.Accounts.AssemblyReference).Assembly,
                    typeof(Qlt.Contracts.Identity.AssemblyReference).Assembly,
                    typeof(Qlt.Contracts.Notifications.AssemblyReference).Assembly);
            });


            services.AddScoped<ICompanyRepository, CompanyRepository>();
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
            services.AddScoped<IUserMembershipRepository, UserMembershipRepository>();
            services.AddScoped<IOnboardingProcessRepository, OnboardingProcessRepository>();
            services.AddScoped<ICustomerActivationProcessRepository, CustomerActivationProcessRepository>();

        }

        private static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
        {
            eventBus.AddSubscription<ExternalUserProvisionedIntegrationEvent, ExternalUserProvisionedIntegrationEventHandler>();
            eventBus.AddSubscription<ActivationDeliveredIntegrationEvent, ActivationDeliveredIntegrationEventHandler>();
            eventBus.AddSubscription<UserActivatedIntegrationEvent, UserActivatedIntegrationEventHandler>();
        }
    }

}

