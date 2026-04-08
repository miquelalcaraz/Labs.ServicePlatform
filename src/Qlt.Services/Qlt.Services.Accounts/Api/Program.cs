using Qlt.Platform.Builders;
using Qlt.Platform.Middleware;
using Qlt.Platform.ServiceDefaults;
using Qlt.Services.Accounts.Api;
using Qlt.Services.Accounts.Extensions;



var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddApiServiceDefaults();
builder.AddApplicationServices();

// Add services to the container.
builder.Services.AddProblemDetails();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(BackOfficeAuthorizationPolicies.OnboardingWrite, policy =>
        policy.RequireRole(
            BackOfficeAuthorizationPolicies.Roles.ProjectManager,
            BackOfficeAuthorizationPolicies.Roles.BusinessAdmin,
            BackOfficeAuthorizationPolicies.Roles.PlatformAdmin))
    .AddPolicy(BackOfficeAuthorizationPolicies.OnboardingRead, policy =>
        policy.RequireRole(
            BackOfficeAuthorizationPolicies.Roles.ProjectManager,
            BackOfficeAuthorizationPolicies.Roles.BusinessAdmin,
            BackOfficeAuthorizationPolicies.Roles.SupportAgent,
            BackOfficeAuthorizationPolicies.Roles.PlatformAdmin))
    .AddPolicy(BackOfficeAuthorizationPolicies.OnboardingRetry, policy =>
        policy.RequireRole(
            BackOfficeAuthorizationPolicies.Roles.ProjectManager,
            BackOfficeAuthorizationPolicies.Roles.SupportAgent,
            BackOfficeAuthorizationPolicies.Roles.PlatformAdmin));

var withApiVersioning = builder.Services.AddApiVersioning();

builder.AddDefaultOpenApi(withApiVersioning);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();


// Register API endpoints (available in all environments)
var accounts = app.NewVersionedApi("accounts");
accounts.MapAccountsApiV1()
     .RequireAuthorization();

var subscriptions = app.NewVersionedApi("subscriptions");
subscriptions.MapSubscriptionsApiV1()
     .RequireAuthorization();

var backofficeOnboarding = app.NewVersionedApi("backoffice-onboarding");
backofficeOnboarding.MapBackOfficeOnboardingApiV1()
     .RequireAuthorization();

// OpenAPI/Swagger UI only in Development
if (app.Environment.IsDevelopment())
{
    app.UseDefaultOpenApi();
}

app.MapDefaultEndpoints();

app.Run();

// Make the implicit Program class public so test projects can access it
namespace Qlt.Services.Accounts
{
    public partial class Program { }
}

