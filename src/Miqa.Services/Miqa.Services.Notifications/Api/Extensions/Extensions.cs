using FluentValidation;

using Miqa.Domain.Behaviors;
using Miqa.Platform.Messaging.Builders;
using Miqa.Services.Notifications.Application;
using Miqa.Services.Notifications.Infrastructure.Extensions;


namespace Miqa.Services.Notifications.Extensions
{
    public static class Extensions
    {

        public static IServiceCollection AddApplicationServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            builder.AddInfrastructureServices();
            builder.AddRedisClient("cache");

            services.AddHttpContextAccessor();

            services.AddMiqaMediator(cfg =>
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
