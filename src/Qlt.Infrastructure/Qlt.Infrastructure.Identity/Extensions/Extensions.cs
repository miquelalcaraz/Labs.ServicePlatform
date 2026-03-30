using Duende.IdentityServer.Stores;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Qlt.Infrastructure.Identity.Behaviors;
using Qlt.Infrastructure.Identity.Extensions;
using Qlt.Infrastructure.Identity.Infrastructure;
using Qlt.Infrastructure.Identity.Infrastructure.Seed;
using Qlt.Infrastructure.Identity.IntegrationEvents.EventHandling;
using Qlt.Infrastructure.Identity.Models;
using Qlt.Infrastructure.Identity.Repositories;
using Qlt.Infrastructure.Identity.Services;
using Qlt.Infrastructure.Identity.Stores;
using Qlt.Platform.EventBus;
using Qlt.Platform.EventBus.Abstractions;
using Qlt.Platform.EventBus.Extensions;
using Qlt.Platform.Messaging.Behaviors;
using Qlt.Platform.Messaging.Extensions;
using Qlt.Platform.Persistence.Behaviors;
using Qlt.Platform.Persistence.Extensions;
using Qlt.Platform.Persistence.Migrations;
using Qlt.Services.Accounts.Contracts.IntegrationEvents;

using System.Reflection;

namespace Qlt.Infrastructure.Identity.Extensions
{
    public static class Extensions
    {
        public static void AddApplicationServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            builder.Services.AddRazorPages();


            builder.AddIdentityServer();
            builder.AddDbContext();

            builder.AddRabbitMqEventBus("eventbus")
                    .AddEventBusSubscriptions();

            services.AddHttpContextAccessor();

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblyContaining(typeof(AssemblyReference));
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(MessagingBehavior<,>));
                cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
                cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
            });

            // Register repositories (in-memory for now)
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<IUserRepository, UserRepository>();

            // Register services
            services.AddSingleton<IPasswordGenerator, PasswordGenerator>();

            // ✅ Register decorators for event handlers (order matters!)
            // 1. Telemetry decorator (outer) - Measures total execution time including idempotency checks
            services.AddIntegrationEventTelemetryDecorator();
            services.AddDomainEventTelemetryDecorator();


            services.AddIdempotencyEntityFramework<IdentityLogsContext>()
                .ForIntegrationEventHandlers();

            services.AddTransactionalOutbox<IdentityLogsContext>(options =>
            {
                options.RegisterEventTypes(typeof(Qlt.Services.Accounts.Contracts.AssemblyReference).Assembly);
            });

        }
        private static void AddDbContext(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("identityLogsDb");

            // Register IdentityContext with Qlt configuration
            services.AddQltSDBContext<IdentityLogsContext>(
                    conf =>
                    conf.SetDefaultSchema("identityLogs")
                    .WithIdempotency(),
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
            var connectionString = builder.Configuration.GetConnectionString("identityDb");

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

            var identityServerBuilder = builder.Services
                .AddIdentityServer(options =>
                {
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
            eventBus.AddSubscription<OrganizationCreatedIntegrationEvent, OrganizationCreatedIntegrationEventHandler>();
        }
    }
}
