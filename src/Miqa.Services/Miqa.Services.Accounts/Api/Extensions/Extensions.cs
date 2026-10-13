using FluentValidation;

using Miqa.Domain.Behaviors;
using Miqa.Platform.Messaging.Builders;
using Miqa.Services.Accounts.Application;
using Miqa.Services.Accounts.Infrastructure.Extensions;


namespace Miqa.Services.Accounts.Extensions
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
