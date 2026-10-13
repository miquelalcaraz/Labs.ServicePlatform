using Microsoft.Extensions.Logging;

using Sirius.Domain.SeedWork;
using Sirius.Services.Accounts.Application.Commands;
using Sirius.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Sirius.Services.Accounts.Domain.Repositories;

namespace Services.UnitTests.Accounts.Application.Commands;

public class CompleteActivationOnUserActivatedCommandHandlerTests
{
    private static readonly Guid AdminUserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_WhenProcessFound_CompletesActivationProcess()
    {
        var process = CreateProcessAtUserActivation();
        var (repo, _) = CreateMocks(process);
        var handler = CreateHandler(repo);

        var result = await handler.Handle(new CompleteActivationOnUserActivatedCommand(AdminUserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ActivationStatus.Completed);
        process.CurrentStep.Should().Be(ActivationStep.Completed);
        process.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenProcessFound_SavesChanges()
    {
        var process = CreateProcessAtUserActivation();
        var (repo, unitOfWork) = CreateMocks(process);
        var handler = CreateHandler(repo);

        await handler.Handle(new CompleteActivationOnUserActivatedCommand(AdminUserId), CancellationToken.None);

        repo.Verify(r => r.Update(process), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenProcessNotFound_ReturnsNotFound()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var repo = new Mock<ICustomerActivationProcessRepository>();
        repo.Setup(r => r.UnitOfWork).Returns(unitOfWork.Object);
        repo.Setup(r => r.GetByAdminUserIdAsync(It.IsAny<Guid>())).ReturnsAsync((CustomerActivationProcess?)null);

        var handler = CreateHandler(repo);

        var result = await handler.Handle(new CompleteActivationOnUserActivatedCommand(AdminUserId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProcessAlreadyCompleted_ReturnsSuccess()
    {
        var process = CreateCompletedProcess();
        var (repo, unitOfWork) = CreateMocks(process);
        var handler = CreateHandler(repo);

        var result = await handler.Handle(new CompleteActivationOnUserActivatedCommand(AdminUserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repo.Verify(r => r.Update(It.IsAny<CustomerActivationProcess>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDomainExceptionThrown_MarksProcessAsFailed()
    {
        var process = CreateProcessAtUserActivation();
        process.Fail("PriorError", "Forced failure for test");
        var (repo, unitOfWork) = CreateMocks(process);
        var handler = CreateHandler(repo);

        var result = await handler.Handle(new CompleteActivationOnUserActivatedCommand(AdminUserId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        process.Status.Should().Be(ActivationStatus.Failed);
        process.LastErrorCode.Should().Be("UserActivatedProcessingError");
        repo.Verify(r => r.Update(process), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CustomerActivationProcess CreateProcessAtUserActivation()
    {
        var process = CustomerActivationProcess.Create(
            subscriptionId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            ownerCompanyId: Guid.NewGuid(),
            adminUserId: AdminUserId,
            adminMembershipId: Guid.NewGuid());
        process.Start();
        process.RequestActivation();
        process.MarkActivationDelivered();
        return process;
    }

    private static CustomerActivationProcess CreateCompletedProcess()
    {
        var process = CreateProcessAtUserActivation();
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
        repo.Setup(r => r.GetByAdminUserIdAsync(AdminUserId)).ReturnsAsync(process);

        return (repo, unitOfWork);
    }

    private static CompleteActivationOnUserActivatedCommandHandler CreateHandler(
        Mock<ICustomerActivationProcessRepository> repo) =>
        new(repo.Object, new Mock<ILogger<CompleteActivationOnUserActivatedCommandHandler>>().Object);
}
