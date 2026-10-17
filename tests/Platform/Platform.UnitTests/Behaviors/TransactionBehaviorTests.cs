using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;

using Sirius.Platform;
using Sirius.Platform.Messaging.Behaviors;
using Sirius.Platform.Messaging.Commands;
using Sirius.Platform.Persistence;

namespace Platform.UnitTests.Behaviors;

public class TransactionBehaviorTests
{
    public record TestCommand(string Name) : IRequest<Result>, ICommandRequest;
    public record TestCommandOfT(string Name) : IRequest<Result<bool>>, ICommandRequest;
    public record TestNonResultCommand(string Name) : IRequest<string>, ICommandRequest;
    public record TestQuery(string Name) : IRequest<Result>;

    private readonly Mock<IDbContextTransaction> _transaction = new();
    private readonly Mock<ITransactionalUnitOfWork> _unitOfWork = new();

    public TransactionBehaviorTests()
    {
        _transaction.Setup(t => t.TransactionId).Returns(Guid.NewGuid());
        _transaction.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _transaction.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);

        _unitOfWork.Setup(u => u.HasActiveTransaction).Returns(false);
        _unitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(_transaction.Object);
        _unitOfWork.Setup(u => u.CommitTransactionAsync(It.IsAny<IDbContextTransaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.CreateExecutionStrategy()).Returns(new PassThroughExecutionStrategy());
    }

    [Fact]
    public async Task Handle_NonCommandRequest_PassesThroughWithoutTransaction()
    {
        var behavior = CreateBehavior<TestQuery, Result>();
        var expectedResult = Result.Success();

        var result = await behavior.Handle(new TestQuery("q"), _ => Task.FromResult(expectedResult), CancellationToken.None);

        result.Should().Be(expectedResult);
        _unitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenActiveTransaction_DoesNotBeginOrCommit()
    {
        _unitOfWork.Setup(u => u.HasActiveTransaction).Returns(true);
        var behavior = CreateBehavior<TestCommand, Result>();

        var result = await behavior.Handle(
            new TestCommand("nested"),
            _ => Task.FromResult(Result.Failure(new Error("nested.failed", "Nested failure"))),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _unitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Never);
        VerifyCommitted(Times.Never());
        _transaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenResultSuccess_Commits()
    {
        var behavior = CreateBehavior<TestCommand, Result>();

        var result = await behavior.Handle(new TestCommand("ok"), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        VerifyCommitted(Times.Once());
        _transaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenResultOfTSuccess_Commits()
    {
        var behavior = CreateBehavior<TestCommandOfT, Result<bool>>();

        var result = await behavior.Handle(new TestCommandOfT("ok"), _ => Task.FromResult(Result.Success(true)), CancellationToken.None);

        result.Value.Should().BeTrue();
        VerifyCommitted(Times.Once());
    }

    [Fact]
    public async Task Handle_WhenResultFailure_RollsBackAndDoesNotCommit()
    {
        var behavior = CreateBehavior<TestCommand, Result>();
        var failure = Result.Failure(new Error("already.exists", "Already exists"));

        var result = await behavior.Handle(new TestCommand("fail"), _ => Task.FromResult(failure), CancellationToken.None);

        result.Should().Be(failure);
        _transaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        VerifyCommitted(Times.Never());
    }

    [Fact]
    public async Task Handle_WhenResultOfTFailure_RollsBackAndDoesNotCommit()
    {
        var behavior = CreateBehavior<TestCommandOfT, Result<bool>>();
        var failure = Result.Failure<bool>(new Error("item.not_found", "Not found"));

        var result = await behavior.Handle(new TestCommandOfT("fail"), _ => Task.FromResult(failure), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _transaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        VerifyCommitted(Times.Never());
    }

    [Fact]
    public async Task Handle_WhenValidationFailure_RollsBackAndDoesNotCommit()
    {
        var behavior = CreateBehavior<TestCommandOfT, Result<bool>>();
        var failure = Result.ValidationFailure<bool>(new Dictionary<string, string[]> { ["Name"] = ["Required"] });

        var result = await behavior.Handle(new TestCommandOfT(""), _ => Task.FromResult(failure), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _transaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        VerifyCommitted(Times.Never());
    }

    [Fact]
    public async Task Handle_WhenResponseIsNotResult_Commits()
    {
        var behavior = CreateBehavior<TestNonResultCommand, string>();

        var result = await behavior.Handle(new TestNonResultCommand("x"), _ => Task.FromResult("done"), CancellationToken.None);

        result.Should().Be("done");
        VerifyCommitted(Times.Once());
    }

    [Fact]
    public async Task Handle_WhenHandlerThrows_RethrowsAndDoesNotCommit()
    {
        var behavior = CreateBehavior<TestCommand, Result>();

        var act = () => behavior.Handle(
            new TestCommand("throw"),
            _ => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        VerifyCommitted(Times.Never());
        _transaction.Verify(t => t.DisposeAsync(), Times.Once);
    }

    private TransactionBehavior<TRequest, TResponse> CreateBehavior<TRequest, TResponse>()
        where TRequest : IRequest<TResponse>
    {
        return new TransactionBehavior<TRequest, TResponse>(
            _unitOfWork.Object,
            NullLogger<TransactionBehavior<TRequest, TResponse>>.Instance);
    }

    private void VerifyCommitted(Times times) =>
        _unitOfWork.Verify(u => u.CommitTransactionAsync(It.IsAny<IDbContextTransaction>(), It.IsAny<CancellationToken>()), times);

    private sealed class PassThroughExecutionStrategy : IExecutionStrategy
    {
        public bool RetriesOnFailure => false;

        public TResult Execute<TState, TResult>(
            TState state,
            Func<DbContext, TState, TResult> operation,
            Func<DbContext, TState, ExecutionResult<TResult>>? verifySucceeded)
            => operation(null!, state);

        public Task<TResult> ExecuteAsync<TState, TResult>(
            TState state,
            Func<DbContext, TState, CancellationToken, Task<TResult>> operation,
            Func<DbContext, TState, CancellationToken, Task<ExecutionResult<TResult>>>? verifySucceeded,
            CancellationToken cancellationToken = default)
            => operation(null!, state, cancellationToken);
    }
}
