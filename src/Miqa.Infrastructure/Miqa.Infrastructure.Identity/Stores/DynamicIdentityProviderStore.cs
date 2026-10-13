using Duende.IdentityServer.EntityFramework.Interfaces;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Miqa.Infrastructure.Identity.Infrastructure;
using Miqa.Infrastructure.Identity.Models;


namespace Miqa.Infrastructure.Identity.Stores
{
    public class DynamicIdentityProviderStore : IIdentityProviderStore
    {
        private readonly IdentityContext _applicationDbContext;
        private IdentityProviderStore _identityProviderStore;
        private readonly UserManager<ApplicationUser> _userManager;

        public DynamicIdentityProviderStore(
            IdentityContext applicationDbContext,
            IConfigurationDbContext context,
            ILogger<IdentityProviderStore> logger,
            IIdentityProviderFactory identityProviderFactory,
             UserManager<ApplicationUser> userManager
            )

        {
            _applicationDbContext = applicationDbContext ?? throw new ArgumentNullException(nameof(applicationDbContext));
            _identityProviderStore = new IdentityProviderStore(context, logger, identityProviderFactory);
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));

        }

        public Task<IReadOnlyCollection<IdentityProviderName>> GetAllSchemeNamesAsync(CancellationToken cancellationToken = default)
        {
            return _identityProviderStore.GetAllSchemeNamesAsync(cancellationToken);
        }

        public Task<IdentityProvider> GetBySchemeAsync(string scheme, CancellationToken cancellationToken = default)
        {

            return _identityProviderStore.GetBySchemeAsync(scheme, cancellationToken);
        }
        public async Task<IEnumerable<IdentityProviderName>> GetSchemeNamesByOrganizationAsync(Guid? organizationId)
        {
            if (organizationId is null)
                return new List<IdentityProviderName>();

            List<TenantProvider> organizationProviders = await _applicationDbContext.Tenants.Where(c => c.Id == organizationId.Value)
                 .Include(c => c.TenantProviders)
                 .SelectMany(c => c.TenantProviders)
                 .ToListAsync();

            var schemasNames = organizationProviders.Select(cp => cp.SchemaProvider).ToList();

            var dynamicSchemes = (await GetAllSchemeNamesAsync())
                .Where(x => schemasNames.Contains(x.Scheme));

            return dynamicSchemes;

        }
        public async Task<IEnumerable<IdentityProviderName>> GetSchemeNamesByUserAsync(string userName)
        {
            ApplicationUser user = await _userManager.FindByNameAsync(userName);
            if (user is null)
            {
                return new List<IdentityProviderName>();
            }

            List<TenantProvider> organizationProviders = await _applicationDbContext.Tenants.Where(c => c.Id == user.TenantId)
                 .Include(c => c.TenantProviders)
                 .SelectMany(c => c.TenantProviders)
                 .ToListAsync();

            var schemasNames = organizationProviders.Select(cp => cp.SchemaProvider).ToList();

            var dynamicSchemes = (await GetAllSchemeNamesAsync())
                .Where(x => schemasNames.Contains(x.Scheme));

            return dynamicSchemes;

        }
        /*
        public async Task<IEnumerable<IdentityProviderName>> GetSchemeNamesByUserAsync(string userName)
        {
            ApplicationUser user = await _applicationDbContext.Users.Where(u => u.UserName == userName)
                  .Include(u => u.Organization).ThenInclude(c => c.OrganizationProviders)
                  .FirstOrDefaultAsync();
            List<string> schemasNames = user.GetIdentityProviderSchemasNames().ToList();

            var dynamicSchemes = (await GetAllSchemeNamesAsync())
                .Where(x => schemasNames.Contains(x.Scheme));

            return dynamicSchemes;

        }
        */
        public async Task<Tenant> GetUserOrganizationAsync(string schema)
        {

            TenantProvider? tenantProvider = await _applicationDbContext.TenantProviders.Where(c => c.SchemaProvider == schema)
                  .Include(u => u.Tenant)
                  .SingleOrDefaultAsync();

            return tenantProvider?.Tenant!;

        }
    }
}
