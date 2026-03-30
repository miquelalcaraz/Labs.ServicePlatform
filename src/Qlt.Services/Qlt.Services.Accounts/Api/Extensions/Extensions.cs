using FluentValidation;

using Qlt.Domain.Behaviors;
using Qlt.Platform.Messaging.Behaviors;
using Qlt.Platform.Persistence.Behaviors;
using Qlt.Services.Accounts.Application;
using Qlt.Services.Accounts.Application.Queries;
using Qlt.Services.Accounts.Infrastructure.Extensions;
using Qlt.Services.Accounts.Infrastructure.Persistence.Queries;


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

                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(MessagingBehavior<,>));
                cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
                cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            });

            services.AddValidatorsFromAssembly(AssemblyReference.Assembly);
            services.AddScoped<IOrganizationQueries, OrganizationQueries>();

            return services;
        }

    }
}
