using Miqa.Platform;

namespace Platform.UnitTests.Core;

public class ErrorTests
{
    [Fact]
    public void None_HasEmptyCodeAndMessage()
    {
        Error.None.Code.Should().BeEmpty();
        Error.None.Message.Should().BeEmpty();
    }

    [Fact]
    public void NullValue_HasExpectedCode()
    {
        Error.NullValue.Code.Should().Be("Error.NullValue");
    }

    [Fact]
    public void Validation_HasExpectedCode()
    {
        Error.Validation.Code.Should().Be("Error.ValidationFailed");
    }

    [Fact]
    public void Sentinels_AreDistinct()
    {
        var sentinels = new[] { Error.None, Error.NullValue, Error.ConditionNotMet, Error.NotFound, Error.Conflict, Error.BadRequest, Error.Unauthorized, Error.Validation };

        sentinels.Select(e => e.Code).Distinct().Should().HaveCount(sentinels.Length);
    }

    [Fact]
    public void WithMessage_ReturnsNewErrorWithUpdatedMessage()
    {
        var original = Error.NotFound;

        var modified = original.WithMessage("Organization not found");

        modified.Message.Should().Be("Organization not found");
        modified.Code.Should().Be(original.Code);
        original.Message.Should().Be("Resource not found.");
    }

    [Fact]
    public void WithPropertyName_ReturnsNewErrorWithUpdatedPropertyName()
    {
        var original = Error.BadRequest;

        var modified = original.WithPropertyName("TenantId");

        modified.PropertyName.Should().Be("TenantId");
        modified.Code.Should().Be(original.Code);
    }

    [Fact]
    public void WithMessage_AndWithPropertyName_CanBeChained()
    {
        var error = Error.NotFound
            .WithMessage("User not found")
            .WithPropertyName("UserId");

        error.Code.Should().Be("Error.NotFound");
        error.Message.Should().Be("User not found");
        error.PropertyName.Should().Be("UserId");
    }

    [Fact]
    public void Record_Equality_WorksCorrectly()
    {
        var error1 = new Error("Test", "Test message");
        var error2 = new Error("Test", "Test message");
        var error3 = new Error("Test", "Different message");

        error1.Should().Be(error2);
        error1.Should().NotBe(error3);
    }
}
