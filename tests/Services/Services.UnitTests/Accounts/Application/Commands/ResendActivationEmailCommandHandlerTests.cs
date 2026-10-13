using Microsoft.Extensions.Logging;

using Sirius.Domain.SeedWork;
using Sirius.Services.Accounts.Application.Commands;
using Sirius.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Sirius.Services.Accounts.Domain.Repositories;

namespace Services.UnitTests.Accounts.Application.Commands;

public class ResendActivationEmailCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenProcessNotFound_ReturnsNotFound()
    {
        var (repo, unitOfWork) = CreateMocks();
        repo.Setup(r => r.GetAsync(It.IsAny<Guid>())).ReturnsAsync((CustomerActivationProcess?)null);

        var handler = CreateHandler(repo);
        var command = new ResendActivationEmailCommand(Guid.NewGuid(), Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProcessIsCompleted_ReturnsConflict()
    {
        var process = CreateCompletedProcess();
        var (repo, unitOfWork) = CreateMocks(process);

        var handler = CreateHandler(repo);
        var command = new ResendActivationEmailCommand(process.Id, Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProcessInInvalidStep_ReturnsConflict()
    {
        // Process is WaitingForNotificationDelivery - RequestActivation requires InProgress+None step
        var process = CreateProcessWaitingForDelivery();
        var (repo, _) = CreateMocks(process);

        var handler = CreateHandler(repo);
        var command = new ResendActivationEmailCommand(process.Id, Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenSaveFails_ReturnsFailure()
    {
        var process = CreateProcessReadyForResend();
        var (repo, _) = CreateMocks(process, saveChangesReturn: 0);

        var handler = CreateHandler(repo);
        var command = new ResendActivationEmailCommand(process.Id, Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    private static CustomerActivationProcess CreateProcessWaitingForDelivery()
    {
        var process = CustomerActivationProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        process.Start();
        process.RequestActivation();
        return process;
    }

    private static CustomerActivationProcess CreateProcessReadyForResend()
    {
        // A process that's InProgress and at step None — this is only possible
        // immediately after Start() before RequestActivation().
        var process = CustomerActivationProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        process.Start();
        return process;
    }

    private static CustomerActivationProcess CreateCompletedProcess()
    {
        var process = CustomerActivationProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        process.Start();
        process.RequestActivation();
        process.MarkActivationDelivered();
        process.MarkUserActivated(process.AdminUserId);
        process.MarkCustomerReady();
        process.Complete();
        return process;
    }

    private static (Mock<ICustomerActivationProcessRepository> Repo, Mock<IUnitOfWork> UnitOfWork) CreateMocks(
        CustomerActivationProcess? process = null, int saveChangesReturn = 1)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(saveChangesReturn);

        var repo = new Mock<ICustomerActivationProcessRepository>();
        repo.Setup(r => r.UnitOfWork).Returns(unitOfWork.Object);

        if (process is not null)
            repo.Setup(r => r.GetAsync(process.Id)).ReturnsAsync(process);

        return (repo, unitOfWork);
    }

    private static ResendActivationEmailCommandHandler CreateHandler(
        Mock<ICustomerActivationProcessRepository> repo) =>
        new(repo.Object, new Mock<ILogger<ResendActivationEmailCommandHandler>>().Object);
}
