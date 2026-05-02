using Microsoft.Extensions.Logging;

using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Platform.Messaging.Models;
using Qlt.Platform.Messaging.Services;

namespace Platform.UnitTests.Messaging;

public class IdempotencyIntegrationEventDecoratorTests
{
    private readonly Mock<IIntegrationEventHandler<TestEvent>> _mockInner;
    private readonly Mock<IIdempotencyService> _mockIdempotency;
    private readonly Mock<ILogger> _mockLogger;

    public IdempotencyIntegrationEventDecoratorTests()
    {
        _mockInner = new Mock<IIntegrationEventHandler<TestEvent>>();
        _mockIdempotency = new Mock<IIdempotencyService>();
        _mockLogger = new Mock<ILogger>();
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

        var decorator = CreateDecorator();

        await decorator.Handle(@event);

        _mockIdempotency.Verify(s => s.MarkAsProcessingAsync(
            It.Is<IdempotencyKey>(k => k.Id == @event.Id && k.Scope == IdempotencyScope.Event),
            It.IsAny<string>()), Times.Once);
        _mockIdempotency.Verify(s => s.MarkAsCompletedAsync(
            It.Is<IdempotencyKey>(k => k.Id == @event.Id)), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenHandlerThrows_DoesNotMarkAsProcessed()
    {
        var @event = new TestEvent();
        _mockIdempotency.Setup(s => s.ExistsAsync(It.IsAny<IdempotencyKey>()))
            .ReturnsAsync(false);
        _mockInner.Setup(h => h.Handle(It.IsAny<TestEvent>()))
            .ThrowsAsync(new InvalidOperationException("Handler failed"));

        var decorator = CreateDecorator();

        var act = () => decorator.Handle(@event);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _mockIdempotency.Verify(s => s.MarkAsProcessingAsync(
            It.IsAny<IdempotencyKey>(), It.IsAny<string>()), Times.Never);
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

    private IdempotencyIntegrationEventDecorator<TestEvent> CreateDecorator()
    {
        return new IdempotencyIntegrationEventDecorator<TestEvent>(
            _mockInner.Object,
            _mockIdempotency.Object,
            _mockLogger.Object);
    }

    public record TestEvent : IntegrationEvent;
}
