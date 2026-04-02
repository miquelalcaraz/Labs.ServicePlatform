using FluentValidation;

using Qlt.Domain.Behaviors;
using Qlt.Platform.Messaging.Behaviors;
using Qlt.Services.Accounts.Application;
using Qlt.Services.Accounts.Infrastructure.Extensions;


namespace Qlt.Services.Accounts.Extensions
{
    public static class Extensions
    {

        public static IServiceCollection AddApplicationServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            builder.AddInfrastructureServices();
            builder.AddRedisClient("cache");

            services.AddHttpContextAccessor();



            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly);
                cfg.RegisterServicesFromAssembly(typeof(Infrastructure.AssemblyReference).Assembly);
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
                cfg.AddOpenBehavior(typeof(IntegrationEventProcessorBehavior<,>));
                cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
                cfg.AddOpenBehavior(typeof(DomainEventProcessorBehavior<,>));
            });

            services.AddValidatorsFromAssembly(AssemblyReference.Assembly);

            return services;
        }

    }
}
