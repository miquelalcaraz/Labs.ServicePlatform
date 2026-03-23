
using Duende.IdentityServer.Models;
namespace QltS.Identity.Infrastructure.Seed
{
    internal class ProviderSeeding
    {
        public static IEnumerable<IdentityProvider> Get()
        {
            List<IdentityProvider> providers = new List<IdentityProvider>
            {
                GetCompnyProvider(),
                //GetGoogleProvider(),
                GetOtherOidcProvider()
            };


            return providers;
        }
        public static IdentityProvider GetCompnyProvider()
        {

            string tenant_id = "00000000-0000-0000-0000-000000000000";
            string client_id = "00000000-0000-0000-0000-000000000000";
            string client_secret = "00000000-0000-0000-0000-000000000000";
            var azureAdProvider = new OidcProvider
            {
                Scheme = "QltSAD",
                DisplayName = "QltS",
                Authority = $"https://login.microsoftonline.com/{tenant_id}/v2.0",
                ClientId = $"{client_id}",
                ClientSecret = $"{client_secret}",
                ResponseType = "code",
                Scope = "openid profile email"
            };


            IdentityProvider identityProvider = new IdentityProvider(azureAdProvider.Type, azureAdProvider) { Enabled = true };


            return identityProvider;
        }

        public static IdentityProvider GetOtherOidcProvider()
        {

            string tenant_id = "00000000-0000-0000-0000-000000000000";
            string client_id = "00000000-0000-0000-0000-000000000000";
            string client_secret = "00000000-0000-0000-0000-000000000000";
            var azureAdProvider = new OidcProvider
            {
                Scheme = "OtherAD",
                DisplayName = "Other AD",
                Authority = $"https://login.microsoftonline.com/{tenant_id}/v2.0",
                ClientId = $"{client_id}",
                ClientSecret = $"{client_secret}",
                ResponseType = "code",
                Scope = "openid profile email"
            };


            IdentityProvider identityProvider = new IdentityProvider(azureAdProvider.Type, azureAdProvider) { Enabled = true };


            return identityProvider;
        }

        /*
        public static IdentityProvider GetGoogleProvider()
        {
            string client_id = "***REMOVED***.apps.googleusercontent.com";
            string client_secret = "***REMOVED***";

            
            IdentityProvider identityProvider = new IdentityProvider("OAuth") //OAuth
            {
                Scheme = "Google",
                DisplayName = "A Google Organization",
                Properties = new Dictionary<string, string>
                {
                    { "SignInScheme", IdentityServerConstants.ExternalCookieAuthenticationScheme },
                    { "ClientId",  $"{client_id}" },
                    { "ClientSecret" , $"{client_secret}"},
                },
                Enabled = true,
            };
            return identityProvider;
        }
        */

    }
}
