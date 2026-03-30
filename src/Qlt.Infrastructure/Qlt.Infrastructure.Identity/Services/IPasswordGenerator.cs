namespace Qlt.Infrastructure.Identity.Services
{
    public interface IPasswordGenerator
    {
        string GenerateTemporaryPassword();
    }
}
