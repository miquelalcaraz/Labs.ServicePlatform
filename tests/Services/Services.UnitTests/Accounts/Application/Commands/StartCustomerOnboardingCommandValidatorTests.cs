using Sirius.Services.Accounts.Application.Commands;
using Sirius.Services.Accounts.Application.Validators;
using Sirius.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Services.UnitTests.Accounts.Application.Commands;

public class StartCustomerOnboardingCommandValidatorTests : AccountsTestBase
{
    private readonly StartCustomerOnboardingCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidCommand_HasNoErrors()
    {
        var result = await _validator.ValidateAsync(CreateValidCommand());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithEmptyPlanCode_HasError(string planCode)
    {
        var command = CreateCommandWith(planCode: planCode);

        var result = await _validator.ValidateAsync(command);

        result.Errors.Should().Contain(e => e.PropertyName == nameof(StartCustomerOnboardingCommand.PlanCode));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithEmptyCompanyLegalName_HasError(string legalName)
    {
        var command = CreateCommandWith(companyLegalName: legalName);

        var result = await _validator.ValidateAsync(command);

        result.Errors.Should().Contain(e => e.PropertyName == nameof(StartCustomerOnboardingCommand.CompanyLegalName));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    public async Task Validate_WithInvalidAdminEmail_HasError(string email)
    {
        var command = CreateCommandWith(adminEmail: email);

        var result = await _validator.ValidateAsync(command);

        result.Errors.Should().Contain(e => e.PropertyName == nameof(StartCustomerOnboardingCommand.AdminEmail));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithEmptyAdminFirstName_HasError(string firstName)
    {
        var command = CreateCommandWith(adminFirstName: firstName);

        var result = await _validator.ValidateAsync(command);

        result.Errors.Should().Contain(e => e.PropertyName == nameof(StartCustomerOnboardingCommand.AdminFirstName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithEmptyAdminLastName_HasError(string lastName)
    {
        var command = CreateCommandWith(adminLastName: lastName);

        var result = await _validator.ValidateAsync(command);

        result.Errors.Should().Contain(e => e.PropertyName == nameof(StartCustomerOnboardingCommand.AdminLastName));
    }

    [Fact]
    public async Task Validate_WithEmptyCreatedByInternalUserId_HasError()
    {
        var command = CreateCommandWith(createdByInternalUserId: Guid.Empty);

        var result = await _validator.ValidateAsync(command);

        result.Errors.Should().Contain(e => e.PropertyName == nameof(StartCustomerOnboardingCommand.CreatedByInternalUserId));
    }

    [Fact]
    public async Task Validate_WithUnknownTaxNumberType_HasError()
    {
        var command = CreateCommandWith(taxNumberType: TaxNumberType.Unknown);

        var result = await _validator.ValidateAsync(command);

        result.Errors.Should().Contain(e => e.PropertyName == nameof(StartCustomerOnboardingCommand.TaxNumberType));
    }

    [Fact]
    public async Task Validate_WithInvalidTaxIdCharacters_HasError()
    {
        var command = CreateCommandWith(taxId: "B1234 5678!");

        var result = await _validator.ValidateAsync(command);

        result.Errors.Should().Contain(e => e.PropertyName == nameof(StartCustomerOnboardingCommand.TaxId));
    }

    [Fact]
    public async Task Validate_WithTenantNameExceedingMaxLength_HasError()
    {
        var command = CreateCommandWith(tenantName: new string('A', 201));

        var result = await _validator.ValidateAsync(command);

        result.Errors.Should().Contain(e => e.PropertyName == nameof(StartCustomerOnboardingCommand.TenantName));
    }

    [Fact]
    public async Task Validate_WithPostalCodeExceedingMaxLength_HasError()
    {
        var command = CreateCommandWith(postalCode: new string('0', 21));

        var result = await _validator.ValidateAsync(command);

        result.Errors.Should().Contain(e => e.PropertyName == nameof(StartCustomerOnboardingCommand.PostalCode));
    }

    private static StartCustomerOnboardingCommand CreateCommandWith(
        string? planCode = null,
        string? companyLegalName = null,
        string? companyTradeName = null,
        string? taxId = null,
        TaxNumberType? taxNumberType = null,
        string? country = null,
        string? countryCode = null,
        string? adminEmail = null,
        string? adminFirstName = null,
        string? adminLastName = null,
        Guid? createdByInternalUserId = null,
        string? tenantName = "Acme Tenant",
        string? postalCode = null) =>
        new(
            planCode: planCode ?? "STANDARD",
            companyLegalName: companyLegalName ?? "Acme Corp S.L.",
            companyTradeName: companyTradeName ?? "Acme",
            taxId: taxId ?? "B12345678",
            taxNumberType: taxNumberType ?? TaxNumberType.Cif,
            country: country ?? "Spain",
            countryCode: countryCode ?? "ES",
            adminEmail: adminEmail ?? "admin@acme.com",
            adminFirstName: adminFirstName ?? "John",
            adminLastName: adminLastName ?? "Doe",
            createdByInternalUserId: createdByInternalUserId ?? AccountsTestBase.InternalUserId,
            tenantName: tenantName,
            postalCode: postalCode,
            requestId: Guid.NewGuid());
}
