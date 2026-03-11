using QltSystem.Accounts.Application.Queries;
using QltSystem.Accounts.Infrastructure.Extensions;
using QltSystem.Accounts.Infrastructure.Persistence.Queries;

namespace QltSystem.Accounts.Extensions
{
    public static class Extensions
    {

        public static IServiceCollection AddApplicationServices(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;

            builder.AddInfrastructureServices();

            // Register MediatR handlers from Application assembly
            // This ensures all command handlers (including IdentifiedCommandHandlers) are registered
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(QltSystem.Accounts.Application.AssemblyReference.Assembly);
            });

            services.AddScoped<IOrganizationQueries, OrganizationQueries>();

            return services;
        }

    }
}
