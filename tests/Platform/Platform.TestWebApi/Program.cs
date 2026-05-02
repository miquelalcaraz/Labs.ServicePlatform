using Platform.TestWebApi;

using Qlt.Platform.Builders;
using Qlt.Platform.Middleware;
using Qlt.Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddApiServiceDefaults();
builder.AddApplicationServices();

builder.Services.AddProblemDetails();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();



var app = builder.Build();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapDefaultEndpoints();
app.MapControllers();



app.Run();

public partial class Program { }
