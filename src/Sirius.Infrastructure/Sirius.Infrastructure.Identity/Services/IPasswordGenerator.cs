namespace Sirius.Infrastructure.Identity.Services
{
    public interface IPasswordGenerator
    {
        string GenerateTemporaryPassword();
    }
}
