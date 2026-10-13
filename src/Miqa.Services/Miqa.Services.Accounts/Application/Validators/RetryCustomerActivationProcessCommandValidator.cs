using FluentValidation;

using Miqa.Services.Accounts.Application.Commands;

namespace Miqa.Services.Accounts.Application.Validators;

public class RetryCustomerActivationProcessCommandValidator : AbstractValidator<RetryCustomerActivationProcessCommand>
{
    public RetryCustomerActivationProcessCommandValidator()
    {
        RuleFor(x => x.ActivationProcessId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
