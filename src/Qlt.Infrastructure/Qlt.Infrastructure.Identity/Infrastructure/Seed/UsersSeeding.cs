using Qlt.Infrastructure.Identity.Models;

namespace Qlt.Infrastructure.Identity.Infrastructure.Seed;

public class UsersSeeding
{
    private static readonly Guid DemoTenantId = Guid.Parse("F42C75FC-4679-40F7-9C68-EBD4699F07D5");
    private static readonly Guid DemoOrganization01Id = Guid.Parse("36E4E153-354D-4D86-AA03-18528884EF88");

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
                ApplicationUser.CreateAsUnConfirmed("apiuser01","apiuser01@Qlt.com",DemoTenantId, Guid.Parse("16F7FAF9-517C-4EEA-AE4A-6AA0D6D7097D")),
            };
    }
    public static Tenant GetTenant()
    {
        var demoTenant01 = Tenant.Create(DemoTenantId, DemoOrganization01Id, "Demo Tenant 01");

        return demoTenant01;
    }
    public static List<ApplicationUser> GetUsers()
    {

        var users = new List<ApplicationUser> {
                    ApplicationUser.CreateAsUnConfirmed("admin@example.com","admin@example.com",DemoTenantId,Guid.Parse("BD56F2F0-8561-4E9A-9053-10BEBBCD018C")),
                    ApplicationUser.CreateAsUnConfirmed("admin2@example.com","admin2@example.com",DemoTenantId,Guid.Parse("5F87A2BE-4B76-4D49-AB7A-F9CFF5997518")),
                      };

        return users;
    }

}
