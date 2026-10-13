using Miqa.Platform.Builders;
using Miqa.Platform.Middleware;
using Miqa.Platform.ServiceDefaults;
using Miqa.Services.Notifications.APIs;
using Miqa.Services.Notifications.Extensions;



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
var Notifications = app.NewVersionedApi("Notifications");
Notifications.MapNotificationsApiV1()
     .RequireAuthorization();


// OpenAPI/Swagger UI only in Development
if (app.Environment.IsDevelopment())
{
    app.UseDefaultOpenApi();
}

app.MapDefaultEndpoints();

app.Run();

// Make the implicit Program class public so test projects can access it
namespace Miqa.Services.Notifications
{
    public partial class Program { }
}

