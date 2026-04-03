using System.Reflection;

using Microsoft.EntityFrameworkCore;

using Platform.TestWebApi.Infrastructure;

using Qlt.Domain.Behaviors;
using Qlt.Platform.EventBus;
using Qlt.Platform.EventBus.Builders;
using Qlt.Platform.Messaging.Behaviors;
using Qlt.Platform.Messaging.Builders;
using Qlt.Platform.Persistence.Builders;
using Qlt.Platform.Persistence.Migrations;

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
                .WithAuditLogs()
                .RegisterModelsFromAssembly(Assembly.GetExecutingAssembly()),
            options => options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
            })
        );
        services.AddMigration<TestDbContext, TestDbContextSeed>();

        builder.AddRabbitMqEventBus("eventbus")
            .AddEventBusSubscriptions();

        services.AddIntegrationEventTelemetryDecorator();
        services.AddDomainEventTelemetryBehavior();
        services.AddCommandTelemetryBehavior();
        services.AddQueryTelemetryBehavior();

        services.AddTransactionalOutbox<TestDbContext>(options =>
        {
            options.RegisterEventTypes(typeof(AssemblyReference).Assembly);
        });

        services.AddIdempotencyEntityFramework<TestDbContext>()
            .ForIntegrationEventHandlers();

        services.AddRepositories();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
            cfg.AddOpenBehavior(typeof(DomainEventProcessorBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });




        // services.AddValidatorsFromAssembly(AssemblyReference.Assembly);
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
