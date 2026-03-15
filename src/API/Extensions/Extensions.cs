using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using QltSystem.Core.API.Services;
using QltSystem.Core.ServiceDefaults;

namespace QltSystem.Core.API.Extensions
{
    public static partial class Extensions
    {
        public static IHostApplicationBuilder AddApiServiceDefaults(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            builder.AddDefaultAuthentication();
            services.AddHttpContextAccessor();
            services.AddTransient<IIdentityService, IdentityService>();
            return builder;
        }
    }
}
