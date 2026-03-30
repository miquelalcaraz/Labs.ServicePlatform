



using Duende.IdentityServer.Models;

namespace Qlt.Infrastructure.Identity.Models
{
    public class TenantProvider
    {

        public string SchemaProvider { get; protected set; }


        public Guid TenantId { get; protected set; }
        public Tenant Tenant { get; protected set; }

        public bool Enable { get; protected set; } = true;


        protected TenantProvider()
        {

        }

        protected TenantProvider(Tenant tenant, IdentityProvider identityProvider)
        {
            Tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));
            if (identityProvider is null)
                throw new ArgumentNullException(nameof(identityProvider));
            SchemaProvider = identityProvider.Scheme;
        }

        public static TenantProvider Create(Tenant tenant, IdentityProvider provider)
        {
            return new TenantProvider(tenant, provider);
        }

        public TenantProvider EnableProvider()
        {
            Enable = true;
            return this;
        }
        public TenantProvider DisableProvider()
        {
            Enable = false;
            return this;
        }
    }
}
