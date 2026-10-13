using FluentValidation;

using Sirius.Services.Accounts.Application.Commands;

namespace Sirius.Services.Accounts.Application.Validators;

public class RetryCustomerActivationProcessCommandValidator : AbstractValidator<RetryCustomerActivationProcessCommand>
{
    public RetryCustomerActivationProcessCommandValidator()
    {
        RuleFor(x => x.ActivationProcessId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
