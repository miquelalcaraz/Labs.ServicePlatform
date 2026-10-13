using MediatR;

using Microsoft.Extensions.Logging.Abstractions;

using Sirius.Platform;
using Sirius.Platform.Messaging.Behaviors;
using Sirius.Platform.Messaging.Commands;
using Sirius.Platform.Messaging.Models;
using Sirius.Platform.Messaging.Services;

namespace Platform.UnitTests.Behaviors;

public class IdempotencyBehaviorTests
{
    public record TestIdempotentCommand(Guid RequestId) : IRequest<Result<bool>>, IIdempotentRequest
    {
        public object CreateDuplicateResult() => Result.Success(true);
    }

    private readonly Mock<IIdempotencyService> _idempotencyService = new();

    [Fact]
    public async Task Handle_WhenDuplicate_ReturnsDuplicateResultWithoutCallingHandler()
    {
        var command = new TestIdempotentCommand(Guid.NewGuid());
        _idempotencyService.Setup(s => s.ExistsAsync(It.IsAny<IdempotencyKey>())).ReturnsAsync(true);
        var handlerCalled = false;

        var result = await CreateBehavior().Handle(
            command,
            _ =>
            {
                handlerCalled = true;
                return Task.FromResult(Result.Success(true));
            },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        handlerCalled.Should().BeFalse();
        _idempotencyService.Verify(s => s.MarkAsProcessingAsync(It.IsAny<IdempotencyKey>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenResultSuccess_MarksAsCompleted()
    {
        var command = new TestIdempotentCommand(Guid.NewGuid());
        var key = IdempotencyKey.ForRequest(command.RequestId);

        await CreateBehavior().Handle(command, _ => Task.FromResult(Result.Success(true)), CancellationToken.None);

        _idempotencyService.Verify(s => s.MarkAsProcessingAsync(key, nameof(TestIdempotentCommand)), Times.Once);
        _idempotencyService.Verify(s => s.MarkAsCompletedAsync(key), Times.Once);
        _idempotencyService.Verify(s => s.DiscardAsync(It.IsAny<IdempotencyKey>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenResultFailure_DiscardsKeyAndReturnsFailure()
    {
        var command = new TestIdempotentCommand(Guid.NewGuid());
        var key = IdempotencyKey.ForRequest(command.RequestId);
        var failure = Result.Failure<bool>(new Error("account.not_found", "Not found"));

        var result = await CreateBehavior().Handle(command, _ => Task.FromResult(failure), CancellationToken.None);

        result.Should().Be(failure);
        _idempotencyService.Verify(s => s.DiscardAsync(key), Times.Once);
        _idempotencyService.Verify(s => s.MarkAsCompletedAsync(It.IsAny<IdempotencyKey>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNoIdempotencyService_CallsHandler()
    {
        var behavior = new IdempotencyBehavior<TestIdempotentCommand, Result<bool>>(
            NullLogger<IdempotencyBehavior<TestIdempotentCommand, Result<bool>>>.Instance);

        var result = await behavior.Handle(
            new TestIdempotentCommand(Guid.NewGuid()),
            _ => Task.FromResult(Result.Success(true)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    private IdempotencyBehavior<TestIdempotentCommand, Result<bool>> CreateBehavior() =>
        new(NullLogger<IdempotencyBehavior<TestIdempotentCommand, Result<bool>>>.Instance, _idempotencyService.Object);
}
