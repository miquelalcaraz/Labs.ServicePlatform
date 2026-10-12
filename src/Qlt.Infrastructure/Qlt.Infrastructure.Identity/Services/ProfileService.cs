using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

using Microsoft.AspNetCore.Identity;

using Qlt.Infrastructure.Identity.Configuration;
using Qlt.Infrastructure.Identity.Models;

namespace Qlt.Infrastructure.Identity.Services
{
    public class ProfileService : IProfileService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileService(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task GetProfileDataAsync(ProfileDataRequestContext context, CancellationToken cancellationToken = default)
        {
            var subject = context.Subject ?? throw new ArgumentNullException(nameof(context.Subject));

            var subjectId = subject.Claims.Where(x => x.Type == "sub").FirstOrDefault()?.Value;

            var user = await _userManager.FindByIdAsync(subjectId);
            if (user is null)
                throw new ArgumentException("Invalid subject identifier");

            var claims = GetClaimsFromUser(user).ToList();

            var roles = await _userManager.GetRolesAsync(user);
            claims.AddRange(roles.Select(role => new Claim(JwtClaimTypes.Role, role)));

            context.IssuedClaims = claims;
        }

        public async Task IsActiveAsync(IsActiveContext context, CancellationToken cancellationToken = default)
        {
            var subject = context.Subject ?? throw new ArgumentNullException(nameof(context.Subject));

            var subjectId = subject.Claims.Where(x => x.Type == "sub").FirstOrDefault()?.Value;
            var user = await _userManager.FindByIdAsync(subjectId);

            context.IsActive = false;

            if (user is not null)
            {
                if (_userManager.SupportsUserSecurityStamp)
                {
                    var security_stamp = subject.Claims.Where(c => c.Type == "security_stamp").Select(c => c.Value).SingleOrDefault();
                    if (security_stamp is not null)
                    {
                        var db_security_stamp = await _userManager.GetSecurityStampAsync(user);
                        if (db_security_stamp != security_stamp)
                            return;
                    }
                }

                context.IsActive =
                    !user.LockoutEnabled ||
                    !user.LockoutEnd.HasValue ||
                    user.LockoutEnd <= DateTime.UtcNow;
            }
        }

        private IEnumerable<Claim> GetClaimsFromUser(ApplicationUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtClaimTypes.Subject, user.Id),
                new Claim(JwtClaimTypes.PreferredUserName, user.UserName!),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName!)
            };

            claims.Add(new Claim(QltClaimTypes.TenantId, user.TenantId.ToString()));
            claims.Add(new Claim("user_type", user.UserType.ToString()));
            claims.Add(new Claim("user_status", user.Status.ToString()));

            if (!string.IsNullOrWhiteSpace(user.FirstName!))
                claims.Add(new Claim("name", user.FirstName!));

            if (!string.IsNullOrWhiteSpace(user.LastName!))
                claims.Add(new Claim("last_name", user.LastName!));

            if (!string.IsNullOrWhiteSpace(user.DisplayName))
                claims.Add(new Claim("display_name", user.DisplayName));

            if (!string.IsNullOrWhiteSpace(user.PreferredLanguage))
                claims.Add(new Claim("preferred_language", user.PreferredLanguage));


            if (_userManager.SupportsUserEmail)
            {
                claims.AddRange(new[]
                {
                    new Claim(JwtClaimTypes.Email, user.Email!),
                    new Claim(JwtClaimTypes.EmailVerified, user.EmailConfirmed ? "true" : "false", ClaimValueTypes.Boolean)
                });
            }

            if (_userManager.SupportsUserPhoneNumber && !string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                claims.AddRange(new[]
                {
                    new Claim(JwtClaimTypes.PhoneNumber, user.PhoneNumber),
                    new Claim(JwtClaimTypes.PhoneNumberVerified, user.PhoneNumberConfirmed ? "true" : "false", ClaimValueTypes.Boolean)
                });
            }

            return claims;
        }
    }
}
