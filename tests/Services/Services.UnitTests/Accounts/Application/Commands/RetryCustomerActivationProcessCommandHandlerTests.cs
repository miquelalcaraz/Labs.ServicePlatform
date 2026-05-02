using Microsoft.Extensions.Logging;

using Qlt.Domain.SeedWork;
using Qlt.Services.Accounts.Application.Commands;
using Qlt.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Services.UnitTests.Accounts.Application.Commands;

public class RetryCustomerActivationProcessCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenProcessNotFound_ReturnsNotFound()
    {
        var (repo, unitOfWork) = CreateMocks();
        repo.Setup(r => r.GetAsync(It.IsAny<Guid>())).ReturnsAsync((CustomerActivationProcess?)null);

        var handler = CreateHandler(repo);
        var command = new RetryCustomerActivationProcessCommand(Guid.NewGuid(), requestId: Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenStatusIsNotFailed_ReturnsConflict()
    {
        var process = CreateProcessInProgress();
        var (repo, unitOfWork) = CreateMocks(process);

        var handler = CreateHandler(repo);
        var command = new RetryCustomerActivationProcessCommand(process.Id, requestId: Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenFailed_RetriesAndSaves()
    {
        var process = CreateFailedProcess();
        var (repo, unitOfWork) = CreateMocks(process);

        var handler = CreateHandler(repo);
        var command = new RetryCustomerActivationProcessCommand(process.Id, "Test retry", Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        process.RetryCount.Should().Be(1);
        process.Status.Should().Be(ActivationStatus.WaitingForNotificationDelivery);
        repo.Verify(r => r.Update(process), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenFailedAtLaterStep_RetriesWithoutRestart()
    {
        var process = CreateProcessFailedAtDelivered();
        var (repo, unitOfWork) = CreateMocks(process);

        var handler = CreateHandler(repo);
        var command = new RetryCustomerActivationProcessCommand(process.Id, requestId: Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        process.RetryCount.Should().Be(1);
        process.Status.Should().Be(ActivationStatus.InProgress);
        process.CurrentStep.Should().Be(ActivationStep.ActivationDelivered);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSaveFails_ReturnsFailure()
    {
        var process = CreateFailedProcess();
        var (repo, _) = CreateMocks(process, saveChangesReturn: 0);

        var handler = CreateHandler(repo);
        var command = new RetryCustomerActivationProcessCommand(process.Id, requestId: Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    private static CustomerActivationProcess CreateProcessInProgress()
    {
        var process = CustomerActivationProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        process.Start();
        process.RequestActivation();
        return process;
    }

    private static CustomerActivationProcess CreateFailedProcess()
    {
        var process = CreateProcessInProgress();
        process.Fail("TestError", "Something went wrong");
        return process;
    }

    private static CustomerActivationProcess CreateProcessFailedAtDelivered()
    {
        var process = CustomerActivationProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        process.Start();
        process.RequestActivation();
        process.MarkActivationDelivered();
        process.Fail("TestError", "Failed after delivery");
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

    private static RetryCustomerActivationProcessCommandHandler CreateHandler(
        Mock<ICustomerActivationProcessRepository> repo) =>
        new(repo.Object, new Mock<ILogger<RetryCustomerActivationProcessCommandHandler>>().Object);
}
