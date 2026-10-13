using Microsoft.EntityFrameworkCore;

using Platform.TestWebApi.Infrastructure;

using Miqa.Platform;
using Miqa.Platform.Messaging.Commands;

namespace Platform.TestWebApi.Commands;

public record UpdateTestEntityCommand(int EntityId, int NewValue, Guid RequestId) : IIdempotentCommand<bool>
{
    Result<bool> IIdempotentCommand<bool>.CreateResultForDuplicateRequest()
    {
        return Result.Success(true);
    }
}

public class UpdateTestEntityCommandHandler : ICommandHandler<UpdateTestEntityCommand, bool>
{
    private readonly TestDbContext _context;

    public UpdateTestEntityCommandHandler(TestDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Result<bool>> Handle(UpdateTestEntityCommand command, CancellationToken cancellationToken)
    {
        var entity = await _context.TestEntities
            .FirstOrDefaultAsync(e => e.Id.GetHashCode() == command.EntityId, cancellationToken);

        if (entity is null)
            return Result.Failure<bool>(Error.NotFound.WithMessage("Entity not found"));

        entity.UpdateValue(command.NewValue);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
