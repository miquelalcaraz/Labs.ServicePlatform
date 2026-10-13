namespace Sirius.Infrastructure.Identity.Configuration
{
    public class SiriusClaimTypes
    {
        public const string Role = "role";
        public const string TenantId = "tenantid";
        public const string OfflineAccess = "offline_access";
    }
    public class SiriusApiScopes
    {
        public const string Accounts = "accounts";
        public const string Notifications = "notifications";
        public const string Email = "email";
    }
    public class SiriusIdentityResources
    {
        public const string Role = "role";
    }
    public class SiriusApiResources
    {
        public const string AccountService = "accounts.service";
        public const string NotificationsService = "notifications.service";
    }
    public static class Roles
    {
        public static class SystemRoles
        {
            public const string SystemAdmin = "SystemAdmin";
            public const string User = "User";
            public const string ApiUser = "ApiUser";
        }
        public static class PlatformRoles
        {
            public const string SalesRepresentative = "SalesRepresentative";
            public const string ProjectManager = "ProjectManager";
            public const string BusinessAdmin = "BusinessAdmin";
            public const string SupportAgent = "SupportAgent";
            public const string PlatformAdmin = "PlatformAdmin";
        }
    }
}
