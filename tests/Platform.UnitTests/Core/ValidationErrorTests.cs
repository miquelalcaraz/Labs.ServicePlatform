using Qlt.Platform;

namespace Platform.UnitTests.Core;

public class ValidationErrorTests
{
    [Fact]
    public void Constructor_WithDictionary_SetsErrorsAndBaseProperties()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Name"] = ["Name is required"],
            ["Email"] = ["Email is invalid", "Email is too long"]
        };

        var ve = new ValidationError(errors);

        ve.Errors.Should().BeSameAs(errors);
        ve.Code.Should().Be("Error.ValidationFailed");
        ve.Message.Should().Be("One or more validation errors occurred.");
    }

    [Fact]
    public void Constructor_WithPropertyAndMessage_SetsSingleError()
    {
        var ve = new ValidationError("Age", "Age must be positive");

        ve.Errors.Should().HaveCount(1);
        ve.Errors["Age"].Should().ContainSingle().Which.Should().Be("Age must be positive");
        ve.PropertyName.Should().Be("Age");
        ve.Message.Should().Be("Age must be positive");
        ve.Code.Should().Be("Error.ValidationFailed");
    }

    [Fact]
    public void Constructor_WithTupleEnumerable_GroupsByPropertyName()
    {
        var failures = new List<(string PropertyName, string ErrorMessage)>
        {
            ("Name", "Name is required"),
            ("Email", "Email is invalid"),
            ("Email", "Email is too long"),
            ("Name", "Name is too short")
        };

        var ve = new ValidationError(failures);

        ve.Errors.Should().HaveCount(2);
        ve.Errors["Name"].Should().BeEquivalentTo(["Name is required", "Name is too short"]);
        ve.Errors["Email"].Should().BeEquivalentTo(["Email is invalid", "Email is too long"]);
        ve.Code.Should().Be("Error.ValidationFailed");
    }

    [Fact]
    public void IsSubclassOfError()
    {
        var ve = new ValidationError("Field", "Error");

        Error error = ve;

        error.Should().BeOfType<ValidationError>();
        error.Code.Should().Be("Error.ValidationFailed");
    }

    [Fact]
    public void Constructor_WithEmptyDictionary_SetsEmptyErrors()
    {
        var errors = new Dictionary<string, string[]>();

        var ve = new ValidationError(errors);

        ve.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithEmptyTupleEnumerable_SetsEmptyErrors()
    {
        var failures = Enumerable.Empty<(string PropertyName, string ErrorMessage)>();

        var ve = new ValidationError(failures);

        ve.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithSingleTuple_CreatesOneDictionaryEntry()
    {
        var failures = new[] { ("Password", "Password too weak") };

        var ve = new ValidationError(failures);

        ve.Errors.Should().HaveCount(1);
        ve.Errors["Password"].Should().ContainSingle().Which.Should().Be("Password too weak");
    }
}
