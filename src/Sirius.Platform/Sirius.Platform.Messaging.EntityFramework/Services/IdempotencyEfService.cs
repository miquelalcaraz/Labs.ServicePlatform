using System;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sirius.Platform.Messaging.Models;
using Sirius.Platform.Persistence;

namespace Sirius.Platform.Messaging.Services;

/// <summary>
/// Idempotency service that persists entries to the database.
/// Can operate standalone (EF-only) or as a decorator wrapping an inner cache-based service.
/// </summary>
public class IdempotencyEfService<TContext> : IIdempotencyService
    where TContext : DbContext, ITransactionalUnitOfWork
{
    private readonly TContext _context;
    private readonly IIdempotencyService? _inner;
    private readonly ILogger<IdempotencyEfService<TContext>> _logger;

    public IdempotencyEfService(
        TContext context,
        ILogger<IdempotencyEfService<TContext>> logger)
        : this(context, null, logger) { }

    public IdempotencyEfService(
        TContext context,
        IIdempotencyService? inner,
        ILogger<IdempotencyEfService<TContext>> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _inner = inner;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> ExistsAsync(IdempotencyKey key)
    {
        if (_inner is not null && await _inner.ExistsAsync(key))
        {
            _logger.LogDebug("{Scope} key {Id} found in cache (fast path)", key.Scope, key.Id);
            return true;
        }

        var qualifier = key.Qualifier ?? string.Empty;
        return await _context.Set<IdempotencyEntry>()
            .AnyAsync(e => e.Id == key.Id && e.Scope == key.Scope && e.Qualifier == qualifier);
    }

    public async Task MarkAsProcessingAsync(IdempotencyKey key, string? typeName = null)
    {
        if (_inner is not null)
            await _inner.MarkAsProcessingAsync(key, typeName);

        var entry = new IdempotencyEntry
        {
            Id = key.Id,
            Scope = key.Scope,
            Qualifier = key.Qualifier ?? string.Empty,
            TypeName = typeName,
            ProcessedAt = DateTime.UtcNow
        };

        _context.Add(entry);

        _logger.LogInformation(
            "Registered {Scope} key {Id} for hybrid processing (cache + DB)",
            key.Scope, key.Id);
    }

    public async Task MarkAsCompletedAsync(IdempotencyKey key)
    {
        if (_inner is not null)
            await _inner.MarkAsCompletedAsync(key);
    }
}
