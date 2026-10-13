using Miqa.BackOfficeApp.Api;
using Miqa.BackOfficeApp.Components;
using Miqa.BackOfficeApp.Extensions;
using Miqa.Platform.Builders;
using Miqa.Platform.ServiceDefaults;

using Radzen;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddApiServiceDefaults();
builder.AddRedisOutputCache("cache");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents().AddHubOptions(options => options.MaximumReceiveMessageSize = 10 * 1024 * 1024);

builder.Services.AddControllers();

builder.Services.AddRadzenComponents();

builder.Services.AddRadzenCookieThemeService(options =>
{
    options.Name = "Miqa.BackOfficeAppTheme";
    options.Duration = TimeSpan.FromDays(365);
});

builder.AddApplicationServices();


var app = builder.Build();

var forwardingOptions = new ForwardedHeadersOptions()
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
};
forwardingOptions.KnownIPNetworks.Clear();
forwardingOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardingOptions);


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.MapControllers();
app.UseHeaderPropagation();
app.MapStaticAssets();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAuthApi();

app.MapDefaultEndpoints();

app.Run();