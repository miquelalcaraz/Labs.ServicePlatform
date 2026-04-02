using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Qlt.Domain.Services;
using Qlt.Platform.Services;

namespace Qlt.Platform.Builders
{
    public static partial class Extensions
    {
        public static IHostApplicationBuilder AddApiServiceDefaults(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            builder.AddDefaultAuthentication();
            services.AddHttpContextAccessor();
            services.AddTransient<IUserHttpContext, UserHttpContext>();
            return builder;
        }
    }
}
