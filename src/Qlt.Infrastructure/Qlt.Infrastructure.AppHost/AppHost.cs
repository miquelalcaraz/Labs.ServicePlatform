
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

var notifications = builder.AddProject<Projects.Services_Notifications>("notifications")
   .WithHttpHealthCheck("/health")
   .WaitFor(identity)
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
    .WaitFor(identity);

var webApp = builder.AddProject<Projects.Qlt_WebApps_BackOffice>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(accountApiService)
    .WaitFor(accountApiService)
    .WaitFor(identity);



var backofficeBff = builder.AddProject<Projects.Qlt_Infrastructure_Bff_BackOffice>("backoffice-bff")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(accountApiService)
    .WaitFor(accountApiService)
    .WithReference(identity)
    .WaitFor(identity);

var backOfficeApp = builder.AddProject<Projects.Qlt_BackOfficeApp>("BackOfficeApp")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(backofficeBff)
    .WaitFor(backofficeBff)
    .WaitFor(identity);

var bffEndpoint = backofficeBff.GetEndpoint(launchProfileName);
var identityEndpoint = identity.GetEndpoint(launchProfileName);

// Wire up Identity URLs through the BFF
accountApiService.WithEnvironment("Identity__Url", bffEndpoint);
webApp.WithEnvironment("IdentityUrl", bffEndpoint);
backOfficeApp.WithEnvironment("Identity__Url", bffEndpoint);
backOfficeApp.WithEnvironment("Identity__Authority", identityEndpoint);
identity.WithEnvironment("IssuerUri", bffEndpoint);

// Wire up the callback urls (self referencing)
webApp.WithEnvironment("CallBackUrl", webApp.GetEndpoint(launchProfileName));
backOfficeApp.WithEnvironment("CallBackUrl", backOfficeApp.GetEndpoint(launchProfileName));

// Identity has a reference to all of the apps for callback urls, this is a cyclic reference
identity.WithEnvironment("WebAppClient", webApp.GetEndpoint(launchProfileName))
    .WithEnvironment("AccountApiClient", accountApiService.GetEndpoint(launchProfileName))
    .WithEnvironment("BackOfficeClient", backOfficeApp.GetEndpoint(launchProfileName));

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
