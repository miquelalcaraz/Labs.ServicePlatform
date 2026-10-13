using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Sirius.Domain.Services;
using Sirius.Platform.Services;

namespace Sirius.Platform.Builders
{
    public static partial class Extensions
    {
        public static IHostApplicationBuilder AddApiServiceDefaults(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            builder.AddDefaultAuthentication();
            services.AddHttpContextAccessor();
            services.AddTransient<ICurrentUserContext, CurrentUserContext>();
            return builder;
        }
    }
}
