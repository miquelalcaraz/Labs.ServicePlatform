using FluentValidation;

using Sirius.Domain.Behaviors;
using Sirius.Platform.Messaging.Builders;
using Sirius.Services.Notifications.Application;
using Sirius.Services.Notifications.Infrastructure.Extensions;


namespace Sirius.Services.Notifications.Extensions
{
    public static class Extensions
    {

        public static IServiceCollection AddApplicationServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            builder.AddInfrastructureServices();
            builder.AddRedisClient("cache");

            services.AddHttpContextAccessor();

            services.AddSiriusMediator(cfg =>
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
