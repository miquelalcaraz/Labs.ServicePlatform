using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Miqa.Infrastructure.Identity.Configuration;
using Miqa.Infrastructure.Identity.Models;
using Miqa.Infrastructure.Identity.Repositories;
using Miqa.Platform.Persistence.Migrations;

namespace Miqa.Infrastructure.Identity.Infrastructure.Seed
{
    public class IdentityContextSeed : IDbSeeder<IdentityContext>
    {
        private const string DemoUsersPasswordKey = "Seed:DemoUsersPassword";

        private readonly IConfiguration _configuration;
        private readonly ILogger<IdentityContextSeed> _logger;
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
            _logger = serviceProvider.GetRequiredService<ILogger<IdentityContextSeed>>();
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
                var providers = ProviderSeeding.Get(_configuration);
                foreach (var provider in providers)
                {
                    context.IdentityProviders.Add(provider.ToEntity());
                }
                context.SaveChanges();
            }

            context.IdentityResources.RemoveRange(context.IdentityResources.ToList());
            context.SaveChanges();
            foreach (var resource in Config.GetResources())
                context.IdentityResources.Add(resource.ToEntity());
            context.SaveChanges();

            context.Clients.RemoveRange(context.Clients.ToList());
            context.SaveChanges();
            foreach (var client in Config.GetClients(_configuration))
                context.Clients.Add(client.ToEntity());
            context.SaveChanges();

            context.ApiScopes.RemoveRange(context.ApiScopes.ToList());
            context.SaveChanges();
            foreach (var scope in Config.GetApiScopes())
                context.ApiScopes.Add(scope.ToEntity());
            context.SaveChanges();

            context.ApiResources.RemoveRange(context.ApiResources.ToList());
            context.SaveChanges();
            foreach (var resource in Config.GetApis())
                context.ApiResources.Add(resource.ToEntity());
            context.SaveChanges();
        }

        private async Task SeedRolesAsync()
        {
            var roles = new[] {
                Roles.PlatformRoles.SalesRepresentative,
                Roles.PlatformRoles.ProjectManager,
                Roles.PlatformRoles.BusinessAdmin,
                Roles.PlatformRoles.SupportAgent,
                Roles.PlatformRoles.PlatformAdmin ,
                Roles.SystemRoles.User,
                Roles.SystemRoles.ApiUser,
                Roles.SystemRoles.SystemAdmin
            };
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
                var demoUsersPassword = _configuration[DemoUsersPasswordKey];
                if (string.IsNullOrWhiteSpace(demoUsersPassword))
                {
                    _logger.LogWarning("Demo users were not seeded because '{SettingKey}' is not configured", DemoUsersPasswordKey);
                    return;
                }

                Tenant tenant = UsersSeeding.GetTenant();
                await _tenantRepository.CreateAsync(tenant);

                foreach (var user in UsersSeeding.GetUsers())
                {
                    await _userManager.CreateAsync(user, demoUsersPassword);
                    await _userManager.AddToRolesAsync(user, new[] { Roles.PlatformRoles.PlatformAdmin, Roles.SystemRoles.SystemAdmin });
                }
                foreach (var user in UsersSeeding.GetApiUsers())
                {
                    await _userManager.CreateAsync(user, demoUsersPassword);
                    await _userManager.AddToRolesAsync(user, new[] { Roles.SystemRoles.ApiUser, });
                }
                foreach (var user in UsersSeeding.GetAdmins())
                {
                    await _userManager.CreateAsync(user, demoUsersPassword);
                    await _userManager.AddToRolesAsync(user, new[] { Roles.PlatformRoles.PlatformAdmin, Roles.SystemRoles.SystemAdmin });
                }
            }
        }
    }
}
