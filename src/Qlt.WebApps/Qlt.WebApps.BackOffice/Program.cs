using Qlt.Platform.ServiceDefaults;
using Qlt.WebApps.BackOffice.Components;
using Qlt.WebApps.BackOffice.Extensions;

using Radzen;

namespace Qlt.WebApps.BackOffice;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add service defaults & Aspire client integrations.
        builder.AddServiceDefaults();
        builder.AddRedisOutputCache("cache");

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddRadzenComponents();

        builder.AddApplicationServices();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.UseOutputCache();

        app.MapStaticAssets();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.MapDefaultEndpoints();

        app.Run();
    }
}
