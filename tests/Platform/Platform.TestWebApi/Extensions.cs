using FluentValidation;

using Microsoft.EntityFrameworkCore;

using Platform.TestWebApi.Infrastructure;

using Miqa.Domain.Behaviors;
using Miqa.Platform.EventBus;
using Miqa.Platform.EventBus.Builders;
using Miqa.Platform.Messaging.Builders;
using Miqa.Platform.Persistence.Builders;
using Miqa.Platform.Persistence.Migrations;

using System.Diagnostics.Contracts;
using System.Reflection;

namespace Platform.TestWebApi;

public static class Extensions
{
    public static IServiceCollection AddApplicationServices(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        builder.AddInfrastructureServices();
        builder.AddRedisClient("cache");

        services.AddHttpContextAccessor();

        services.AddMiqaMediator(cfg =>
        {
            cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly);
            cfg.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly);
            cfg.WithLogging(typeof(LoggingBehavior<,>));
            cfg.WithValidation(typeof(ValidationBehavior<,>));
        });

        //services.AddValidatorsFromAssembly(AssemblyReference.Assembly);

        return services;
    }

    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        var connectionString = builder.Configuration.GetConnectionString("TestDb");
        var readConnectionString = builder.Configuration.GetConnectionString("TestDbReadonly")
            ?? connectionString;

        services.AddMiqaSDBContext<TestDbContext>(
            conf => conf
                .SetDefaultSchema("test")
                .RegisterModelsFromAssembly(Assembly.GetExecutingAssembly()),
            options => options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
            })
        );

        services.AddMiqaReadDbContext<TestReadDbContext>(
                conf => conf
                    .SetDefaultSchema("test")
                    .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
                options => options.UseSqlServer(readConnectionString)
            );


        services.AddMigration<TestDbContext, TestDbContextSeed>();

        builder.AddServiceBusEventBus("eventbus")
            .AddEventBusSubscriptions();

        services.AddRedisIdempotencyCache(builder.Configuration)
               .ForApiRequests()
            .ForIntegrationEventHandlers();

        services.AddIdempotencyEntityFramework<TestDbContext>()
            .ForApiRequests()
            .ForIntegrationEventHandlers();

        services.AddTransactionalOutbox<TestDbContext>(options =>
        {
            options.RegisterEventTypes(
                typeof( AssemblyReference).Assembly);
        });

        services.AddQueryRepositories();
    }
    private static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
    {

    }

    private static void AddRepositories(this IServiceCollection services)
    {

    }

    private static void AddQueryRepositories(this IServiceCollection services)
    {

    }
}
