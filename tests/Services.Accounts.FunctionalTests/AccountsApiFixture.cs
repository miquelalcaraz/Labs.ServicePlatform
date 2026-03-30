using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Services.Accounts.FunctionalTests;

public sealed class AccountsApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly IHost _app;

    public IResourceBuilder<SqlServerServerResource> SqlServer { get; private set; }
    //    public IResourceBuilder<SqlServerServerResource> IdentityDB { get; private set; }

    public IResourceBuilder<SqlServerDatabaseResource> IdentityLogsDB { get; private set; }
    public IResourceBuilder<SqlServerDatabaseResource> IdentityDB { get; private set; }
    public IResourceBuilder<SqlServerDatabaseResource> AccountDb { get; private set; }
    public IResourceBuilder<RabbitMQServerResource> RabbitMQ { get; private set; }
    public IResourceBuilder<RedisResource> RedisCache { get; private set; }
    public IResourceBuilder<ProjectResource> IdentityApi { get; private set; }

    private string _sqlserverConnectionString;
    private string _redisConnectionString;
    private string _eventBusUri;

    public AccountsApiFixture()
    {

        var options = new DistributedApplicationOptions
        {
            AssemblyName = typeof(AccountsApiFixture).Assembly.FullName,
            DisableDashboard = true
        };
        var appBuilder = DistributedApplication.CreateBuilder(options);

        var sqlPassword = appBuilder.AddParameter("sqlPassword", value: "***REMOVED***", secret: true);
        SqlServer = appBuilder.AddSqlServer("sqlserver", sqlPassword, 1445)
            .WithImageTag("latest");
        //.WithLifetime(ContainerLifetime.Persistent);

        AccountDb = SqlServer.AddDatabase("accountDb");
        IdentityDB = SqlServer.AddDatabase("identityDb");
        IdentityLogsDB = SqlServer.AddDatabase("identityLogsDb");
        RedisCache = appBuilder.AddRedis("cache");
        RabbitMQ = appBuilder.AddRabbitMQ("eventbus");

        IdentityApi = appBuilder.AddProject<Projects.Qlt_Infrastructure_Identity>("identity-api")
            .WithReference(IdentityDB)
            .WaitFor(AccountDb)
            .WaitFor(IdentityDB)
            .WaitFor(IdentityLogsDB)
            .WaitFor(RedisCache)
            .WaitFor(RabbitMQ);

        _app = appBuilder.Build();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            // string eventBusUri = RabbitMQ.Resource.Host.Endpoint.Url;
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"ConnectionStrings:{AccountDb.Resource.Name}", _sqlserverConnectionString },
                { $"ConnectionStrings:{RedisCache.Resource.Name}", _redisConnectionString },
                { $"ConnectionStrings:{RabbitMQ.Resource.Name}", _eventBusUri },
                { "Identity:Url", IdentityApi.GetEndpoint("http").Url },
                { "EventBus:SubscriptionClientName", "Accounts"}
            });
        });
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter>(new AutoAuthorizeStartupFilter());
        });
        return base.CreateHost(builder);
    }

    public new async Task DisposeAsync()
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
        _sqlserverConnectionString = await AccountDb.Resource.ConnectionStringExpression.GetValueAsync(default);
        _redisConnectionString = await RedisCache.Resource.ConnectionStringExpression.GetValueAsync(default);
        _eventBusUri = await RabbitMQ.Resource.ConnectionStringExpression.GetValueAsync(default);

        Console.WriteLine($"SQL Server Connection String: {_sqlserverConnectionString}");
        Console.WriteLine($"RabbitMQ Event Bus URI: {_eventBusUri}");
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
