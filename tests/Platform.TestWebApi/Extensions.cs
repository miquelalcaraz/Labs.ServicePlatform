using Microsoft.EntityFrameworkCore;

using Platform.TestWebApi.Infrastructure;

using Qlt.Domain.Behaviors;
using Qlt.Platform.EventBus;
using Qlt.Platform.EventBus.Abstractions;
using Qlt.Platform.Messaging.Behaviors;
using Qlt.Platform.Messaging.Extensions;
using Qlt.Platform.Persistence.Behaviors;
using Qlt.Platform.Persistence.Extensions;
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

        // Add telemetry decorators for both integration and domain events
        services.AddIntegrationEventTelemetryDecorator();
        services.AddDomainEventTelemetryDecorator();

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
            cfg.AddOpenBehavior(typeof(MessagingBehavior<,>));
            cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
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
