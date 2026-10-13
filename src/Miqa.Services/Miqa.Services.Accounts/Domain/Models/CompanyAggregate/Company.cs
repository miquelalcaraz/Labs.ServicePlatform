using Miqa.Domain.SeedWork;
using Miqa.Services.Accounts.Domain.Events;
using Miqa.Services.Accounts.Domain.Exceptions;

namespace Miqa.Services.Accounts.Domain.Models.CompanyAggregate;

public class Company : Entity, IAggregateRoot
{
    public Guid TenantId { get; private set; }

    public string LegalName { get; private set; } = null!;

    public string TradeName { get; private set; } = null!;

    public CompanyType CompanyType { get; private set; }

    public CompanyStatus Status { get; private set; }

    public Address? Address { get; private set; }

    public string TaxId { get; private set; } = null!;

    public TaxNumberType TaxNumberType { get; private set; }

    public Guid CreatedByInternalUserId { get; private set; }

    private readonly List<CompanyContact> _contacts;
    public IReadOnlyCollection<CompanyContact> Contacts => _contacts.AsReadOnly();

    protected Company()
    {
        _contacts = [];
    }

    public static Company CreateOwnerCompany(
        Guid tenantId,
        string legalName,
        string tradeName,
        Address address,
        string taxId,
        TaxNumberType taxNumberType,
        Guid createdByInternalUserId)
    {
        if (taxNumberType == TaxNumberType.Unknown)
            throw new AccountDomainException("TaxNumberType cannot be Unknown");

        var company = new Company
        {
            TenantId = tenantId,
            LegalName = legalName.Trim(),
            TradeName = tradeName.Trim(),
            CompanyType = CompanyType.Owner,
            Status = CompanyStatus.Pending,
            Address = address,
            TaxId = taxId.Trim(),
            TaxNumberType = taxNumberType,
            CreatedByInternalUserId = createdByInternalUserId
        };
        return company;
    }

    public static Company Create(
        Guid tenantId,
        string legalName,
        string tradeName,
        CompanyType companyType,
        Address address,
        string taxId,
        TaxNumberType taxNumberType,
        Guid createdByInternalUserId = default)
    {
        if (taxNumberType == TaxNumberType.Unknown)
            throw new AccountDomainException("TaxNumberType cannot be Unknown");

        var company = new Company
        {
            TenantId = tenantId,
            LegalName = legalName.Trim(),
            TradeName = tradeName.Trim(),
            CompanyType = companyType,
            Status = CompanyStatus.Pending,
            Address = address,
            TaxId = taxId.Trim(),
            TaxNumberType = taxNumberType,
            CreatedByInternalUserId = createdByInternalUserId
        };

        company.AddCompanyCreatedDomainEvent();
        return company;
    }

    public void Activate()
    {
        Status = CompanyStatus.Active;
    }

    public void Deactivate()
    {
        if (Status != CompanyStatus.Active)
            throw new AccountDomainException("Only active companies can be deactivated.");

        Status = CompanyStatus.Inactive;
    }

    public void AddContact(string firstName, string lastName, string email, string phoneNumber, string jobTitle)
    {
        var existingContact = _contacts.SingleOrDefault(c =>
            c.Email.Equals(email, StringComparison.OrdinalIgnoreCase) ||
            c.PhoneNumber.Equals(phoneNumber, StringComparison.OrdinalIgnoreCase));

        if (existingContact is null)
        {
            var contact = CompanyContact.Create(Id, firstName, lastName, email, phoneNumber, jobTitle);
            _contacts.Add(contact);
        }
    }

    private void AddCompanyCreatedDomainEvent()
    {
        var domainEvent = new CompanyCreatedDomainEvent(
            TenantId, Id, LegalName, TradeName, TaxId, Address?.CountryCode!);
        AddDomainEvent(domainEvent);
    }
}
