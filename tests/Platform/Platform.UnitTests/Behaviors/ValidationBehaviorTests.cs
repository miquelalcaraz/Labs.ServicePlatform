using FluentValidation;
using FluentValidation.Results;

using MediatR;

using Miqa.Domain.Behaviors;
using Miqa.Domain.Exceptions;
using Miqa.Platform;

namespace Platform.UnitTests.Behaviors;

public class ValidationBehaviorTests
{
    public record TestCommand(string Name) : IRequest<Result<bool>>;
    public record TestNonResultCommand(string Name) : IRequest<string>;
    public record TestResultCommand(string Name) : IRequest<Result>;

    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<TestCommand, Result<bool>>(
            Enumerable.Empty<IValidator<TestCommand>>());

        var expectedResult = Result.Success(true);
        var next = new RequestHandlerDelegate<Result<bool>>(_ => Task.FromResult(expectedResult));

        var result = await behavior.Handle(new TestCommand("Test"), next, CancellationToken.None);

        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task Handle_ValidatorsPass_CallsNext()
    {
        var validator = new Mock<IValidator<TestCommand>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var behavior = new ValidationBehavior<TestCommand, Result<bool>>(new[] { validator.Object });

        var expectedResult = Result.Success(true);
        var next = new RequestHandlerDelegate<Result<bool>>(_ => Task.FromResult(expectedResult));

        var result = await behavior.Handle(new TestCommand("Valid"), next, CancellationToken.None);

        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task Handle_ValidationFails_ReturnsResultFailure_ForResultOfT()
    {
        var validator = new Mock<IValidator<TestCommand>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("Name", "Name is required"),
                new ValidationFailure("Name", "Name is too short")
            }));

        var behavior = new ValidationBehavior<TestCommand, Result<bool>>(new[] { validator.Object });

        var nextCalled = false;
        var next = new RequestHandlerDelegate<Result<bool>>(_ =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success(true));
        });

        var result = await behavior.Handle(new TestCommand(""), next, CancellationToken.None);

        nextCalled.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationError>();

        var ve = (ValidationError)result.Error;
        ve.Errors.Should().ContainKey("Name");
        ve.Errors["Name"].Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ValidationFails_ReturnsResultFailure_ForResult()
    {
        var validator = new Mock<IValidator<TestResultCommand>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestResultCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("Name", "Name is required")
            }));

        var behavior = new ValidationBehavior<TestResultCommand, Result>(new[] { validator.Object });

        var next = new RequestHandlerDelegate<Result>(_ => Task.FromResult(Result.Success()));

        var result = await behavior.Handle(new TestResultCommand(""), next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationError>();
    }

    [Fact]
    public async Task Handle_ValidationFails_ThrowsDomainValidationException_ForNonResultType()
    {
        var validator = new Mock<IValidator<TestNonResultCommand>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestNonResultCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("Name", "Name is required")
            }));

        var behavior = new ValidationBehavior<TestNonResultCommand, string>(new[] { validator.Object });

        var next = new RequestHandlerDelegate<string>(_ => Task.FromResult("ok"));

        var act = () => behavior.Handle(new TestNonResultCommand(""), next, CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task Handle_MultipleValidators_AggregatesErrors()
    {
        var validator1 = new Mock<IValidator<TestCommand>>();
        validator1
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("Name", "Name is required")
            }));

        var validator2 = new Mock<IValidator<TestCommand>>();
        validator2
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("Email", "Email is invalid")
            }));

        var behavior = new ValidationBehavior<TestCommand, Result<bool>>(
            new[] { validator1.Object, validator2.Object });

        var next = new RequestHandlerDelegate<Result<bool>>(_ => Task.FromResult(Result.Success(true)));

        var result = await behavior.Handle(new TestCommand(""), next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        var ve = (ValidationError)result.Error;
        ve.Errors.Should().HaveCount(2);
        ve.Errors.Should().ContainKey("Name");
        ve.Errors.Should().ContainKey("Email");
    }

    [Fact]
    public async Task Handle_MultipleValidatorsAllPass_CallsNext()
    {
        var validator1 = new Mock<IValidator<TestCommand>>();
        validator1
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var validator2 = new Mock<IValidator<TestCommand>>();
        validator2
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var behavior = new ValidationBehavior<TestCommand, Result<bool>>(
            new[] { validator1.Object, validator2.Object });

        var expectedResult = Result.Success(true);
        var next = new RequestHandlerDelegate<Result<bool>>(_ => Task.FromResult(expectedResult));

        var result = await behavior.Handle(new TestCommand("Valid"), next, CancellationToken.None);

        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task Handle_ValidationFails_SamePropertyMultipleErrors_GroupsCorrectly()
    {
        var validator = new Mock<IValidator<TestCommand>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("Password", "Password is required"),
                new ValidationFailure("Password", "Password must be at least 8 characters"),
                new ValidationFailure("Password", "Password must contain a digit")
            }));

        var behavior = new ValidationBehavior<TestCommand, Result<bool>>(new[] { validator.Object });

        var next = new RequestHandlerDelegate<Result<bool>>(_ => Task.FromResult(Result.Success(true)));

        var result = await behavior.Handle(new TestCommand(""), next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        var ve = (ValidationError)result.Error;
        ve.Errors.Should().HaveCount(1);
        ve.Errors["Password"].Should().HaveCount(3);
    }
}
