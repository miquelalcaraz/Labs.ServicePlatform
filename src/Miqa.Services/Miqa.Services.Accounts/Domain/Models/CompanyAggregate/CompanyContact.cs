using Miqa.Domain.SeedWork;

namespace Miqa.Services.Accounts.Domain.Models.CompanyAggregate;

public class CompanyContact : Entity
{
    public Guid CompanyId { get; private set; }

    public string FirstName { get; private set; } = null!;

    public string? LastName { get; private set; }

    public string Email { get; private set; } = null!;

    public string PhoneNumber { get; private set; } = null!;

    public string? JobTitle { get; private set; }

    public Company Company { get; } = null!;

    protected CompanyContact() { }

    public static CompanyContact Create(
        Guid companyId, string firstName, string lastName, string email, string phoneNumber, string jobTitle)
    {
        return new CompanyContact
        {
            CompanyId = companyId,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phoneNumber,
            JobTitle = jobTitle
        };
    }
}
