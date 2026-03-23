using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using QltS.Core.Application.Services;
using QltS.Core.Infrastructure.Services;
using QltS.Core.ServiceDefaults;

namespace QltS.Core.API.Extensions
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
