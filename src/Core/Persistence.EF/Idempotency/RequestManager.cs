using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using QltSystem.Core.EventBus.Idempotency;

namespace QltSystem.Core.Persistence.EF.Idempotency;

/// <summary>
/// Generic implementation of request manager for command idempotency.
/// Uses Entity Framework to track processed requests in the database.
/// </summary>
/// <typeparam name="TContext">The DbContext type used to persist client requests</typeparam>
public class RequestManager<TContext> : IRequestManager
    where TContext : DBContextBase
{
    private readonly TContext _context;
    private readonly ILogger<RequestManager<TContext>> _logger;

    public RequestManager(TContext context, ILogger<RequestManager<TContext>> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> ExistAsync(Guid id)
    {
        var request = await _context.FindAsync<ClientRequest>(id);
        return request != null;
    }

    public async Task CreateRequestForCommandAsync<T>(Guid id)
    {
        try
        {
            var request = new ClientRequest
            {
                Id = id,
                Name = typeof(T).Name,
                Time = DateTime.UtcNow
            };

            _context.Add(request);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created request entry for command {CommandName} with RequestId {RequestId}",
                typeof(T).Name,
                id);
        }
        catch (DbUpdateException ex)
        {
            // Check if it's a unique constraint violation (duplicate key)
            // This is the expected behavior for idempotency - the request already exists
            if (_context.IsUniqueConstraintViolation(ex))
            {
                _logger.LogWarning(
                    "Duplicate request {RequestId} for command {CommandName} detected and ignored (idempotency enforced by database)",
                    id,
                    typeof(T).Name);

                // Don't throw - this is expected for duplicate requests
                return;
            }

            // If it's a different kind of DbUpdateException, rethrow
            _logger.LogError(ex, "Database error creating request entry for command {CommandName} with RequestId {RequestId}", typeof(T).Name, id);
            throw;
        }
    }

}
