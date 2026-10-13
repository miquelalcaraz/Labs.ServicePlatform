using Microsoft.Extensions.Logging;

using Sirius.Platform.Messaging.IntegrationEvents;
using Sirius.Platform.Messaging.Models;
using Sirius.Platform.Messaging.Services;
using Sirius.Platform.Persistence;

namespace Platform.UnitTests.Messaging.EntityFramework;

public class IdempotencyIntegrationEventEfDecoratorTests
{
    private readonly Mock<IIntegrationEventHandler<TestEvent>> _mockInner;
    private readonly Mock<IIdempotencyService> _mockIdempotency;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<ITransactionalUnitOfWork> _mockUnitOfWork;

    public IdempotencyIntegrationEventEfDecoratorTests()
    {
        _mockInner = new Mock<IIntegrationEventHandler<TestEvent>>();
        _mockIdempotency = new Mock<IIdempotencyService>();
        _mockLogger = new Mock<ILogger>();
        _mockUnitOfWork = new Mock<ITransactionalUnitOfWork>();
    }

    [Fact]
    public async Task Handle_WhenDuplicate_SkipsHandler()
    {
        var @event = new TestEvent();
        _mockIdempotency.Setup(s => s.ExistsAsync(It.IsAny<IdempotencyKey>()))
            .ReturnsAsync(true);

        var decorator = CreateDecorator();

        await decorator.Handle(@event);

        _mockInner.Verify(h => h.Handle(It.IsAny<TestEvent>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNew_ExecutesHandler()
    {
        var @event = new TestEvent();
        _mockIdempotency.Setup(s => s.ExistsAsync(It.IsAny<IdempotencyKey>()))
            .ReturnsAsync(false);
        _mockUnitOfWork.Setup(u => u.HasActiveTransaction).Returns(false);
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync())
            .ReturnsAsync(Mock.Of<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>());

        var decorator = CreateDecorator();

        await decorator.Handle(@event);

        _mockInner.Verify(h => h.Handle(@event), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNew_MarksAsProcessedAndCompleted()
    {
        var @event = new TestEvent();
        _mockIdempotency.Setup(s => s.ExistsAsync(It.IsAny<IdempotencyKey>()))
            .ReturnsAsync(false);
        _mockUnitOfWork.Setup(u => u.HasActiveTransaction).Returns(false);
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync())
            .ReturnsAsync(Mock.Of<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>());

        var decorator = CreateDecorator();

        await decorator.Handle(@event);

        _mockIdempotency.Verify(s => s.MarkAsProcessingAsync(
            It.Is<IdempotencyKey>(k => k.Id == @event.Id && k.Scope == IdempotencyScope.Event),
            It.IsAny<string>()), Times.Once);
        _mockIdempotency.Verify(s => s.MarkAsCompletedAsync(
            It.Is<IdempotencyKey>(k => k.Id == @event.Id)), Times.Once);
    }

    [Fact]
    public async Task Handle_WithTransaction_CommitsOnSuccess()
    {
        var @event = new TestEvent();
        var mockTransaction = new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>();

        _mockIdempotency.Setup(s => s.ExistsAsync(It.IsAny<IdempotencyKey>()))
            .ReturnsAsync(false);
        _mockUnitOfWork.Setup(u => u.HasActiveTransaction).Returns(false);
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync())
            .ReturnsAsync(mockTransaction.Object);

        var decorator = CreateDecorator();

        await decorator.Handle(@event);

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(mockTransaction.Object, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenHandlerThrows_RollsBackAndRethrows()
    {
        var @event = new TestEvent();
        _mockIdempotency.Setup(s => s.ExistsAsync(It.IsAny<IdempotencyKey>()))
            .ReturnsAsync(false);
        _mockUnitOfWork.Setup(u => u.HasActiveTransaction).Returns(false);
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync())
            .ReturnsAsync(Mock.Of<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>());
        _mockUnitOfWork.SetupSequence(u => u.HasActiveTransaction)
            .Returns(false)
            .Returns(true);
        _mockInner.Setup(h => h.Handle(It.IsAny<TestEvent>()))
            .ThrowsAsync(new InvalidOperationException("Handler failed"));

        var decorator = CreateDecorator();

        var act = () => decorator.Handle(@event);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Handler failed");
        _mockUnitOfWork.Verify(u => u.RollbackTransaction(), Times.Once);
        _mockIdempotency.Verify(s => s.MarkAsProcessingAsync(
            It.IsAny<IdempotencyKey>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithExistingTransaction_DoesNotCreateNew()
    {
        var @event = new TestEvent();
        _mockIdempotency.Setup(s => s.ExistsAsync(It.IsAny<IdempotencyKey>()))
            .ReturnsAsync(false);
        _mockUnitOfWork.Setup(u => u.HasActiveTransaction).Returns(true);

        var decorator = CreateDecorator();

        await decorator.Handle(@event);

        _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Never);
        _mockInner.Verify(h => h.Handle(@event), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutUnitOfWork_ProcessesWithoutTransaction()
    {
        var @event = new TestEvent();
        _mockIdempotency.Setup(s => s.ExistsAsync(It.IsAny<IdempotencyKey>()))
            .ReturnsAsync(false);

        var decorator = new IdempotencyIntegrationEventEfDecorator<TestEvent>(
            _mockInner.Object,
            _mockIdempotency.Object,
            _mockLogger.Object,
            unitOfWork: null);

        await decorator.Handle(@event);

        _mockInner.Verify(h => h.Handle(@event), Times.Once);
        _mockIdempotency.Verify(s => s.MarkAsProcessingAsync(
            It.IsAny<IdempotencyKey>(), It.IsAny<string>()), Times.Once);
        _mockIdempotency.Verify(s => s.MarkAsCompletedAsync(
            It.IsAny<IdempotencyKey>()), Times.Once);
    }

    [Fact]
    public void InnerHandler_ReturnsWrappedHandler()
    {
        var decorator = CreateDecorator();

        var inner = ((IDecoratedHandler<TestEvent>)decorator).InnerHandler;

        inner.Should().BeSameAs(_mockInner.Object);
    }

    [Fact]
    public async Task Handle_UsesEventScope_WithHandlerNameAsQualifier()
    {
        var @event = new TestEvent();
        IdempotencyKey capturedKey = default;
        _mockIdempotency.Setup(s => s.ExistsAsync(It.IsAny<IdempotencyKey>()))
            .Callback<IdempotencyKey>(k => capturedKey = k)
            .ReturnsAsync(true);

        var decorator = CreateDecorator();

        await decorator.Handle(@event);

        capturedKey.Scope.Should().Be(IdempotencyScope.Event);
        capturedKey.Id.Should().Be(@event.Id);
        capturedKey.Qualifier.Should().NotBeNullOrEmpty();
    }

    private IdempotencyIntegrationEventEfDecorator<TestEvent> CreateDecorator()
    {
        return new IdempotencyIntegrationEventEfDecorator<TestEvent>(
            _mockInner.Object,
            _mockIdempotency.Object,
            _mockLogger.Object,
            _mockUnitOfWork.Object);
    }

    public record TestEvent : IntegrationEvent;
}
