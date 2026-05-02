using FluentValidation;

using Qlt.Domain.Behaviors;
using Qlt.Platform.Messaging.Builders;
using Qlt.Services.Notifications.Application;
using Qlt.Services.Notifications.Infrastructure.Extensions;


namespace Qlt.Services.Notifications.Extensions
{
    public static class Extensions
    {

        public static IServiceCollection AddApplicationServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            builder.AddInfrastructureServices();
            builder.AddRedisClient("cache");

            services.AddHttpContextAccessor();

            services.AddQltMediator(cfg =>
            {
                cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly);
                cfg.RegisterServicesFromAssembly(typeof(Infrastructure.AssemblyReference).Assembly);
                cfg.WithLogging(typeof(LoggingBehavior<,>));
                cfg.WithValidation(typeof(ValidationBehavior<,>));
            });

            services.AddValidatorsFromAssembly(AssemblyReference.Assembly);

            return services;
        }

    }
}
