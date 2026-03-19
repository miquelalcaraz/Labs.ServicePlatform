using FluentValidation;

using QltS.Accounts.Application.Queries;
using QltS.Accounts.Infrastructure.Extensions;
using QltS.Accounts.Infrastructure.Persistence.Queries;
using QltS.Core.Application.Behaviors;
using QltS.Core.EventBus.TransactionalOutbox.Extensions;

namespace QltS.Accounts.Extensions
{
    public static class Extensions
    {

        public static IServiceCollection AddApplicationServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            builder.AddInfrastructureServices();

            services.AddHttpContextAccessor();

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(Application.AssemblyReference.Assembly);
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
                cfg.AddTransactionalOutboxBehavior();
            });

            services.AddValidatorsFromAssembly(Application.AssemblyReference.Assembly);
            services.AddScoped<IOrganizationQueries, OrganizationQueries>();

            return services;
        }

    }
}
