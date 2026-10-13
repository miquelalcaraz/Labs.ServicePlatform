using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using Miqa.Infrastructure.Identity.Models;


namespace Miqa.Infrastructure.Identity.Stores
{
    public class ApplicationUserManager : UserManager<ApplicationUser>
    {
        
        public ApplicationUserManager(
            IUserStore<ApplicationUser> store,
            IOptions<IdentityOptions> optionsAccessor,
            IPasswordHasher<ApplicationUser> passwordHasher,
            IEnumerable<IUserValidator<ApplicationUser>> userValidators,
            IEnumerable<IPasswordValidator<ApplicationUser>> passwordValidators,
            ILookupNormalizer keyNormalizer,
            IdentityErrorDescriber errors,
            IServiceProvider services,
            ILogger<UserManager<ApplicationUser>> logger)
            : base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors, services, logger)
        {

        }

        public override async Task<IdentityResult> CreateAsync(ApplicationUser user)
        {

            IdentityResult result = await base.CreateAsync(user);
            if (result.Succeeded)
            {

            }
            return result;
        }
        public override async Task<IdentityResult> UpdateAsync(ApplicationUser user)
        {
            IdentityResult result = await base.UpdateAsync(user);
            if (result.Succeeded)
            {

            }
            return result;
        }
    }
}
