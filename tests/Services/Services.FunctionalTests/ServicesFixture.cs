using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Services.FunctionalTests;

public abstract class ServicesFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly IHost _app;

    public IResourceBuilder<SqlServerServerResource> SqlServer { get; private set; }
    //    public IResourceBuilder<SqlServerServerResource> IdentityDB { get; private set; }

    public IResourceBuilder<SqlServerDatabaseResource> IdentityDB { get; private set; }
    public IResourceBuilder<SqlServerDatabaseResource> SiriusDb { get; private set; }
    public IResourceBuilder<AzureServiceBusResource> AzureServiceBus { get; private set; }
    public IResourceBuilder<RedisResource> RedisCache { get; private set; }
    public IResourceBuilder<ProjectResource> IdentityApi { get; private set; }

    private string _sqlserverConnectionString;
    private string _redisConnectionString;
    private string _eventBusUri;

    public ServicesFixture()
    {

        var options = new DistributedApplicationOptions
        {
            AssemblyName = typeof(ServicesFixture).Assembly.FullName,
            DisableDashboard = true
        };
        var appBuilder = DistributedApplication.CreateBuilder(options);

        var sqlPassword = appBuilder.AddParameter("sqlPassword", value: "_TestPassword123", secret: true);
        SqlServer = appBuilder.AddSqlServer("sqlserver", sqlPassword, 1445)
            .WithImageTag("latest")
            .WithLifetime(ContainerLifetime.Persistent);

        SiriusDb = SqlServer.AddDatabase("SiriusDb");
        IdentityDB = SqlServer.AddDatabase("IdentityDb");

        RedisCache = appBuilder.AddRedis("cache")
            .WithLifetime(ContainerLifetime.Persistent);

        AzureServiceBus = appBuilder.AddAzureServiceBus("eventbus")
                    .RunAsEmulator(emulator => emulator
                    .WithLifetime(ContainerLifetime.Persistent));

        var eventBusTopic = AzureServiceBus.AddServiceBusTopic("sirius-event-bus");
        eventBusTopic.AddServiceBusSubscription("accounts-sub", subscriptionName: "Accounts");
        eventBusTopic.AddServiceBusSubscription("notifications-sub", subscriptionName: "Notifications");
        eventBusTopic.AddServiceBusSubscription("identity-sub", subscriptionName: "Identity");

        IdentityApi = appBuilder.AddProject<Projects.Infrastructure_Identity>("identity-api")
            .WithReference(IdentityDB)
            .WithReference(RedisCache)
            .WithReference(AzureServiceBus)
            .WaitFor(SiriusDb)
            .WaitFor(IdentityDB)
            .WaitFor(RedisCache)
            .WaitFor(AzureServiceBus)
            .WithEnvironment("Aspire__Azure__Messaging__ServiceBus__eventbus__DisableHealthChecks", "true")
            .WithEnvironment("Aspire__Azure__Messaging__ServiceBus__eventbus__DisableTracing", "true");

        _app = appBuilder.Build();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"ConnectionStrings:{SiriusDb.Resource.Name}", _sqlserverConnectionString },
                { $"ConnectionStrings:{RedisCache.Resource.Name}", _redisConnectionString },
                { $"ConnectionStrings:{AzureServiceBus.Resource.Name}", _eventBusUri },
                { "Identity:Url", IdentityApi.GetEndpoint("http").Url },
                { "EventBus:SubscriptionClientName", "Accounts"},
                { $"Aspire:Azure:Messaging:ServiceBus:{AzureServiceBus.Resource.Name}:DisableHealthChecks", "true" },
                { "Aspire:Azure:Messaging:ServiceBus:eventbus:DisableTracing", "true" }
            });
        });
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter>(new AutoAuthorizeStartupFilter());
        });
        return base.CreateHost(builder);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _app.StopAsync();
        if (_app is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _app.Dispose();
        }
    }

    public async ValueTask InitializeAsync()
    {
        await _app.StartAsync();
        _sqlserverConnectionString = await SiriusDb.Resource.ConnectionStringExpression.GetValueAsync(default);
        _redisConnectionString = await RedisCache.Resource.ConnectionStringExpression.GetValueAsync(default);
        _eventBusUri = await AzureServiceBus.Resource.ConnectionStringExpression.GetValueAsync(default);

        Console.WriteLine($"SQL Server Connection String: {_sqlserverConnectionString}");
        Console.WriteLine($"Azure Service Bus URI: {_eventBusUri}");
        Console.WriteLine($"Redis Connection String: {_redisConnectionString}");
    }

    private class AutoAuthorizeStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                builder.UseMiddleware<AutoAuthorizeMiddleware>();
                next(builder);
            };
        }
    }
}
