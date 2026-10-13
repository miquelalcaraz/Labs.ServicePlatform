using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

namespace Platform.FunctionalTests;

public sealed class BuildingBlocksFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly IHost _app;

    public IResourceBuilder<SqlServerServerResource> SqlServer { get; private set; } = default!;
    public IResourceBuilder<SqlServerDatabaseResource> TestDb { get; private set; } = default!;
    public IResourceBuilder<AzureServiceBusResource> AzureServiceBus { get; private set; }
    public IResourceBuilder<RedisResource> RedisCache { get; private set; }

    private string _sqlConnectionString = default!;
    private string _redisConnectionString;
    private string _eventBusUri = default!;

    public BuildingBlocksFixture()
    {
        var options = new DistributedApplicationOptions
        {
            AssemblyName = typeof(BuildingBlocksFixture).Assembly.FullName,
            DisableDashboard = true
        };
        var appBuilder = DistributedApplication.CreateBuilder(options);

        var sqlPassword = appBuilder.AddParameter("sqlPassword", value: "_TestPassword123", secret: true);

        SqlServer = appBuilder.AddSqlServer("sqlserver", sqlPassword, 1446)
            .WithImageTag("latest")
            .WithLifetime(ContainerLifetime.Persistent);

        TestDb = SqlServer.AddDatabase("testDb");

        RedisCache = appBuilder.AddRedis("cache")
                                .WithLifetime(ContainerLifetime.Persistent);

        AzureServiceBus = appBuilder.AddAzureServiceBus("eventbus")
                    .RunAsEmulator(emulator => emulator
                    .WithLifetime(ContainerLifetime.Persistent));

        var eventBusTopic = AzureServiceBus.AddServiceBusTopic("sirius-event-bus");
        eventBusTopic.AddServiceBusSubscription("platform-sub", subscriptionName: "Platform");

        _app = appBuilder.Build();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { $"ConnectionStrings:{TestDb.Resource.Name}", _sqlConnectionString },
                { $"ConnectionStrings:{RedisCache.Resource.Name}", _redisConnectionString },
                { $"ConnectionStrings:{AzureServiceBus.Resource.Name}", _eventBusUri },
                { "EventBus:SubscriptionClientName", "Platform"},
                { $"Aspire:Azure:Messaging:ServiceBus:{AzureServiceBus.Resource.Name}:DisableHealthChecks", "true" },
                { "Aspire:Azure:Messaging:ServiceBus:eventbus:DisableTracing", "true" }
            }!);
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

        _sqlConnectionString = await TestDb.Resource.ConnectionStringExpression.GetValueAsync(default);
        _redisConnectionString = await RedisCache.Resource.ConnectionStringExpression.GetValueAsync(default);
        _eventBusUri = await AzureServiceBus.Resource.ConnectionStringExpression.GetValueAsync(default);
         

        Console.WriteLine($"SQL Server Connection String: {_sqlConnectionString}");
        Console.WriteLine($"Azure Service Bus URI: {_eventBusUri}");
        Console.WriteLine($"Redis Connection String: {_redisConnectionString}");
    }

    private static async Task WaitForSqlServerReadyAsync(string connectionString, int maxRetries = 30, int delayMs = 2000)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                return;
            }
            catch when (i < maxRetries - 1)
            {
                await Task.Delay(delayMs);
            }
        }
    }
}
