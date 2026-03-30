using FluentValidation;

using Platform.TestWebApi.Commands;



namespace Platform.TestWebApi.Validators;


public class CreateTestEntityCommandValidator : AbstractValidator<CreateTestEntityCommand>
{
    public CreateTestEntityCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("name is required")
            .MaximumLength(200).WithMessage("name cannot exceed 200 characters");


    }
}
