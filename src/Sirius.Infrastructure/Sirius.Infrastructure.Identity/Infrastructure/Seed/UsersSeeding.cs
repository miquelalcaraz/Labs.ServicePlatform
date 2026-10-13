using Sirius.Infrastructure.Identity.Models;

namespace Sirius.Infrastructure.Identity.Infrastructure.Seed;

/// <summary>
/// Demo tenant and users for local development. The password comes from the <c>Seed:DemoUsersPassword</c> setting.
/// </summary>
public class UsersSeeding
{
    private static readonly Guid SiriusInternalTenantId = Guid.Parse("F42C75FC-4679-40F7-9C68-EBD4699F07D5");
    private static readonly Guid SiriusInternalCompanyId = Guid.Parse("36E4E153-354D-4D86-AA03-18528884EF88");

    public static List<ApplicationUser> GetAdmins()
    {
        return new List<ApplicationUser>
        {

        };

    }

    public static List<ApplicationUser> GetApiUsers()
    {
        return new List<ApplicationUser>
            {
                ApplicationUser.ProvisionInternalUser(SiriusInternalTenantId,"apiuser01@example.com","api_user","apiuser01", Guid.Parse("16F7FAF9-517C-4EEA-AE4A-6AA0D6D7097D")),
            };
    }
    public static Tenant GetTenant()
    {
        var demoTenant01 = Tenant.Create(SiriusInternalTenantId, SiriusInternalCompanyId, "Sirius Internal Tenant");

        return demoTenant01;
    }
    public static List<ApplicationUser> GetUsers()
    {

        var users = new List<ApplicationUser> {
                    ApplicationUser.ProvisionInternalUser(SiriusInternalTenantId,"admin@example.com","Platform","Admin", Guid.Parse("BD56F2F0-8561-4E9A-9053-10BEBBCD018C")),
                    ApplicationUser.ProvisionInternalUser(SiriusInternalTenantId,"admin2@example.com","Platform","Admin 2",Guid.Parse("5F87A2BE-4B76-4D49-AB7A-F9CFF5997518")),
                      };

        return users;
    }

}
