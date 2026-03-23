using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using QltS.Shared.Messaging.Idempotency;

namespace QltS.Shared.Persistence.EF.Idempotency;

/// <summary>
/// Generic implementation of request manager for command idempotency.
/// Uses Entity Framework to track processed requests in the database.
/// </summary>
/// <typeparam name="TContext">The DbContext type used to persist client requests</typeparam>
public class RequestIdempotencyService<TContext> : IRequestIdempotencyService
    where TContext : QltDBContextBase
{
    private readonly TContext _context;
    private readonly ILogger<RequestIdempotencyService<TContext>> _logger;

    public RequestIdempotencyService(TContext context, ILogger<RequestIdempotencyService<TContext>> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> ExistAsync(Guid id)
    {
        var request = await _context.FindAsync<RequestIdempotency>(id);
        return request != null;
    }

    public async Task CreateRequestForCommandAsync<T>(Guid id)
    {
        try
        {
            var request = new RequestIdempotency
            {
                RequestId = id,
                CommandName = typeof(T).Name,
                ProcessedAt = DateTime.UtcNow
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
