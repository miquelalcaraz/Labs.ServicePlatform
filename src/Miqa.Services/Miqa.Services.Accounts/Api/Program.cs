using Miqa.Platform.Builders;
using Miqa.Platform.Middleware;
using Miqa.Platform.ServiceDefaults;
using Miqa.Services.Accounts.APIs;
using Miqa.Services.Accounts.Authorization;
using Miqa.Services.Accounts.Extensions;



var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddApiServiceDefaults();
builder.AddApplicationServices();

// Add services to the container.
builder.Services.AddProblemDetails();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AccountsAuthorizationPolicies.OnboardingWrite, policy =>
        policy.RequireRole(
            AccountsAuthorizationPolicies.Roles.ProjectManager,
            AccountsAuthorizationPolicies.Roles.BusinessAdmin,
            AccountsAuthorizationPolicies.Roles.PlatformAdmin))
    .AddPolicy(AccountsAuthorizationPolicies.OnboardingRead, policy =>
        policy.RequireRole(
            AccountsAuthorizationPolicies.Roles.ProjectManager,
            AccountsAuthorizationPolicies.Roles.BusinessAdmin,
            AccountsAuthorizationPolicies.Roles.SupportAgent,
            AccountsAuthorizationPolicies.Roles.PlatformAdmin))
    .AddPolicy(AccountsAuthorizationPolicies.OnboardingRetry, policy =>
        policy.RequireRole(
            AccountsAuthorizationPolicies.Roles.ProjectManager,
            AccountsAuthorizationPolicies.Roles.SupportAgent,
            AccountsAuthorizationPolicies.Roles.PlatformAdmin))
    .AddPolicy(AccountsAuthorizationPolicies.ActivationRead, policy =>
        policy.RequireRole(
            AccountsAuthorizationPolicies.Roles.ProjectManager,
            AccountsAuthorizationPolicies.Roles.BusinessAdmin,
            AccountsAuthorizationPolicies.Roles.SupportAgent,
            AccountsAuthorizationPolicies.Roles.PlatformAdmin))
    .AddPolicy(AccountsAuthorizationPolicies.ActivationRetry, policy =>
        policy.RequireRole(
            AccountsAuthorizationPolicies.Roles.ProjectManager,
            AccountsAuthorizationPolicies.Roles.SupportAgent,
            AccountsAuthorizationPolicies.Roles.PlatformAdmin))
    .AddPolicy(AccountsAuthorizationPolicies.ActivationResend, policy =>
        policy.RequireRole(
            AccountsAuthorizationPolicies.Roles.ProjectManager,
            AccountsAuthorizationPolicies.Roles.SupportAgent,
            AccountsAuthorizationPolicies.Roles.PlatformAdmin));

var withApiVersioning = builder.Services.AddApiVersioning();

builder.AddDefaultOpenApi(withApiVersioning);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();


// Register API endpoints (available in all environments)
var accounts = app.NewVersionedApi("accounts");
accounts.MapAccountsApiV1()
     .RequireAuthorization();

var onboarding = app.NewVersionedApi("customer-onboarding");
onboarding.MapOnboardingApiV1()
     .RequireAuthorization();

ActivationApi.MapActivationApiV1(app)
     .RequireAuthorization();

// OpenAPI/Swagger UI only in Development
if (app.Environment.IsDevelopment())
{
    app.UseDefaultOpenApi();
}

app.MapDefaultEndpoints();

app.Run();

// Make the implicit Program class public so test projects can access it
namespace Miqa.Services.Accounts
{
    public partial class Program { }
}

