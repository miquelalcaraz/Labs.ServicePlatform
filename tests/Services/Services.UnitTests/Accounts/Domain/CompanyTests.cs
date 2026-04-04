using Qlt.Services.Accounts.Domain.Events;
using Qlt.Services.Accounts.Domain.Exceptions;
using Qlt.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Services.UnitTests.Accounts.Domain;

public class CompanyTests
{
    private static Address CreateValidAddress() =>
        Address.Create("Mexico", "MX", "Jalisco", "44100", "Guadalajara", "Av. Vallarta 1234");

    [Fact]
    public void Create_WithValidData_CreatesCompanyWithPendingStatus()
    {
        var tenantId = Guid.NewGuid();
        var internalUserId = Guid.NewGuid();
        var address = CreateValidAddress();

        var company = Company.Create(
            tenantId, "Acme S.A. de C.V.", "Acme",
            CompanyType.Owner, address, "RFC1234567890", TaxNumberType.Rfc, internalUserId);

        company.TenantId.Should().Be(tenantId);
        company.LegalName.Should().Be("Acme S.A. de C.V.");
        company.TradeName.Should().Be("Acme");
        company.CompanyType.Should().Be(CompanyType.Owner);
        company.Status.Should().Be(CompanyStatus.Pending);
        company.Address.Should().Be(address);
        company.TaxId.Should().Be("RFC1234567890");
        company.TaxNumberType.Should().Be(TaxNumberType.Rfc);
        company.CreatedByInternalUserId.Should().Be(internalUserId);
    }

    [Fact]
    public void Create_TrimsLegalNameAndTradeNameAndTaxId()
    {
        var company = Company.Create(
            Guid.NewGuid(), "  Acme S.A.  ", "  Acme  ",
            CompanyType.Owner, CreateValidAddress(), "  RFC123  ", TaxNumberType.Rfc);

        company.LegalName.Should().Be("Acme S.A.");
        company.TradeName.Should().Be("Acme");
        company.TaxId.Should().Be("RFC123");
    }

    [Fact]
    public void Create_WithUnknownTaxNumberType_ThrowsAccountDomainException()
    {
        var act = () => Company.Create(
            Guid.NewGuid(), "Acme", "Acme",
            CompanyType.Owner, CreateValidAddress(), "RFC123", TaxNumberType.Unknown);

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void Create_RaisesCompanyCreatedDomainEvent()
    {
        var tenantId = Guid.NewGuid();

        var company = Company.Create(
            tenantId, "Acme S.A.", "Acme",
            CompanyType.Owner, CreateValidAddress(), "RFC123", TaxNumberType.Rfc);

        var domainEvent = company.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CompanyCreatedDomainEvent>().Subject;

        domainEvent.TenantId.Should().Be(tenantId);
        domainEvent.CompanyId.Should().Be(company.Id);
        domainEvent.LegalName.Should().Be("Acme S.A.");
        domainEvent.TradeName.Should().Be("Acme");
        domainEvent.TaxId.Should().Be("RFC123");
        domainEvent.CountryCode.Should().Be("MX");
    }

    [Fact]
    public void CreateOwnerCompany_CreatesCompanyWithOwnerType()
    {
        var tenantId = Guid.NewGuid();
        var internalUserId = Guid.NewGuid();

        var company = Company.CreateOwnerCompany(
            tenantId, "Acme S.A.", "Acme",
            CreateValidAddress(), "RFC123", TaxNumberType.Rfc, internalUserId);

        company.CompanyType.Should().Be(CompanyType.Owner);
        company.CreatedByInternalUserId.Should().Be(internalUserId);
        company.Status.Should().Be(CompanyStatus.Pending);
    }

    [Fact]
    public void Activate_SetsStatusToActive()
    {
        var company = Company.Create(
            Guid.NewGuid(), "Acme", "Acme",
            CompanyType.Owner, CreateValidAddress(), "RFC123", TaxNumberType.Rfc);

        company.Activate();

        company.Status.Should().Be(CompanyStatus.Active);
    }

    [Fact]
    public void Deactivate_WhenActive_SetsStatusToInactive()
    {
        var company = Company.Create(
            Guid.NewGuid(), "Acme", "Acme",
            CompanyType.Owner, CreateValidAddress(), "RFC123", TaxNumberType.Rfc);
        company.Activate();

        company.Deactivate();

        company.Status.Should().Be(CompanyStatus.Inactive);
    }

    [Fact]
    public void Deactivate_WhenNotActive_ThrowsAccountDomainException()
    {
        var company = Company.Create(
            Guid.NewGuid(), "Acme", "Acme",
            CompanyType.Owner, CreateValidAddress(), "RFC123", TaxNumberType.Rfc);

        var act = () => company.Deactivate();

        act.Should().Throw<AccountDomainException>();
    }

    [Fact]
    public void AddContact_WithValidData_AddsContactToCollection()
    {
        var company = Company.Create(
            Guid.NewGuid(), "Acme", "Acme",
            CompanyType.Owner, CreateValidAddress(), "RFC123", TaxNumberType.Rfc);

        company.AddContact("John", "Doe", "john@acme.com", "+5211234567", "CEO");

        company.Contacts.Should().ContainSingle()
            .Which.Email.Should().Be("john@acme.com");
    }

    [Fact]
    public void AddContact_WithDuplicateEmail_DoesNotAddDuplicate()
    {
        var company = Company.Create(
            Guid.NewGuid(), "Acme", "Acme",
            CompanyType.Owner, CreateValidAddress(), "RFC123", TaxNumberType.Rfc);

        company.AddContact("John", "Doe", "john@acme.com", "+5211234567", "CEO");
        company.AddContact("Jane", "Doe", "john@acme.com", "+5219876543", "CTO");

        company.Contacts.Should().HaveCount(1);
    }

    [Fact]
    public void AddContact_WithDuplicatePhone_DoesNotAddDuplicate()
    {
        var company = Company.Create(
            Guid.NewGuid(), "Acme", "Acme",
            CompanyType.Owner, CreateValidAddress(), "RFC123", TaxNumberType.Rfc);

        company.AddContact("John", "Doe", "john@acme.com", "+5211234567", "CEO");
        company.AddContact("Jane", "Doe", "jane@acme.com", "+5211234567", "CTO");

        company.Contacts.Should().HaveCount(1);
    }

    [Fact]
    public void AddContact_WithUniqueEmailAndPhone_AddsBothContacts()
    {
        var company = Company.Create(
            Guid.NewGuid(), "Acme", "Acme",
            CompanyType.Owner, CreateValidAddress(), "RFC123", TaxNumberType.Rfc);

        company.AddContact("John", "Doe", "john@acme.com", "+5211234567", "CEO");
        company.AddContact("Jane", "Doe", "jane@acme.com", "+5219876543", "CTO");

        company.Contacts.Should().HaveCount(2);
    }
}
