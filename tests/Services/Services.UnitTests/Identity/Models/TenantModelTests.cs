using Miqa.Infrastructure.Identity.Models;

namespace Services.UnitTests.Identity.Models;

public class TenantModelTests
{
    [Fact]
    public void Create_WithValidData_CreatesTenantWithExpectedProperties()
    {
        var id = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var tenant = Tenant.Create(id: id, companyId: companyId, name: "Acme Tenant");

        tenant.Id.Should().Be(id);
        tenant.CompanyId.Should().Be(companyId);
        tenant.Name.Should().Be("Acme Tenant");
        tenant.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithNullId_GeneratesNewId()
    {
        var tenant = Tenant.Create(id: null, companyId: Guid.NewGuid(), name: "Acme Tenant");

        tenant.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_InitializesWithEmptyUsersCollection()
    {
        var tenant = Tenant.Create(id: Guid.NewGuid(), companyId: Guid.NewGuid(), name: "Acme Tenant");

        tenant.Users.Should().BeEmpty();
    }

    [Fact]
    public void AddUser_WithMatchingTenantId_AddsUserToCollection()
    {
        var tenant = Tenant.Create(id: Guid.NewGuid(), companyId: Guid.NewGuid(), name: "Acme Tenant");
        var user = ApplicationUser.CreateAsUnConfirmed(
            userName: "admin@acme.com",
            email: "admin@acme.com",
            tenantId: tenant.Id);

        tenant.AddUser(user);

        tenant.Users.Should().ContainSingle().Which.Should().Be(user);
    }

    [Fact]
    public void AddUser_WithMismatchedTenantId_ThrowsInvalidOperationException()
    {
        var tenant = Tenant.Create(id: Guid.NewGuid(), companyId: Guid.NewGuid(), name: "Acme Tenant");
        var user = ApplicationUser.CreateAsUnConfirmed(
            userName: "admin@acme.com",
            email: "admin@acme.com",
            tenantId: Guid.NewGuid());

        var act = () => tenant.AddUser(user);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddUser_WithNullUser_ThrowsArgumentNullException()
    {
        var tenant = Tenant.Create(id: Guid.NewGuid(), companyId: Guid.NewGuid(), name: "Acme Tenant");

        var act = () => tenant.AddUser(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
