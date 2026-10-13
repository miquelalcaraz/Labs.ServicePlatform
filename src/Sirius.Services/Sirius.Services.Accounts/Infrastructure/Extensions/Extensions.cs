using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Sirius.Contracts.Identity.IntegrationEvents;
using Sirius.Contracts.Notifications.IntegrationEvents;
using Sirius.Platform.EventBus;
using Sirius.Platform.EventBus.Builders;
using Sirius.Platform.EventBus.Extensions;
using Sirius.Platform.Messaging.Builders;
using Sirius.Platform.Persistence.Builders;
using Sirius.Platform.Persistence.Migrations;
using Sirius.Services.Accounts.Domain.Repositories;
using Sirius.Services.Accounts.Infrastructure.IntegrationEvents.EventHandling;
using Sirius.Services.Accounts.Infrastructure.Persistence;
using Sirius.Services.Accounts.Infrastructure.Persistence.Repositories;
using Sirius.Services.Accounts.Infrastructure.Persistence.Seed;

namespace Sirius.Services.Accounts.Infrastructure.Extensions
{
    public static class Extensions
    {
        public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("SiriusDb");
            var readConnectionString = builder.Configuration.GetConnectionString("SiriusDbReadonly")
                ?? connectionString;

            services.AddSiriusSDBContext<AccountContext>(
                conf => conf
                    .SetDefaultSchema("Accounts")
                    .WithIdempotency()
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly.FullName);
                })
            );

            services.AddSiriusReadDbContext<AccountReadContext>(
                conf => conf
                    .SetDefaultSchema("Accounts")
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(readConnectionString)
            );

            services.AddHealthChecks()
                .AddDbContextCheck<AccountContext>(name: "accounts-db", tags: ["ready"]);

            services.AddMigration<AccountContext, SiriusDBContextSeed>();


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
                    typeof(Sirius.Contracts.Accounts.AssemblyReference).Assembly,
                    typeof(Sirius.Contracts.Identity.AssemblyReference).Assembly,
                    typeof(Sirius.Contracts.Notifications.AssemblyReference).Assembly);
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

