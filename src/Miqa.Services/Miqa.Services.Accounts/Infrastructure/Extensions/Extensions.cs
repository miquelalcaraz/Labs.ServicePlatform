using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Miqa.Contracts.Identity.IntegrationEvents;
using Miqa.Contracts.Notifications.IntegrationEvents;
using Miqa.Platform.EventBus;
using Miqa.Platform.EventBus.Builders;
using Miqa.Platform.EventBus.Extensions;
using Miqa.Platform.Messaging.Builders;
using Miqa.Platform.Persistence.Builders;
using Miqa.Platform.Persistence.Migrations;
using Miqa.Services.Accounts.Domain.Repositories;
using Miqa.Services.Accounts.Infrastructure.IntegrationEvents.EventHandling;
using Miqa.Services.Accounts.Infrastructure.Persistence;
using Miqa.Services.Accounts.Infrastructure.Persistence.Repositories;
using Miqa.Services.Accounts.Infrastructure.Persistence.Seed;

namespace Miqa.Services.Accounts.Infrastructure.Extensions
{
    public static class Extensions
    {
        public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("MiqaSystemDb");
            var readConnectionString = builder.Configuration.GetConnectionString("MiqaSystemDbReadonly")
                ?? connectionString;

            services.AddMiqaSDBContext<AccountContext>(
                conf => conf
                    .SetDefaultSchema("Accounts")
                    .WithIdempotency()
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly.FullName);
                })
            );

            services.AddMiqaReadDbContext<AccountReadContext>(
                conf => conf
                    .SetDefaultSchema("Accounts")
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(readConnectionString)
            );

            services.AddHealthChecks()
                .AddDbContextCheck<AccountContext>(name: "accounts-db", tags: ["ready"]);

            services.AddMigration<AccountContext, MiqaDBContextSeed>();


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
                    typeof(Miqa.Contracts.Accounts.AssemblyReference).Assembly,
                    typeof(Miqa.Contracts.Identity.AssemblyReference).Assembly,
                    typeof(Miqa.Contracts.Notifications.AssemblyReference).Assembly);
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

