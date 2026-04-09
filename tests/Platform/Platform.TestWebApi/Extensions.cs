using Microsoft.EntityFrameworkCore;

using Platform.TestWebApi.Infrastructure;

using Qlt.Domain.Behaviors;
using Qlt.Platform.EventBus;
using Qlt.Platform.EventBus.Builders;
using Qlt.Platform.Messaging.Builders;
using Qlt.Platform.Persistence.Builders;
using Qlt.Platform.Persistence.Migrations;

using System.Reflection;

namespace Platform.TestWebApi;

public static class Extensions
{
    public static WebApplicationBuilder AddTestInfrastructure(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        var connectionString = configuration.GetConnectionString("testDb")
            ?? throw new InvalidOperationException("Connection string 'testDb' not found");

        services.AddQltSDBContext<TestDbContext>(
            conf => conf
                .SetDefaultSchema("test")
                .RegisterModelsFromAssembly(Assembly.GetExecutingAssembly()),
            options => options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
            })
        );
        services.AddMigration<TestDbContext, TestDbContextSeed>();

        builder.AddRabbitMqEventBus("eventbus")
            .AddEventBusSubscriptions();

        services.AddTransactionalOutbox<TestDbContext>(options =>
        {
            options.RegisterEventTypes(typeof(AssemblyReference).Assembly);
        });

        services.AddIdempotencyEntityFramework<TestDbContext>()
            .ForIntegrationEventHandlers();

        services.AddRepositories();

        services.AddQltMediator(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.WithLogging(typeof(LoggingBehavior<,>));
            cfg.WithValidation(typeof(ValidationBehavior<,>));
        });

        services.AddQueryRepositories();

        return builder;
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
