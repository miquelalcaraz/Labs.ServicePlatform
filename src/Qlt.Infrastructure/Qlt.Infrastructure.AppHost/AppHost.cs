
//#define USE_RABBITMQ
#define USE_AZURE_SERVICE_BUS


var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

#if USE_RABBITMQ
var eventbus = builder.AddRabbitMQ("eventbus")
    .WithLifetime(ContainerLifetime.Persistent);
#endif

#if USE_AZURE_SERVICE_BUS
var eventbus = builder.AddAzureServiceBus("eventbus")
    .RunAsEmulator(emulator => emulator
        .WithLifetime(ContainerLifetime.Persistent));

var eventBusTopic = eventbus.AddServiceBusTopic("qlt-event-bus");
eventBusTopic.AddServiceBusSubscription("accounts-sub", subscriptionName: "Accounts");
eventBusTopic.AddServiceBusSubscription("notifications-sub", subscriptionName: "Notifications");
eventBusTopic.AddServiceBusSubscription("identity-sub", subscriptionName: "Identity");
#endif

var sqlPassword = builder.AddParameter("sqlPassword", value: "***REMOVED***", secret: true);
var sqlserver = builder.AddSqlServer("sqlserver", sqlPassword, 1444)
    .WithImageTag("latest")
    .WithLifetime(ContainerLifetime.Persistent);

//Databases
var accountDb = sqlserver.AddDatabase("AccountsDb");
var identityDb = sqlserver.AddDatabase("IdentityDb");
var notificationsDb = sqlserver.AddDatabase("NotificationsDb");


var launchProfileName = ShouldUseHttpForEndpoints() ? "http" : "https";
var identity = builder.AddProject<Projects.Qlt_Infrastructure_Identity>("identity")
   .WithHttpHealthCheck("/health")
   .WithExternalHttpEndpoints()
    .WithReference(identityDb)
    .WaitFor(identityDb)
    .WithReference(eventbus).WaitFor(eventbus);

var identityEndpoint = identity.GetEndpoint(launchProfileName);

var notifications = builder.AddProject<Projects.Services_Notifications>("notifications")
   .WithHttpHealthCheck("/health")
    .WithReference(notificationsDb)
    .WaitFor(notificationsDb)
    .WithReference(eventbus).WaitFor(eventbus);

var accountApiService = builder.AddProject<Projects.Services_Accounts_API>("accounts")
    .WithHttpHealthCheck("/health")
    .WithReference(accountDb)
    .WaitFor(accountDb)
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(eventbus).WaitFor(eventbus)
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
