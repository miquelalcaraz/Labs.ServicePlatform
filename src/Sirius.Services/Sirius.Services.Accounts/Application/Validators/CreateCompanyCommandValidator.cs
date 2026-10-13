using FluentValidation;

using Sirius.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Sirius.Services.Accounts.Application.Validators;

/// <summary>
/// Validates business rules before the command reaches the domain layer.
/// </summary>
public class CreateCompanyCommandValidator : AbstractValidator<Commands.CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required");

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("Legal name is required")
            .MaximumLength(200).WithMessage("Legal name cannot exceed 200 characters");

        RuleFor(x => x.TradeName)
            .NotEmpty().WithMessage("Trade name is required")
            .MaximumLength(200).WithMessage("Trade name cannot exceed 200 characters");

        RuleFor(x => x.TaxId)
            .NotEmpty().WithMessage("Tax ID is required")
            .MaximumLength(50).WithMessage("Tax ID cannot exceed 50 characters")
            .Matches(@"^[A-Za-z0-9\-]+$").WithMessage("Tax ID can only contain alphanumeric characters and hyphens");

        RuleFor(x => x.TaxNumberType)
            .NotEqual(TaxNumberType.Unknown).WithMessage("Tax number type must be specified")
            .IsInEnum().WithMessage("Invalid tax number type");

        RuleFor(x => x.CompanyType)
            .IsInEnum().WithMessage("Invalid company type");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required")
            .MaximumLength(100).WithMessage("Country cannot exceed 100 characters");

        RuleFor(x => x.CountryCode)
            .NotEmpty().WithMessage("Country code is required")
            .MaximumLength(10).WithMessage("Country code cannot exceed 10 characters");

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
