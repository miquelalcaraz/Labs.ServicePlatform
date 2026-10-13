using FluentValidation;

using Miqa.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Miqa.Services.Accounts.Application.Validators;

/// <summary>
/// Validates business rules before the StartCustomerOnboarding command reaches the domain layer.
/// </summary>
public class StartCustomerOnboardingCommandValidator : AbstractValidator<Commands.StartCustomerOnboardingCommand>
{
    public StartCustomerOnboardingCommandValidator()
    {
        RuleFor(x => x.PlanCode)
            .NotEmpty().WithMessage("Plan code is required")
            .MaximumLength(100).WithMessage("Plan code cannot exceed 100 characters");

        RuleFor(x => x.CompanyLegalName)
            .NotEmpty().WithMessage("Company legal name is required")
            .MaximumLength(200).WithMessage("Company legal name cannot exceed 200 characters");

        RuleFor(x => x.CompanyTradeName)
            .MaximumLength(200).WithMessage("Company trade name cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.CompanyTradeName));

        RuleFor(x => x.TenantName)
            .MaximumLength(200).WithMessage("Tenant name cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.TenantName));

        RuleFor(x => x.TaxId)
            .NotEmpty().WithMessage("Tax ID is required")
            .MaximumLength(50).WithMessage("Tax ID cannot exceed 50 characters")
            .Matches(@"^[A-Za-z0-9\-]+$").WithMessage("Tax ID can only contain alphanumeric characters and hyphens");

        RuleFor(x => x.TaxNumberType)
            .NotEqual(TaxNumberType.Unknown).WithMessage("Tax number type must be specified")
            .IsInEnum().WithMessage("Invalid tax number type");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required")
            .MaximumLength(100).WithMessage("Country cannot exceed 100 characters");

        RuleFor(x => x.CountryCode)
            .NotEmpty().WithMessage("Country code is required")
            .MaximumLength(10).WithMessage("Country code cannot exceed 10 characters");

        RuleFor(x => x.AdminEmail)
            .NotEmpty().WithMessage("Admin email is required")
            .EmailAddress().WithMessage("A valid admin email is required")
            .MaximumLength(256).WithMessage("Admin email cannot exceed 256 characters");

        RuleFor(x => x.AdminFirstName)
            .NotEmpty().WithMessage("Admin first name is required")
            .MaximumLength(100).WithMessage("Admin first name cannot exceed 100 characters");

        RuleFor(x => x.AdminLastName)
            .NotEmpty().WithMessage("Admin last name is required")
            .MaximumLength(100).WithMessage("Admin last name cannot exceed 100 characters");

        RuleFor(x => x.CreatedByInternalUserId)
            .NotEmpty().WithMessage("CreatedByInternalUserId is required");

        RuleFor(x => x.PostalCode)
            .MaximumLength(20).WithMessage("Postal code cannot exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.PostalCode));

        RuleFor(x => x.Street)
            .MaximumLength(200).WithMessage("Street cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.Street));

        RuleFor(x => x.City)
            .MaximumLength(100).WithMessage("City cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.City));

        RuleFor(x => x.State)
            .MaximumLength(100).WithMessage("State cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.State));
    }
}
