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

var withApiVersioning = builder.Services.AddApiVersioning();

builder.AddDefaultOpenApi(withApiVersioning);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();


// Register API endpoints (available in all environments)
var accounts = app.NewVersionedApi("accounts");
accounts.MapAccountsApiV1()
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

