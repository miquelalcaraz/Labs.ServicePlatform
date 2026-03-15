using Core.Persistence.EF.Relational.Extensions;

using Duende.IdentityServer.Stores;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using QltSystem.Accounts.Contracts.IntegrationEvents.Events;
using QltSystem.Core.EventBus.Abstractions;
using QltSystem.Core.EventBus.Extensions;
using QltSystem.Core.EventBus.Idempotency.Extensions;
using QltSystem.Core.EventBus.TransactionalOutbox.Extensions;
using QltSystem.Core.EventBusRabbitMQ;
using QltSystem.Core.Persistence.EF.Extensions;
using QltSystem.Identity.Behaviors;
using QltSystem.Identity.Infrastructure;
using QltSystem.Identity.Infrastructure.Seed;
using QltSystem.Identity.IntegrationEvents.EventHandling;
using QltSystem.Identity.Models;
using QltSystem.Identity.Repositories;
using QltSystem.Identity.Services;
using QltSystem.Identity.Stores;

using System.Reflection;

namespace QltSystem.Identity.Extensions
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
                cfg.AddTransactionalOutboxBehavior();
            });

            // Register repositories (in-memory for now)
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<IUserRepository, UserRepository>();

            // Register services
            services.AddSingleton<IPasswordGenerator, PasswordGenerator>();


        }
        private static void AddDbContext(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            var connectionString = builder.Configuration.GetConnectionString("identityLogsDb");

            // Register IdentityContext with QltSystem configuration
            services.AddQltSystemDBContext<IdentityLogsContext>(
                conf => conf
                    .UseIdempotency()
                    .UseAudit()
                    .SetDefaultSchema("identityLogs"),
                options => options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly);
                })
            );

            services.AddMigration<IdentityLogsContext, IdentityLogsContextSeed>();

            services.AddTransactionalOutbox<IdentityLogsContext>(options =>
            {
                options.RegisterEventTypes(typeof(AssemblyReference).Assembly);
            });

            services.AddEventIdempotency<IdentityLogsContext>();

            services.AddCommandIdempotency<IdentityLogsContext>();
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
