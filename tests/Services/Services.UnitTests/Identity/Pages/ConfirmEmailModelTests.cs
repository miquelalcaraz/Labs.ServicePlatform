using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

using Qlt.Contracts.Identity.IntegrationEvents;
using Qlt.Infrastructure.Identity.Commands;
using Qlt.Infrastructure.Identity.Models;
using Qlt.Platform.Messaging.Services;

namespace Services.UnitTests.Identity.Pages;

public class ConfirmEmailModelTests : IdentityTestBase
{
    // ── Error cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNonExistentUser_SetsErrorStatus()
    {
        var (userManager, outbox) = CreateMocks();
        userManager.Setup(m => m.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        var handler = CreateHandler(userManager, outbox);

        var result = await handler.Handle(new ConfirmEmailCommand(Guid.NewGuid().ToString()), default);

        result.Value.Status.Should().Be(ConfirmEmailStatus.Error);
        outbox.Verify(o => o.AddAndSaveEventAsync(It.IsAny<UserActivatedIntegrationEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAlreadyActiveUser_SetsAlreadyConfirmedStatus()
    {
        var user = CreateActiveUser();
        var (userManager, outbox) = CreateMocks();
        userManager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        var handler = CreateHandler(userManager, outbox);

        var result = await handler.Handle(new ConfirmEmailCommand(user.Id), default);

        result.Value.Status.Should().Be(ConfirmEmailStatus.AlreadyConfirmed);
        result.Value.UserEmail.Should().Be(user.Email);
        outbox.Verify(o => o.AddAndSaveEventAsync(It.IsAny<UserActivatedIntegrationEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUpdateFails_SetsErrorStatus()
    {
        var user = CreatePendingUser();
        var (userManager, outbox) = CreateMocks();
        userManager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        userManager.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "DB error" }));
        var handler = CreateHandler(userManager, outbox);

        var result = await handler.Handle(new ConfirmEmailCommand(user.Id), default);

        result.Value.Status.Should().Be(ConfirmEmailStatus.Error);
        outbox.Verify(o => o.AddAndSaveEventAsync(It.IsAny<UserActivatedIntegrationEvent>()), Times.Never);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidPendingUser_ActivatesUserAndPublishesEvent()
    {
        var user = CreatePendingUser();
        var (userManager, outbox) = CreateMocks();
        userManager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        userManager.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        var handler = CreateHandler(userManager, outbox);

        var result = await handler.Handle(new ConfirmEmailCommand(user.Id), default);

        result.Value.Status.Should().Be(ConfirmEmailStatus.Success);
        result.Value.UserEmail.Should().Be(user.Email);

        userManager.Verify(m => m.UpdateAsync(It.Is<ApplicationUser>(u =>
            u.Status == UserStatus.Active &&
            u.EmailConfirmed == true)),
            Times.Once);

        outbox.Verify(o => o.AddAndSaveEventAsync(It.Is<UserActivatedIntegrationEvent>(e =>
            e.UserId == Guid.Parse(user.Id) &&
            e.Email == user.Email &&
            e.TenantId == user.TenantId)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidPendingUser_SetsEmailConfirmedTrue()
    {
        var user = CreatePendingUser();
        var (userManager, outbox) = CreateMocks();
        userManager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        userManager.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        var handler = CreateHandler(userManager, outbox);

        await handler.Handle(new ConfirmEmailCommand(user.Id), default);

        user.EmailConfirmed.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (Mock<UserManager<ApplicationUser>> UserManager, Mock<IEventBusOutboxService> Outbox) CreateMocks()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var outbox = new Mock<IEventBusOutboxService>();
        outbox.Setup(o => o.AddAndSaveEventAsync(It.IsAny<UserActivatedIntegrationEvent>()))
              .Returns(Task.CompletedTask);
        return (userManager, outbox);
    }

    private static ConfirmEmailCommandHandler CreateHandler(
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<IEventBusOutboxService> outbox) =>
        new(userManager.Object, outbox.Object, Mock.Of<ILogger<ConfirmEmailCommandHandler>>());

    private static ApplicationUser CreatePendingUser() =>
        ApplicationUser.CreateAsUnConfirmed(
            userName: "admin@test.com",
            email: "admin@test.com",
            tenantId: Guid.NewGuid());

    private static ApplicationUser CreateActiveUser()
    {
        var user = ApplicationUser.CreateAsUnConfirmed(
            userName: "active@test.com",
            email: "active@test.com",
            tenantId: Guid.NewGuid());
        user.Activate();
        return user;
    }
}
