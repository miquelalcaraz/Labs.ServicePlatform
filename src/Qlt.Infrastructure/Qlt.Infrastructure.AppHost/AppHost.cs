

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var rabbitMq = builder.AddRabbitMQ("eventbus")
    .WithLifetime(ContainerLifetime.Persistent);

var sqlPassword = builder.AddParameter("sqlPassword", value: "***REMOVED***", secret: true);
var sqlserver = builder.AddSqlServer("sqlserver", sqlPassword, 1444)
    .WithImageTag("latest")
    .WithLifetime(ContainerLifetime.Persistent);

//Databases
var accountDb = sqlserver.AddDatabase("accountDb");
var identityLogsDb = sqlserver.AddDatabase("identityLogsDb");
var identityDb = sqlserver.AddDatabase("identityDb");
var notificationsDb = sqlserver.AddDatabase("notificationsDb");


var launchProfileName = ShouldUseHttpForEndpoints() ? "http" : "https";
var identity = builder.AddProject<Projects.Qlt_Infrastructure_Identity>("identity")
   .WithHttpHealthCheck("/health")
   .WithExternalHttpEndpoints()
    .WithReference(identityDb)
    .WaitFor(identityDb)
    .WithReference(identityLogsDb)
    .WaitFor(identityLogsDb)
    .WithReference(rabbitMq).WaitFor(rabbitMq);

var identityEndpoint = identity.GetEndpoint(launchProfileName);

var notifications = builder.AddProject<Projects.Services_Notifications>("notifications")
   .WithHttpHealthCheck("/health")
    .WithReference(notificationsDb)
    .WaitFor(notificationsDb)
    .WithReference(rabbitMq).WaitFor(rabbitMq);

var accountApiService = builder.AddProject<Projects.Services_Accounts_API>("accounts")
    .WithHttpHealthCheck("/health")
    .WithReference(accountDb)
    .WaitFor(accountDb)
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WaitFor(identity)
    .WithEnvironment("Identity__Url", identityEndpoint);

var webApp = builder.AddProject<Projects.Qlt_WebApps_BackOffice>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(accountApiService)
    .WaitFor(accountApiService)
    .WaitFor(identity)
    .WithEnvironment("IdentityUrl", identityEndpoint);

// Wire up the callback urls (self referencing)
webApp.WithEnvironment("CallBackUrl", webApp.GetEndpoint(launchProfileName));

// Identity has a reference to all of the apps for callback urls, this is a cyclic reference
identity.WithEnvironment("WebAppClient", webApp.GetEndpoint(launchProfileName))
    .WithEnvironment("AccountApiClient", accountApiService.GetEndpoint("http"));

builder.Build().Run();


// For test use only.
// Looks for an environment variable that forces the use of HTTP for all the endpoints. We
// are doing this for ease of running the Playwright tests in CI.
static bool ShouldUseHttpForEndpoints()
{
    const string EnvVarName = "QLT_USE_HTTP_ENDPOINTS";
    var envValue = Environment.GetEnvironmentVariable(EnvVarName);

    // Attempt to parse the environment variable value; return true if it's exactly "1".
    return int.TryParse(envValue, out int result) && result == 1;
}
