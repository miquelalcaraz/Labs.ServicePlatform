# CQRS Pattern with MediatR

## Executive Summary

### Brief Description
Command Query Responsibility Segregation (CQRS) separates read and write operations into distinct models. Commands mutate state and return Result, while Queries retrieve data without side effects. Both flow through MediatR's pipeline, giving queries the same telemetry, logging, and validation as commands.

### Technical Implementation

**Location**: 
- Commands: `src/Qlt.Platform/Qlt.Platform.Messaging.Abstractions/Commands/`
- Queries: `src/Qlt.Platform/Qlt.Platform.Messaging.Abstractions/Queries/`
- Domain Events Interface: `src/Qlt.Domain/Qlt.Domain.Core/SeedWork/IDomainEvent.cs`
- Domain Events Handler Interface: `src/Qlt.Platform/Qlt.Platform.Messaging.Abstractions/DomainEvents/`
- Write-only Behaviors: `src/Qlt.Platform/Qlt.Platform.Messaging.EntityFramework/Behaviors/`
- Cross-cutting Behaviors: `src/Qlt.Domain/Qlt.Domain.Application/Behaviors/`
- Telemetry Behaviors: `src/Qlt.Platform/Qlt.Platform.Messaging/Behaviors/`

**Core Components**:

1. **Command Abstractions**:
```csharp
// Non-generic marker for pipeline behavior filtering (zero-reflection)
public interface ICommandRequest { }

// Non-generic command returning Result
public interface ICommand : IRequest<Result>, ICommandRequest { }

// Generic command returning Result<TResponse>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>, ICommandRequest { }

// Command handlers
public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand { }

public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse> { }
```

2. **Query Abstractions**:
```csharp
// Query interface — flows through pipeline but skips transactions and idempotency
public interface IQuery<TResponse> : IRequest<Result<TResponse>> { }

// Query handler — inject the read-only DbContext, not the write context
public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse> { }
```

3. **Domain Event Abstractions**:
```csharp
// In Domain.Core (pure DDD, no dependencies)
namespace Qlt.Domain;
public interface IDomainEvent { }

// In Platform.Messaging.Abstractions (MediatR adapter)
using MediatR;
using Qlt.Domain;

public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent, INotification { }
```

4. **Pipeline Behaviors** (Applied in order):
   - **TelemetryCommandBehavior / TelemetryQueryBehavior**: OpenTelemetry activities and metrics (separate counters/histograms for commands and queries)
   - **LoggingBehavior**: Tracks request/correlation IDs, measures execution time, detects slow requests (>500ms)
   - **IdempotencyBehavior**: Checks if idempotent commands were already processed. Skips non-`IIdempotentRequest` (including queries)
   - **IntegrationEventProcessorBehavior**: Publishes outbox events after command completes
   - **TransactionBehavior**: Wraps commands in database transaction. Skips non-`ICommandRequest` (including queries)
   - **ValidationBehavior**: Executes FluentValidation validators. Returns `Result.ValidationFailure` for Result-based responses; throws `DomainValidationException` for non-Result types
   - **DomainEventProcessorBehavior**: Dispatches domain events from tracked entities within the active transaction. No-ops naturally for queries (no transaction = early return)

   **How behaviors distinguish commands from queries:**
   | Behavior | Filter | Queries |
   |---|---|---|
   | TransactionBehavior | `request is not ICommandRequest` → skip | ✅ Skipped |
   | IdempotencyBehavior | `request is not IIdempotentRequest` → skip | ✅ Skipped |
   | DomainEventProcessorBehavior | No active transaction → early return | ✅ No-op |
   | TelemetryCommandBehavior | `DetectCommandType()` checks `ICommand` | ✅ Skipped |
   | TelemetryQueryBehavior | `DetectQueryType()` checks `IQuery<>` | ✅ Active |
   | LoggingBehavior | No filter | ✅ Active |
   | ValidationBehavior | No filter | ✅ Active |

5. **Read/Write DbContext Separation**:
```csharp
// Write context — transactions, audit logs, idempotency, outbox
public abstract class QltDBContext : DbContext, ITransactionalUnitOfWork { ... }

// Read context — NoTracking, throws on write attempts, read-replica ready
public abstract class QltReadDbContext : DbContext
{
    // NoTrackingWithIdentityResolution by default
    // SaveChanges() sealed to throw InvalidOperationException
}
```

6. **Command Example**:
```csharp
// Command Definition
public record CreateOrganizationCommand : IdempotentCommand<bool>
{
    public string Name { get; private set; }
    public string TaxIdNumber { get; private set; }
    // ...
}

// Command Handler — uses write context (via repository + UnitOfWork)
public class CreateOrganizationCommandHandler : ICommandHandler<CreateOrganizationCommand, bool>
{
    public async Task<Result<bool>> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var organization = Organization.Create(request.Name, ...);
        _repository.Add(organization);
        var saved = await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);
        return saved > 0
            ? Result.Success(true)
            : Result.Failure<bool>(Error.BadRequest.WithMessage("Failed to create organization"));
    }
}
```

7. **Query Example** (Reference implementation: Accounts service):
```csharp
// Query Definition — in Application/Queries/
public record GetOrganizationByIdQuery(Guid Id) : IQuery<Organization>;

public record GetAllOrganizationsQuery : IQuery<IEnumerable<OrganizationSummary>>;

// Query Handler — in Infrastructure/Persistence/Queries/
// Injects the read-only context, NOT the write context
public class GetOrganizationByIdQueryHandler(AccountReadContext context)
    : IQueryHandler<GetOrganizationByIdQuery, Organization>
{
    public async Task<Result<Organization>> Handle(
        GetOrganizationByIdQuery request, CancellationToken cancellationToken)
    {
        var organization = await context.Set<Domain.Models.OrganizationAggregate.Organization>()
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);

        if (organization is null)
            return Result.Failure<Organization>(
                Error.NotFound.WithMessage($"Organization with id '{request.Id}' was not found."));

        return Result.Success(new Organization { ... });
    }
}
```

8. **API Endpoint Usage**:
```csharp
// Commands and queries both flow through MediatR
public static async Task<Results<Ok<OrganizationViewModel>, NotFound>> GetOrganizationByIdAsync(
    Guid id, [AsParameters] AccountsServices services)
{
    var result = await services.Mediator.Send(new GetOrganizationByIdQuery(id));

    if (result.IsFailure)
        return TypedResults.NotFound();

    return TypedResults.Ok(result.Value);
}
```

9. **DI Registration** (Query handlers auto-discovered via assembly scanning):
```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly);          // Application (queries defined here)
    cfg.RegisterServicesFromAssembly(typeof(Infrastructure.AssemblyReference).Assembly); // Infrastructure (handlers here)
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
    cfg.AddOpenBehavior(typeof(IntegrationEventProcessorBehavior<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(DomainEventProcessorBehavior<,>));
});

// Telemetry registered separately (as IPipelineBehavior, not open behaviors)
services.AddCommandTelemetryBehavior();
services.AddQueryTelemetryBehavior();
```

10. **Domain Event Dispatching**:
```csharp
// DomainEventProcessorBehavior runs INSIDE TransactionBehavior
// Dispatches domain events from tracked entities within the active transaction
// Supports cascading: re-checks for new domain events after each dispatch cycle

// Domain Event Handler
public class OrganizationCreatedDomainEventHandler : IDomainEventHandler<OrganizationCreatedDomainEvent>
{
    public async Task Handle(OrganizationCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new OrganizationCreatedIntegrationEvent(...);
        await _eventBusOutboxService.AddAndSaveEventAsync(integrationEvent);
    }
}
```

### Architecture Decisions

1. **Why `ICommandRequest` marker instead of checking `ICommand<>` directly**:
   C# doesn't support open generic pattern matching (`request is ICommand<>`). The non-generic `ICommandRequest` marker enables zero-reflection filtering in pipeline behaviors, following the same pattern as `IIdempotentRequest`.

2. **Why separate `TelemetryCommandBehavior` and `TelemetryQueryBehavior`**:
   Each uses a static `Detect*Type()` method cached per `TRequest` generic instantiation. Separate behaviors enable distinct OpenTelemetry counters (`application.command_handlers.processed` vs `application.query_handlers.processed`) and histograms, allowing independent monitoring of read vs write performance.

3. **Why query handlers live in Infrastructure, not Application**:
   Query handlers depend on the concrete read DbContext (`AccountReadContext`), which is an infrastructure concern. The query records (`IQuery<T>`) live in Application as pure contracts.

4. **Why no `IQueryRequest` marker (YAGNI)**:
   No pipeline behavior currently needs to identify "this is a query" to skip it. Write-only behaviors already filter by their own markers (`ICommandRequest`, `IIdempotentRequest`). If a future behavior needs query-specific filtering, the marker can be added without breaking changes.

### Resolved Items

The following issues from the initial review have been addressed:

1. **~~No Query Abstractions~~** → ✅ `IQuery<T>` and `IQueryHandler<Q,R>` implemented. Accounts refactored as reference. Queries flow through MediatR pipeline with telemetry, logging, and validation.

2. **~~TransactionBehavior Always Active~~** → ✅ Now skips queries via `request is not ICommandRequest`. Lives in `Platform.Messaging.EntityFramework` (correct layer for messaging + persistence concern).

3. **~~Validation Throws Exception~~** → ✅ `ValidationBehavior` now returns `Result.ValidationFailure` for Result-based responses. Only throws `DomainValidationException` for non-Result types (backward compatibility).

4. **~~No Read/Write DbContext Separation~~** → ✅ `QltReadDbContext` (NoTracking, sealed SaveChanges) and `QltDBContext` (transactions, audit, outbox) fully implemented with separate connection string support.

5. **~~Domain Events Dispatched Inside SaveChanges~~** → ✅ Moved to `DomainEventProcessorBehavior` which runs inside `TransactionBehavior`. Supports cascading domain events.

### Remaining Considerations

1. **⚠️ No Query Result Caching**:
   - No caching behavior in pipeline
   - All queries hit database
   - **Impact**: Medium for read-heavy scenarios
   - **Future**: Consider `ICacheableQuery` marker with `CachingBehavior` backed by Redis

2. **⚠️ Logging Behavior Position**:
   - LoggingBehavior is outermost, captures all pipeline execution time including validation failures
   - **Impact**: Low — acceptable tradeoff for observability

3. **⚠️ No Authorization Behavior**:
   - Authorization handled outside MediatR pipeline
   - **Future**: Consider `IAuthorizedRequest` marker with `AuthorizationBehavior`

## Conclusion

The CQRS implementation is **complete and production-ready** with unified command/query patterns flowing through MediatR. Both commands and queries benefit from the pipeline (telemetry, logging, validation), while write-only concerns (transactions, idempotency, domain event dispatching) automatically skip queries through marker interfaces. The read/write DbContext separation supports read-replica deployments.

**Overall Risk Level**: 🟢 Low — Commands and queries are standardized with consistent patterns
