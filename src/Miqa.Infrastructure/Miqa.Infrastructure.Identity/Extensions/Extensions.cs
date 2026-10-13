using System.Reflection;

using Duende.IdentityServer.Stores;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Miqa.Contracts.Accounts.IntegrationEvents;
using Miqa.Domain.Behaviors;
using Miqa.Infrastructure.Identity.Extensions;
using Miqa.Infrastructure.Identity.Infrastructure;
using Miqa.Infrastructure.Identity.Infrastructure.Seed;
using Miqa.Infrastructure.Identity.IntegrationEvents.EventHandling;
using Miqa.Infrastructure.Identity.Models;
using Miqa.Infrastructure.Identity.Repositories;
using Miqa.Infrastructure.Identity.Services;
using Miqa.Infrastructure.Identity.Stores;
using Miqa.Platform.EventBus;
using Miqa.Platform.EventBus.Builders;
using Miqa.Platform.EventBus.Extensions;
using Miqa.Platform.Messaging.Builders;
using Miqa.Platform.Persistence.Builders;
using Miqa.Platform.Persistence.Migrations;

namespace Miqa.Infrastructure.Identity.Extensions
{
    public static class Extensions
    {
        public static void AddApplicationServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            builder.Services.AddRazorPages();

            builder.AddIdentityServer();
            builder.AddDbContext();
            builder.AddInfrastructureServices();

            services.AddHttpContextAccessor();

            services.AddMiqaMediator(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly);
                cfg.WithLogging(typeof(LoggingBehavior<,>));
            });

            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddSingleton<IPasswordGenerator, PasswordGenerator>();
        }

        private static void AddInfrastructureServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            builder.AddServiceBusEventBus("eventbus")
                    .AddEventBusSubscriptions();

            services.AddIdempotencyEntityFramework<IdentityLogsContext>()
                .ForIntegrationEventHandlers();

            services.AddTransactionalOutbox<IdentityLogsContext>(options =>
            {
                options.RegisterEventTypes(
                    typeof(Contracts.Accounts.AssemblyReference).Assembly,
                    typeof(Contracts.Identity.AssemblyReference).Assembly);
            });
        }
        private static void AddDbContext(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("IdentityDb");

            // Register IdentityContext with Miqa configuration
            services.AddMiqaSDBContext<IdentityLogsContext>(
                    conf => conf.WithIdempotency(),
                options => options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly);
                })
            );

            services.AddMigration<IdentityLogsContext, IdentityLogsContextSeed>();


        }

        private static void AddIdentityServer(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;
            var connectionString = builder.Configuration.GetConnectionString("IdentityDb");

            var migrationsAssembly = typeof(AssemblyReference).GetTypeInfo().Assembly.GetName().Name;

            services.AddDbContext<IdentityContext>(builder => builder.UseSqlServer(connectionString,
                sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(migrationsAssembly);
                }));

            services.AddMigration<IdentityContext, IdentityContextSeed>();

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<IdentityContext>()
                .AddUserManager<ApplicationUserManager>()
                .AddDefaultTokenProviders();

            var issuerUri = builder.Configuration["IssuerUri"];

            var identityServerBuilder = builder.Services
                .AddIdentityServer(options =>
                {
                    if (!string.IsNullOrEmpty(issuerUri))
                    {
                        options.IssuerUri = issuerUri;
                    }

                    options.Discovery.CustomEntries.Add("local_api", "~/localapi");
                    options.Events.RaiseErrorEvents = true;
                    options.Events.RaiseInformationEvents = true;
                    options.Events.RaiseFailureEvents = true;
                    options.Events.RaiseSuccessEvents = true;

                    // see https://docs.duendesoftware.com/identityserver/v6/fundamentals/resources/
                    options.EmitStaticAudienceClaim = false;

                });


            identityServerBuilder.AddIdentityProviderStore<DynamicIdentityProviderStore>();

            identityServerBuilder.AddOperationalStore(options =>
                options.ConfigureDbContext = builder =>
                builder.UseSqlServer(connectionString, sqlOptions => sqlOptions.MigrationsAssembly(migrationsAssembly)));

            identityServerBuilder.AddConfigurationStore(options =>
                options.ConfigureDbContext = builder =>
                builder.UseSqlServer(connectionString, sqlOptions => sqlOptions.MigrationsAssembly(migrationsAssembly)));


            //services.AddMigration<PersistedGrantDbContext, PersistedGrantDbSeed>();
            //services.AddMigration<ConfigurationDbContext, ConfigurationDbContextSeed>();
            services.AddMigration<IdentityContext, IdentityContextSeed>();

            services.AddTransient<IIdentityProviderStore, DynamicIdentityProviderStore>();

            identityServerBuilder.AddAspNetIdentity<ApplicationUser>();
            identityServerBuilder.AddProfileService<ProfileService>();

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = "Cookie";
                options.DefaultChallengeScheme = "Cookie";
            }).AddCookie("Cookie");

            services.AddAuthorization(options => options.AddPolicy("TwoFactorEnabled", x => x.RequireClaim("amr", "mfa")));

            services.AddLocalApiAuthentication();

        }
        private static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
        {
            eventBus.AddSubscription<TenantCreatedIntegrationEvent, TenantCreatedIntegrationEventHandler>();
            eventBus.AddSubscription<CustomerAdminProvisioningRequestedIntegrationEvent, CustomerAdminProvisioningRequestedIntegrationEventHandler>();
        }
    }
}
