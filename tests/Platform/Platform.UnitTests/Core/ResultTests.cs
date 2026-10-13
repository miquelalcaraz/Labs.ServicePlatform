using Miqa.Platform;

namespace Platform.UnitTests.Core;

public class ResultTests
{
    [Fact]
    public void Success_CreatesSuccessResult()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Success_WithValue_CreatesSuccessResultWithValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_CreatesFailureResult()
    {
        var result = Result.Failure(Error.NotFound);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(Error.NotFound);
    }

    [Fact]
    public void Failure_WithType_CreatesTypedFailureResult()
    {
        var result = Result.Failure<int>(Error.NotFound);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NotFound);
    }

    [Fact]
    public void Failure_AccessingValue_Throws()
    {
        var result = Result.Failure<int>(Error.NotFound);

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_WithNonNullValue_ReturnsSuccess()
    {
        var result = Result.Create<string>("hello");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void Create_WithNull_ReturnsNullValueFailure()
    {
        var result = Result.Create<string>(null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NullValue);
    }

    [Fact]
    public void ImplicitOperator_WithValue_ReturnsSuccess()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void ValidationFailure_WithDictionary_CreatesValidationError()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Name"] = ["Name is required"],
            ["Email"] = ["Email is invalid", "Email is too long"]
        };

        var result = Result.ValidationFailure(errors);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationError>();
        var ve = (ValidationError)result.Error;
        ve.Errors.Should().HaveCount(2);
        ve.Errors["Name"].Should().ContainSingle().Which.Should().Be("Name is required");
        ve.Errors["Email"].Should().HaveCount(2);
    }

    [Fact]
    public void ValidationFailure_WithPropertyAndMessage_CreatesValidationError()
    {
        var result = Result.ValidationFailure("Name", "Name is required");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationError>();
        var ve = (ValidationError)result.Error;
        ve.Errors["Name"].Should().ContainSingle().Which.Should().Be("Name is required");
        ve.PropertyName.Should().Be("Name");
        ve.Message.Should().Be("Name is required");
    }

    [Fact]
    public void ValidationFailure_Generic_WithDictionary_CreatesTypedFailure()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Field"] = ["Error"]
        };

        var result = Result.ValidationFailure<bool>(errors);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationError>();
    }

    [Fact]
    public void ValidationFailure_Generic_WithPropertyAndMessage_CreatesTypedFailure()
    {
        var result = Result.ValidationFailure<int>("Age", "Age must be positive");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationError>();
        ((ValidationError)result.Error).Errors["Age"].Should().Contain("Age must be positive");
    }

    [Fact]
    public void Match_OnSuccess_CallsOnSuccess()
    {
        var result = Result.Success();

        var output = result.Match(
            () => "ok",
            error => $"fail: {error.Message}");

        output.Should().Be("ok");
    }

    [Fact]
    public void Match_OnFailure_CallsOnFailure()
    {
        var result = Result.Failure(Error.NotFound);

        var output = result.Match(
            () => "ok",
            error => $"fail: {error.Code}");

        output.Should().Be("fail: Error.NotFound");
    }

    [Fact]
    public void Bind_OnSuccess_ExecutesBinder()
    {
        var result = Result.Success();

        var bound = result.Bind(() => Result.Success(42));

        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be(42);
    }

    [Fact]
    public void Bind_OnFailure_PropagatesError()
    {
        var result = Result.Failure(Error.BadRequest);

        var bound = result.Bind(() => Result.Success(42));

        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be(Error.BadRequest);
    }

    [Fact]
    public void Bind_NonGeneric_OnSuccess_ExecutesBinder()
    {
        var result = Result.Success();

        var bound = result.Bind(() => Result.Success());

        bound.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Bind_NonGeneric_OnFailure_ReturnsOriginal()
    {
        var result = Result.Failure(Error.Conflict);

        var bound = result.Bind(() => Result.Success());

        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be(Error.Conflict);
    }
}

public class ResultOfTTests
{
    [Fact]
    public void Map_OnSuccess_TransformsValue()
    {
        var result = Result.Success(5);

        var mapped = result.Map(v => v * 2);

        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(10);
    }

    [Fact]
    public void Map_OnFailure_PropagatesError()
    {
        var result = Result.Failure<int>(Error.NotFound);

        var mapped = result.Map(v => v * 2);

        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Should().Be(Error.NotFound);
    }

    [Fact]
    public void Map_TransformsType()
    {
        var result = Result.Success(42);

        var mapped = result.Map(v => v.ToString());

        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be("42");
    }

    [Fact]
    public void Bind_OnSuccess_ExecutesBinder()
    {
        var result = Result.Success(5);

        var bound = result.Bind(v => v > 0
            ? Result.Success($"positive: {v}")
            : Result.Failure<string>(Error.BadRequest));

        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be("positive: 5");
    }

    [Fact]
    public void Bind_OnSuccess_BinderReturnsFailure()
    {
        var result = Result.Success(-1);

        var bound = result.Bind(v => v > 0
            ? Result.Success($"positive: {v}")
            : Result.Failure<string>(Error.BadRequest));

        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be(Error.BadRequest);
    }

    [Fact]
    public void Bind_OnFailure_PropagatesError()
    {
        var result = Result.Failure<int>(Error.Conflict);

        var bound = result.Bind(v => Result.Success(v.ToString()));

        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be(Error.Conflict);
    }

    [Fact]
    public void Match_OnSuccess_CallsOnSuccess()
    {
        var result = Result.Success("hello");

        var output = result.Match(
            value => $"got: {value}",
            error => $"fail: {error.Code}");

        output.Should().Be("got: hello");
    }

    [Fact]
    public void Match_OnFailure_CallsOnFailure()
    {
        var result = Result.Failure<string>(Error.Unauthorized);

        var output = result.Match(
            value => $"got: {value}",
            error => $"fail: {error.Code}");

        output.Should().Be("fail: Error.Unauthorized");
    }

    [Fact]
    public void Map_Then_Bind_Chain()
    {
        var result = Result.Success(5)
            .Map(v => v * 10)
            .Bind(v => v > 100
                ? Result.Failure<int>(Error.BadRequest)
                : Result.Success(v));

        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(50);
    }

    [Fact]
    public void Map_Then_Bind_Chain_FailsInBind()
    {
        var result = Result.Success(20)
            .Map(v => v * 10)
            .Bind(v => v > 100
                ? Result.Failure<int>(Error.BadRequest)
                : Result.Success(v));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.BadRequest);
    }

    [Fact]
    public void Map_Then_Match_Chain()
    {
        var output = Result.Success(42)
            .Map(v => v.ToString())
            .Match(
                value => $"Value is {value}",
                error => "Error");

        output.Should().Be("Value is 42");
    }
}
