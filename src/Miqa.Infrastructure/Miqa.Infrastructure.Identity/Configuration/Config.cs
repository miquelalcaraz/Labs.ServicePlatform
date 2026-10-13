using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace Miqa.Infrastructure.Identity.Configuration
{
    public class Config
    {
        // ApiResources define the apis in your system
        public static IEnumerable<ApiResource> GetApis()
        {
            return new List<ApiResource>
            {
                new ApiResource
                {
                    Name = MiqaApiResources.AccountService,
                    Description = "Accounts Service",
                    Scopes = new List<string>
                    {
                        MiqaApiScopes.Accounts,
                    },
                    UserClaims = new List<string>
                    {
                        MiqaClaimTypes.Role,
                        MiqaClaimTypes.TenantId,
                    }
                },
                new ApiResource
                {    Name = MiqaApiResources.NotificationsService,
                    Description = "Notifications Service",
                    Scopes = new List<string>
                    {
                        MiqaApiScopes.Notifications,
                    },
                    UserClaims = new List<string>
                    {
                        MiqaClaimTypes.Role,
                        MiqaClaimTypes.TenantId,
                    }
                  }
             };
        }

        // ApiScope is used to protect the API 
        //The effect is the same as that of API resources in IdentityServer 3.x
        public static IEnumerable<ApiScope> GetApiScopes()
        {
            return new List<ApiScope>
            {
                new ApiScope(IdentityServerConstants.LocalApi.ScopeName),
                new ApiScope(MiqaApiScopes.Accounts, "Accounts Service"),
                new ApiScope(MiqaApiScopes.Notifications, "Notifications Service"),
            };
        }

        // Identity resources are data like user ID, name, or email address of a user
        // see: http://docs.identityserver.io/en/release/configuration/resources.html
        public static IEnumerable<IdentityResource> GetResources()
        {
            return new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
                new IdentityResources.Email(),
                new IdentityResource
                {
                    Name = MiqaIdentityResources.Role,
                    DisplayName = "User Roles",
                    UserClaims = new List<string>
                    {
                        MiqaClaimTypes.Role
                    }
                },
            };
        }

        // client want to access resources (aka scopes)
        public static IEnumerable<Client> GetClients(IConfiguration configuration)
        {
            return new List<Client>
            {
                new Client
                {
                    ClientId = "webapp",
                    ClientName = "WebApp Client",
                    ClientSecrets = new List<Secret>
                    {
                        new Secret($"secret".Sha256())
                    },
                    ClientUri = $"{configuration["WebAppClient"]}",// public uri of the client
                    AllowedGrantTypes = GrantTypes.Code,
                    AllowAccessTokensViaBrowser = false,
                    RequireConsent = false,
                    AllowOfflineAccess = true,
                    AlwaysIncludeUserClaimsInIdToken = true,
                    RequirePkce = false,
                    RedirectUris = new List<string>
                    {
                        $"{configuration["WebAppClient"]}/signin-oidc"
                    },
                    PostLogoutRedirectUris = new List<string>
                    {
                        $"{configuration["WebAppClient"]}/signout-callback-oidc"
                    },
                    AllowedScopes = new List<string>
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        IdentityServerConstants.StandardScopes.OfflineAccess,
                        MiqaApiScopes.Accounts,
                        MiqaIdentityResources.Role
                    },
                    AccessTokenLifetime = 60*60*2, // 2 hours
                    IdentityTokenLifetime= 60*60*2 // 2 hours
                },
                 new Client
                {
                    ClientId = "BackOffice",
                    ClientName = "BackOffice Client",
                    ClientSecrets = new List<Secret>
                    {
                        new Secret($"secret".Sha256())
                    },
                    ClientUri = $"{configuration["BackOfficeClient"]}",// public uri of the client
                    AllowedGrantTypes = GrantTypes.Code,
                    AllowAccessTokensViaBrowser = false,
                    RequireConsent = false,
                    AllowOfflineAccess = true,
                    AlwaysIncludeUserClaimsInIdToken = true,
                    RequirePkce = false,
                    RedirectUris = new List<string>
                    {
                        $"{configuration["BackOfficeClient"]}/signin-oidc",
                        "https://localhost:5001/signin-oidc"
                    },
                    PostLogoutRedirectUris = new List<string>
                    {
                        $"{configuration["BackOfficeClient"]}/signout-callback-oidc",
                        "https://localhost:5001/signout-callback-oidc"
                    },
                    AllowedScopes = new List<string>
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        IdentityServerConstants.StandardScopes.OfflineAccess,
                        MiqaApiScopes.Accounts,
                        MiqaIdentityResources.Role,
                        MiqaApiScopes.Email
                    },
                    AccessTokenLifetime = 60*60*2, // 2 hours
                    IdentityTokenLifetime= 60*60*2 // 2 hours
                },
                new Client
                {
                    ClientId = "accountswaggerui",
                    ClientName = "Account Swagger UI",
                    AllowedGrantTypes = GrantTypes.Implicit,
                    AllowAccessTokensViaBrowser = true,

                    RedirectUris = { $"{configuration["AccountApiClient"]}/scalar/v1" },
                    PostLogoutRedirectUris = { $"{configuration["AccountApiClient"]}/scalar/v1" },

                    AllowedScopes =
                    {
                        MiqaApiScopes.Accounts
                    }
                },
                new Client
                {
                    ClientId = "notificationsswaggerui",
                    ClientName = "Notifications Swagger UI",
                    AllowedGrantTypes = GrantTypes.Implicit,
                    AllowAccessTokensViaBrowser = true,
                    RedirectUris = { $"{configuration["NotificationsApiClient"]}/scalar/v1" },
                    PostLogoutRedirectUris = { $"{configuration["NotificationsApiClient"]}/scalar/v1" },
                    AllowedScopes =
                    {
                        MiqaApiScopes.Notifications
                    }
                 }
            };
        }
    }
}
