using Microsoft.Extensions.Logging;

using Sirius.Domain.SeedWork;
using Sirius.Services.Accounts.Application.Commands;
using Sirius.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Sirius.Services.Accounts.Domain.Repositories;

namespace Services.UnitTests.Accounts.Application.Commands;

public class ConfirmActivationDeliveryCommandHandlerTests
{
    private static readonly Guid AdminUserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_WhenProcessExists_AdvancesToWaitingForUserActivation()
    {
        var process = CreateProcessAtNotificationDelivery();
        var (repo, _) = CreateMocks(process);
        var handler = CreateHandler(repo);

        var result = await handler.Handle(new ConfirmActivationDeliveryCommand(process.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ActivationStatus.WaitingForUserActivation);
        process.CurrentStep.Should().Be(ActivationStep.ActivationDelivered);
        repo.Verify(r => r.Update(process), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenProcessExists_SavesChanges()
    {
        var process = CreateProcessAtNotificationDelivery();
        var (repo, unitOfWork) = CreateMocks(process);
        var handler = CreateHandler(repo);

        await handler.Handle(new ConfirmActivationDeliveryCommand(process.Id), CancellationToken.None);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenProcessNotFound_ReturnsNotFound()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var repo = new Mock<ICustomerActivationProcessRepository>();
        repo.Setup(r => r.UnitOfWork).Returns(unitOfWork.Object);
        repo.Setup(r => r.GetAsync(It.IsAny<Guid>())).ReturnsAsync((CustomerActivationProcess?)null);

        var handler = CreateHandler(repo);

        var result = await handler.Handle(new ConfirmActivationDeliveryCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProcessAlreadyCompleted_ReturnsSuccess()
    {
        var process = CreateCompletedProcess();
        var (repo, unitOfWork) = CreateMocks(process);
        var handler = CreateHandler(repo);

        var result = await handler.Handle(new ConfirmActivationDeliveryCommand(process.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repo.Verify(r => r.Update(It.IsAny<CustomerActivationProcess>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDomainExceptionThrown_MarksProcessAsFailed()
    {
        var process = CreateProcessAtNotificationDelivery();
        process.Fail("PriorError", "Forced failure for test");
        var (repo, unitOfWork) = CreateMocks(process);
        var handler = CreateHandler(repo);

        var result = await handler.Handle(new ConfirmActivationDeliveryCommand(process.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        process.Status.Should().Be(ActivationStatus.Failed);
        process.LastErrorCode.Should().Be("ActivationDeliveredProcessingError");
        repo.Verify(r => r.Update(process), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CustomerActivationProcess CreateProcessAtNotificationDelivery()
    {
        var process = CustomerActivationProcess.Create(
            subscriptionId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            ownerCompanyId: Guid.NewGuid(),
            adminUserId: AdminUserId,
            adminMembershipId: Guid.NewGuid());
        process.Start();
        process.RequestActivation();
        return process;
    }

    private static CustomerActivationProcess CreateCompletedProcess()
    {
        var process = CreateProcessAtNotificationDelivery();
        process.MarkActivationDelivered();
        process.MarkUserActivated(AdminUserId);
        process.MarkCustomerReady();
        process.Complete();
        return process;
    }

    private static (Mock<ICustomerActivationProcessRepository> Repo, Mock<IUnitOfWork> UnitOfWork) CreateMocks(
        CustomerActivationProcess process)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var repo = new Mock<ICustomerActivationProcessRepository>();
        repo.Setup(r => r.UnitOfWork).Returns(unitOfWork.Object);
        repo.Setup(r => r.GetAsync(process.Id)).ReturnsAsync(process);

        return (repo, unitOfWork);
    }

    private static ConfirmActivationDeliveryCommandHandler CreateHandler(
        Mock<ICustomerActivationProcessRepository> repo) =>
        new(repo.Object, new Mock<ILogger<ConfirmActivationDeliveryCommandHandler>>().Object);
}
