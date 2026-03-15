using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using QltSystem.Identity.Models;


namespace QltSystem.Identity.Stores
{
    public class ApplicationUserManager : UserManager<ApplicationUser>
    {
        //private readonly IBusService _bus;
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
                //await _bus.Publish(new UserCreatedIntegrationEvent()
                //{
                //    Id = Guid.Parse(user.Id),
                //    Email = user.Email,
                //    UserName = user.UserName,
                //    PhoneNumber = user.PhoneNumber,
                //    OrganizationId = user.OrganizationId
                //});
            }
            return result;
        }
        public override async Task<IdentityResult> UpdateAsync(ApplicationUser user)
        {
            IdentityResult result = await base.UpdateAsync(user);
            if (result.Succeeded)
            {
                //await _bus.Publish(new UserUpdatedIntegrationEvent()
                //{
                //    Id = Guid.Parse(user.Id),
                //    Email = user.Email,
                //    PhoneNumber = user.PhoneNumber,
                //    OrganizationId = user.OrganizationId
                //});
            }
            return result;
        }
    }
}
