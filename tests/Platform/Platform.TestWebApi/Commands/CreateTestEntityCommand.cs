using Platform.TestWebApi.Infrastructure;

using Qlt.Platform;
using Qlt.Platform.Messaging.Commands;

namespace Platform.TestWebApi.Commands;

public record CreateTestEntityCommand(string Name, int Value, Guid RequestId) : IIdempotentCommand<int>
{
    Result<int> IIdempotentCommand<int>.CreateResultForDuplicateRequest()
    {
        return Result.Success(Value);
    }
}

public class CreateTestEntityCommandHandler : ICommandHandler<CreateTestEntityCommand, int>
{
    private readonly TestDbContext _context;

    public CreateTestEntityCommandHandler(TestDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Result<int>> Handle(CreateTestEntityCommand command, CancellationToken cancellationToken)
    {
        var entity = new TestEntity(command.Name, command.Value);

        _context.TestEntities.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.Value);
    }
}
