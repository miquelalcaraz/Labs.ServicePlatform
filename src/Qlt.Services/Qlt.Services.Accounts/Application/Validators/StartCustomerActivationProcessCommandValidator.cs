using FluentValidation;

using Qlt.Services.Accounts.Application.Commands;

namespace Qlt.Services.Accounts.Application.Validators;

public class StartCustomerActivationProcessCommandValidator : AbstractValidator<StartCustomerActivationProcessCommand>
{
    public StartCustomerActivationProcessCommandValidator()
    {
        RuleFor(x => x.SubscriptionId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.OwnerCompanyId).NotEmpty();
        RuleFor(x => x.AdminUserId).NotEmpty();
        RuleFor(x => x.AdminMembershipId).NotEmpty();
    }
}
