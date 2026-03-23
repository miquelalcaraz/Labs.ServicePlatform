using QltS.Accounts.Api;
using QltS.Accounts.Extensions;
using QltS.Core.API.Extensions;
using QltS.Core.API.Middleware;
using QltS.Core.ServiceDefaults;


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

