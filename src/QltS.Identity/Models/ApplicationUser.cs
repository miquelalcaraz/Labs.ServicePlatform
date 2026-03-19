using Microsoft.AspNetCore.Identity;

namespace QltS.Identity.Models
{
    public class ApplicationUser : IdentityUser
    {

        public Guid TenantId { get; set; }

        public DateTime CreatedAt { get; set; }

        public Tenant? Tenant { get; protected set; }

        internal ApplicationUser()
        {

        }
        protected ApplicationUser(Guid? id)
        {
            Id = id?.ToString() ?? Guid.NewGuid().ToString();
        }

        public static ApplicationUser CreateAsUnConfirmed(string userName, string email, Guid tenantId, Guid? id = null)
        {
            return new ApplicationUser(id)
            {
                UserName = userName,
                Email = email,
                TenantId = tenantId,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static ApplicationUser Create(string userName, string email, Tenant tenant, Guid? id = null)
        {
            return new ApplicationUser(id)
            {
                UserName = userName,
                Email = email,
                Tenant = tenant,
                CreatedAt = DateTime.UtcNow
            };
        }

    }
}
