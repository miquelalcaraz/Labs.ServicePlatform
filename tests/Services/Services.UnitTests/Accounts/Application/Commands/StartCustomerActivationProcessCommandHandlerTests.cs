using Microsoft.Extensions.Logging;

using Miqa.Domain.SeedWork;
using Miqa.Services.Accounts.Application.Commands;
using Miqa.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Miqa.Services.Accounts.Domain.Repositories;

namespace Services.UnitTests.Accounts.Application.Commands;

public class StartCustomerActivationProcessCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenNoExistingProcess_CreatesAndStartsProcess()
    {
        var (repo, unitOfWork) = CreateMocks();
        repo.Setup(r => r.GetBySubscriptionIdAsync(It.IsAny<Guid>())).ReturnsAsync((CustomerActivationProcess?)null);

        var handler = CreateHandler(repo);
        var command = CreateValidCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        repo.Verify(r => r.Add(It.Is<CustomerActivationProcess>(p =>
            p.SubscriptionId == command.SubscriptionId &&
            p.TenantId == command.TenantId &&
            p.Status == ActivationStatus.WaitingForNotificationDelivery &&
            p.CurrentStep == ActivationStep.ActivationRequested)), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenProcessAlreadyExists_ReturnsSuccessWithoutCreating()
    {
        var existing = CustomerActivationProcess.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        existing.Start();
        existing.RequestActivation();

        var (repo, unitOfWork) = CreateMocks();
        repo.Setup(r => r.GetBySubscriptionIdAsync(existing.SubscriptionId)).ReturnsAsync(existing);

        var handler = CreateHandler(repo);
        var command = new StartCustomerActivationProcessCommand(
            existing.SubscriptionId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), requestId: Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repo.Verify(r => r.Add(It.IsAny<CustomerActivationProcess>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSaveReturnsFailed_ReturnsFailure()
    {
        var (repo, _) = CreateMocks(saveChangesReturn: 0);
        repo.Setup(r => r.GetBySubscriptionIdAsync(It.IsAny<Guid>())).ReturnsAsync((CustomerActivationProcess?)null);

        var handler = CreateHandler(repo);
        var command = CreateValidCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SetsCorrectMetadataOnProcess()
    {
        var (repo, _) = CreateMocks();
        repo.Setup(r => r.GetBySubscriptionIdAsync(It.IsAny<Guid>())).ReturnsAsync((CustomerActivationProcess?)null);
        CustomerActivationProcess? captured = null;
        repo.Setup(r => r.Add(It.IsAny<CustomerActivationProcess>()))
            .Callback<CustomerActivationProcess>(p => captured = p)
            .Returns<CustomerActivationProcess>(p => p);

        var handler = CreateHandler(repo);
        var command = CreateValidCommand();

        await handler.Handle(command, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.CompanyLegalName.Should().Be("Acme Corp");
        captured.SubscriptionPlanCode.Should().Be("STANDARD");
        captured.AdminEmail.Should().Be("admin@acme.com");
    }

    private static StartCustomerActivationProcessCommand CreateValidCommand() =>
        new(
            subscriptionId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            ownerCompanyId: Guid.NewGuid(),
            adminUserId: Guid.NewGuid(),
            adminMembershipId: Guid.NewGuid(),
            companyLegalName: "Acme Corp",
            subscriptionPlanCode: "STANDARD",
            adminEmail: "admin@acme.com",
            requestId: Guid.NewGuid());

    private static (Mock<ICustomerActivationProcessRepository> Repo, Mock<IUnitOfWork> UnitOfWork) CreateMocks(
        int saveChangesReturn = 1)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(saveChangesReturn);

        var repo = new Mock<ICustomerActivationProcessRepository>();
        repo.Setup(r => r.UnitOfWork).Returns(unitOfWork.Object);
        repo.Setup(r => r.Add(It.IsAny<CustomerActivationProcess>()))
            .Returns<CustomerActivationProcess>(p => p);

        return (repo, unitOfWork);
    }

    private static StartCustomerActivationProcessCommandHandler CreateHandler(
        Mock<ICustomerActivationProcessRepository> repo) =>
        new(repo.Object, new Mock<ILogger<StartCustomerActivationProcessCommandHandler>>().Object);
}
