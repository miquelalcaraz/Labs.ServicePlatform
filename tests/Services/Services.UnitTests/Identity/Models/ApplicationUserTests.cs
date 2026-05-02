using Qlt.Infrastructure.Identity.Models;

namespace Services.UnitTests.Identity.Models;

public class ApplicationUserTests
{
    [Fact]
    public void CreateAsUnConfirmed_WithValidData_CreatesUserWithExpectedProperties()
    {
        var tenantId = Guid.NewGuid();

        var user = ApplicationUser.CreateAsUnConfirmed(
            userName: "admin@acme.com",
            email: "admin@acme.com",
            tenantId: tenantId,
            firstName: "John",
            lastName: "Doe");

        user.UserName.Should().Be("admin@acme.com");
        user.Email.Should().Be("admin@acme.com");
        user.TenantId.Should().Be(tenantId);
        user.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
    }

    [Fact]
    public void CreateAsUnConfirmed_GeneratesIdWhenNoneProvided()
    {
        var user = ApplicationUser.CreateAsUnConfirmed(
            userName: "admin@acme.com",
            email: "admin@acme.com",
            tenantId: Guid.NewGuid());

        user.Id.Should().NotBeNullOrEmpty();
        Guid.TryParse(user.Id, out _).Should().BeTrue();
    }

    [Fact]
    public void CreateAsUnConfirmed_WithExplicitId_UsesProvidedId()
    {
        var id = Guid.NewGuid();

        var user = ApplicationUser.CreateAsUnConfirmed(
            userName: "admin@acme.com",
            email: "admin@acme.com",
            tenantId: Guid.NewGuid(),
            id: id);

        user.Id.Should().Be(id.ToString());
    }

    [Fact]
    public void CreateAsUnConfirmed_WithoutFirstAndLastName_LeavesBothNull()
    {
        var user = ApplicationUser.CreateAsUnConfirmed(
            userName: "admin@acme.com",
            email: "admin@acme.com",
            tenantId: Guid.NewGuid());

        user.FirstName.Should().BeNull();
        user.LastName.Should().BeNull();
    }

    [Fact]
    public void CreateAsUnConfirmed_SetsCreatedAtToNow()
    {
        var before = DateTime.UtcNow;

        var user = ApplicationUser.CreateAsUnConfirmed(
            userName: "admin@acme.com",
            email: "admin@acme.com",
            tenantId: Guid.NewGuid());

        user.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void CreateAsUnConfirmed_SetsUserTypeToExternalAndStatusToPendingActivation()
    {
        var user = ApplicationUser.CreateAsUnConfirmed(
            userName: "admin@acme.com",
            email: "admin@acme.com",
            tenantId: Guid.NewGuid());

        user.UserType.Should().Be(UserType.External);
        user.Status.Should().Be(UserStatus.PendingActivation);
    }

    [Fact]
    public void CreateAsUnConfirmed_WithNames_SetsDisplayName()
    {
        var user = ApplicationUser.CreateAsUnConfirmed(
            userName: "admin@acme.com",
            email: "admin@acme.com",
            tenantId: Guid.NewGuid(),
            firstName: "John",
            lastName: "Doe");

        user.DisplayName.Should().Be("John Doe");
    }

    [Fact]
    public void ProvisionExternalUser_CreatesUserWithExternalTypeAndPendingActivation()
    {
        var tenantId = Guid.NewGuid();

        var user = ApplicationUser.ProvisionExternalUser(
            email: "admin@acme.com",
            tenantId: tenantId,
            firstName: "John",
            lastName: "Doe");

        user.Email.Should().Be("admin@acme.com");
        user.UserName.Should().Be("admin@acme.com");
        user.TenantId.Should().Be(tenantId);
        user.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
        user.UserType.Should().Be(UserType.External);
        user.Status.Should().Be(UserStatus.PendingActivation);
        user.DisplayName.Should().Be("John Doe");
    }

    [Fact]
    public void ProvisionExternalUser_WithExplicitId_UsesProvidedId()
    {
        var id = Guid.NewGuid();

        var user = ApplicationUser.ProvisionExternalUser(
            email: "admin@acme.com",
            tenantId: Guid.NewGuid(),
            id: id);

        user.Id.Should().Be(id.ToString());
    }

    [Fact]
    public void ProvisionInternalUser_CreatesUserWithInternalTypeAndNoTenantId()
    {
        var tenantId = Guid.NewGuid();
        var user = ApplicationUser.ProvisionInternalUser(
            tenantId: tenantId,
            email: "admin@qlt.com",
            firstName: "Jane",
            lastName: "Smith");

        user.Email.Should().Be("admin@qlt.com");
        user.UserName.Should().Be("admin@qlt.com");
        user.UserType.Should().Be(UserType.Internal);
        user.Status.Should().Be(UserStatus.PendingActivation);
        user.DisplayName.Should().Be("Jane Smith");
        user.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void ProvisionInternalUser_WithExplicitId_UsesProvidedId()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = ApplicationUser.ProvisionInternalUser(
            tenantId: tenantId,
            email: "admin@qlt.com",
            id: id);

        user.Id.Should().Be(id.ToString());
    }

    [Fact]
    public void Activate_SetsStatusToActive()
    {
        var user = ApplicationUser.ProvisionExternalUser("admin@acme.com", Guid.NewGuid());

        user.Activate();

        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Lock_SetsStatusToLocked()
    {
        var user = ApplicationUser.ProvisionExternalUser("admin@acme.com", Guid.NewGuid());

        user.Lock();

        user.Status.Should().Be(UserStatus.Locked);
    }

    [Fact]
    public void Disable_SetsStatusToDisabled()
    {
        var user = ApplicationUser.ProvisionExternalUser("admin@acme.com", Guid.NewGuid());

        user.Disable();

        user.Status.Should().Be(UserStatus.Disabled);
    }

    [Fact]
    public void DisplayName_WithOnlyFirstName_ReturnsFirstNameOnly()
    {
        var user = ApplicationUser.ProvisionExternalUser(
            email: "admin@acme.com",
            tenantId: Guid.NewGuid(),
            firstName: "John");

        user.DisplayName.Should().Be("John");
    }

    [Fact]
    public void DisplayName_WithNeitherFirstNorLastName_ReturnsNull()
    {
        var user = ApplicationUser.ProvisionExternalUser(
            email: "admin@acme.com",
            tenantId: Guid.NewGuid());

        user.DisplayName.Should().BeNull();
    }
}
