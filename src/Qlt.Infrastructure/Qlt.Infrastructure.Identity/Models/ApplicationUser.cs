using Microsoft.AspNetCore.Identity;

namespace Qlt.Infrastructure.Identity.Models
{
    public class ApplicationUser : IdentityUser
    {

        public Guid TenantId { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public string? DisplayName { get; set; }

        public UserType UserType { get; set; }

        public UserStatus Status { get; set; }

        public string? PreferredLanguage { get; set; }

        public DateTime CreatedAt { get; set; }

        public Tenant? Tenant { get; protected set; }

        internal ApplicationUser()
        {

        }
        protected ApplicationUser(Guid? id)
        {
            Id = id?.ToString() ?? Guid.NewGuid().ToString();
        }

        public static ApplicationUser ProvisionExternalUser(
            string email,
            Guid tenantId,
            string? firstName = null,
            string? lastName = null,
            Guid? id = null)
        {
            return new ApplicationUser(id)
            {
                UserName = email,
                Email = email,
                TenantId = tenantId,
                FirstName = firstName,
                LastName = lastName,
                DisplayName = BuildDisplayName(firstName, lastName),
                UserType = UserType.External,
                Status = UserStatus.PendingActivation,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static ApplicationUser ProvisionInternalUser(
            Guid tenantId,
            string email,
            string? firstName = null,
            string? lastName = null,
            Guid? id = null)
        {
            return new ApplicationUser(id)
            {
                TenantId = tenantId,
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                DisplayName = BuildDisplayName(firstName, lastName),
                UserType = UserType.Internal,
                Status = UserStatus.PendingActivation,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static ApplicationUser CreateAsUnConfirmed(string userName, string email, Guid tenantId, Guid? id = null, string? firstName = null, string? lastName = null)
        {
            return new ApplicationUser(id)
            {
                UserName = userName,
                Email = email,
                TenantId = tenantId,
                FirstName = firstName,
                LastName = lastName,
                DisplayName = BuildDisplayName(firstName, lastName),
                UserType = UserType.External,
                Status = UserStatus.PendingActivation,
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

        public void Activate()
        {
            Status = UserStatus.Active;
        }

        public void Lock()
        {
            Status = UserStatus.Locked;
        }

        public void Disable()
        {
            Status = UserStatus.Disabled;
        }

        private static string? BuildDisplayName(string? firstName, string? lastName)
        {
            if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
                return null;

            return $"{firstName} {lastName}".Trim();
        }
    }
}
