namespace Miqa.Infrastructure.Identity.Services
{
    public interface IPasswordGenerator
    {
        string GenerateTemporaryPassword();
    }
}
