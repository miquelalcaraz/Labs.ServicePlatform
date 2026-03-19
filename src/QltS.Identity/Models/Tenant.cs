using Duende.IdentityServer.Models;

namespace QltS.Identity.Models
{
    public class Tenant
    {
        public Guid Id { get; private set; }
        public Guid OrganizationId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }

        private readonly List<ApplicationUser> _users = new();
        public IReadOnlyCollection<ApplicationUser> Users => _users;

        private readonly List<TenantProvider> _tenantProviders = new();
        public IReadOnlyCollection<TenantProvider> TenantProviders => _tenantProviders;

        internal Tenant() { }

        protected Tenant(Guid? id)
        {
            Id = id ?? Guid.NewGuid();
        }

        public static Tenant Create(Guid? id, Guid organizationId, string name)
        {
            return new Tenant(id)
            {
                OrganizationId = organizationId,
                Name = name,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void AddUser(ApplicationUser user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (user.TenantId != Id) throw new InvalidOperationException("User's tenant ID does not match.");

            _users.Add(user);
        }

        public void AddProvider(IdentityProvider identityProvider)
        {
            if (identityProvider is null)
                throw new ArgumentNullException(nameof(identityProvider));

            if (_tenantProviders.Any(tp => tp.SchemaProvider == identityProvider.Scheme))
                throw new InvalidOperationException("This Provider is already associated with the tenant.");

            _tenantProviders.Add(TenantProvider.Create(this, identityProvider));
        }


        public void RemoveProvider(IdentityProvider identityProvider)
        {
            var tenantProvider = _tenantProviders.FirstOrDefault(tp => tp.SchemaProvider == identityProvider.Scheme);
            if (tenantProvider is not null)
            {
                _tenantProviders.Remove(tenantProvider);
            }
        }

    }
}
