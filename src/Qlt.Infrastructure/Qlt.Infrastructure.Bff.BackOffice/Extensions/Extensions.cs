using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System;

internal static class Extensions
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpForwarderWithServiceDiscovery();

        builder.Services.AddHealthChecks()
            .AddUrlGroup(new Uri("https://accounts/health"), name: "accountsapi-check")
            .AddUrlGroup(new Uri("https://identity/health"), name: "identityapi-check");
    }
}
