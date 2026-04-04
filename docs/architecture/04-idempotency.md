# Idempotency

Covers command deduplication, idempotent consumer for integration events, and the Redis hybrid cache layer. All mechanisms share a unified `IIdempotencyService` and `IdempotencyEntry` model.

---

## Unified Idempotency Model

A single `IdempotencyEntry` POCO with a composite key (`Id + Scope + Qualifier`) handles idempotency for both commands and integration events:

```csharp
// Platform.Messaging/Models/IdempotencyEntry.cs (POCO, no EF attributes)
public class IdempotencyEntry
{
    public Guid Id { get; set; }
    public IdempotencyScope Scope { get; set; }      // Command or IntegrationEvent
    public string Qualifier { get; set; } = string.Empty;  // Handler name for events
    public string? TypeName { get; set; }
    public DateTime ProcessedAt { get; set; }
}
```

```csharp
// Platform.Messaging.Abstractions/Services/IIdempotencyService.cs
public interface IIdempotencyService
{
    Task<bool> ExistsAsync(IdempotencyKey key);
    Task MarkAsProcessingAsync(IdempotencyKey key, string typeName);
    Task MarkAsCompletedAsync(IdempotencyKey key);
}
```

`IdempotencyKey` encapsulates `Id`, `Scope`, and `Qualifier`. `IdempotencyScope` is either `Command` or `IntegrationEvent`.

---

## Command Idempotency

Commands that extend `IdempotentCommand<T>` are automatically deduplicated by `IdempotencyBehavior` in the pipeline.

### Command Definition

```csharp
public record CreateOrganizationCommand : IdempotentCommand<bool>
{
    public string Name { get; private set; }
    // ...

    public override Result<bool> CreateResultForDuplicateRequest()
    {
        return Result.Success(true);
    }
}
```

### Pipeline Flow

```
IdempotencyBehavior
  ├── request is not IIdempotentRequest? → pass through (queries, non-idempotent commands)
  ├── ExistsAsync(key)? → return CreateResultForDuplicateRequest() (fast skip)
  ├── MarkAsProcessingAsync(key) → adds to EF change tracker (no SaveChanges yet)
  ├── next() → inner pipeline: transaction + validation + handler
  └── MarkAsCompletedAsync(key) → updates Redis cache if configured
```

The idempotency entry is committed atomically with business data in the same transaction. If the command fails, no entry is persisted — the command can be safely retried.

### API Response for Duplicates

Duplicate requests return `200 OK` with the same body as the original request. This follows industry standards (Stripe, PayPal, AWS) and maintains idempotency semantics — the client sees identical behavior for the 1st and Nth request.

---

## Idempotent Consumer (Integration Events)

Integration events are deduplicated by the `IdempotencyIntegrationEventEfDecorator<T>`, which wraps each handler and checks/marks idempotency per handler name.

### Decorator Flow

```
TelemetryIntegrationEventHandlerDecorator (outer)
  └── IdempotencyIntegrationEventEfDecorator (inner)
        ├── ExistsAsync(eventId, scope: IntegrationEvent, qualifier: handlerName)?
        │     → skip (log duplicate)
        ├── BEGIN TRANSACTION
        ├── handler.Handle(@event) → business logic
        ├── MarkAsProcessingAsync → adds to change tracker
        ├── COMMIT (business data + idempotency entry = atomic)
        └── MarkAsCompletedAsync → update Redis cache
```

The `Qualifier` field stores the handler type name, enabling per-handler idempotency — the same event can be processed by multiple handlers independently.

### Handler Example

```csharp
public class OrganizationCreatedIntegrationEventHandler
    : IIntegrationEventHandler<OrganizationCreatedIntegrationEvent>
{
    public async Task Handle(OrganizationCreatedIntegrationEvent @event)
    {
        // Pure business logic — idempotency handled by decorator
        var tenant = Tenant.Create(@event.TenantId, @event.OrganizationId, @event.Name);
        await _tenantRepository.CreateAsync(tenant);
    }
}
```

---

## Redis Hybrid Cache

`IdempotencyEfService<TContext>` optionally wraps `IdempotencyRedisService` to provide a two-tier duplicate detection system.

### Two-Tier Architecture

```
Request arrives
  ↓
Tier 1: Redis Cache (fast path)
  ├── Cache hit → return true (sub-millisecond)
  ├── Cache miss → continue to Tier 2
  └── Redis unavailable → log warning, continue to Tier 2
  ↓
Tier 2: Database (source of truth)
  ├── Record exists → return true
  └── Not exists → mark as processing (add to change tracker)
```

### Configuration

Redis is optional. If not configured, `IdempotencyEfService<TContext>` operates with database-only checks. The service constructor accepts an optional `IIdempotencyService? inner` parameter:

```csharp
public class IdempotencyEfService<TContext> : IIdempotencyService
    where TContext : DbContext
{
    private readonly TContext _context;
    private readonly IIdempotencyService? _innerCacheService;  // Redis, if configured

    public async Task<bool> ExistsAsync(IdempotencyKey key)
    {
        // Check cache first (Redis)
        if (_innerCacheService != null && await _innerCacheService.ExistsAsync(key))
            return true;

        // Check database (source of truth)
        return await _context.Set<IdempotencyEntry>()
            .AnyAsync(e => e.Id == key.Id && e.Scope == key.Scope && e.Qualifier == key.Qualifier);
    }
}
```

### Redis TTLs

| Phase | TTL | Purpose |
|---|---|---|
| Processing | 30 seconds | Prevent race conditions during command execution |
| Completed | 5 minutes | Fast duplicate detection for retries |

### Resilience

Redis failures are non-fatal. If Redis is unavailable, the system falls back to database-only checks. The database `UniqueConstraintViolationException` acts as a final safety net.

---

## Database Schema

Table `IdempotencyEntry` in schema `infra` (configured in `Platform.Persistence.EntityFramework.Relational`):

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | RequestId (commands) or EventId (integration events) |
| `Scope` | `string(20)` | `Command` or `IntegrationEvent` |
| `Qualifier` | `string(255)` | Empty for commands, handler name for events |
| `TypeName` | `string(255)` | Command or event type name |
| `ProcessedAt` | `DateTime` | Timestamp |

**Composite primary key:** `(Id, Scope, Qualifier)`
**Indexes:** `ProcessedAt`, `(Id, Scope)`
