using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using QltSystem.Core.Persistence.EF.Extensions;
using QltSystem.Identity.Configuration;
using QltSystem.Identity.Models;
using QltSystem.Identity.Repositories;

namespace QltSystem.Identity.Infrastructure.Seed
{
    public class IdentityContextSeed : IDbSeeder<IdentityContext>
    {
        private readonly IConfiguration _configuration;
        private readonly ITenantRepository _tenantRepository;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ConfigurationDbContext _configurationDbContext;
        private readonly PersistedGrantDbContext _persistedGrantDbContext;

        public IdentityContextSeed(IServiceProvider serviceProvider)
        {
            _configurationDbContext = serviceProvider.GetRequiredService<ConfigurationDbContext>();
            _persistedGrantDbContext = serviceProvider.GetRequiredService<PersistedGrantDbContext>();

            _configurationDbContext.Database.Migrate();
            _persistedGrantDbContext.Database.Migrate();

            _configuration = serviceProvider.GetRequiredService<IConfiguration>();
            _roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            _userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            _tenantRepository = serviceProvider.GetRequiredService<ITenantRepository>();

        }

        public async Task SeedAsync(IdentityContext context)
        {
            await SeedConfigurationAsync();
            await SeedRolesAsync();
            await SeedUsersAsync();
        }

        private async Task SeedConfigurationAsync()
        {
            var context = _configurationDbContext;

            if (!context.IdentityProviders.Any())
            {
                var providers = ProviderSeeding.Get();
                foreach (var provider in providers)
                {
                    context.IdentityProviders.Add(provider.ToEntity());
                }
                context.SaveChanges();
            }
            if (!context.IdentityResources.Any())
            {
                foreach (var resource in Config.GetResources())
                {
                    context.IdentityResources.Add(resource.ToEntity());
                }
                context.SaveChanges();
            }
            if (!context.Clients.Any())
            {
                var clients = Config.GetClients(_configuration);
                foreach (var client in clients)
                {
                    context.Clients.Add(client.ToEntity());
                }
                context.SaveChanges();
            }
            if (!context.ApiScopes.Any())
            {
                foreach (var scope in Config.GetApiScopes())
                {
                    context.ApiScopes.Add(scope.ToEntity());
                }
                context.SaveChanges();
            }
            if (!context.ApiResources.Any())
            {
                foreach (var resource in Config.GetApis())
                {
                    context.ApiResources.Add(resource.ToEntity());
                }
                context.SaveChanges();
            }
        }

        private async Task SeedRolesAsync()
        {
            var roles = new[] { "Admin", "User", "Manager", "ApiUser" };
            foreach (var role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }
        private async Task SeedUsersAsync()
        {
            if (!_userManager.Users.Any())
            {
                Tenant tenant = UsersSeeding.GetTenant();
                await _tenantRepository.CreateAsync(tenant);

                foreach (var user in UsersSeeding.GetUsers())
                {
                    await _userManager.CreateAsync(user, "***REMOVED***");
                    await _userManager.AddToRolesAsync(user, new[] { "User" });
                }
                foreach (var user in UsersSeeding.GetApiUsers())
                {
                    await _userManager.CreateAsync(user, "***REMOVED***");
                    await _userManager.AddToRolesAsync(user, new[] { "ApiUser" });
                }
                foreach (var user in UsersSeeding.GetAdmins())
                {
                    await _userManager.CreateAsync(user, "***REMOVED***");
                    await _userManager.AddToRolesAsync(user, new[] { "Admin" });
                }
            }
        }
    }
}
