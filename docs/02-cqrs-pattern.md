# CQRS Pattern with MediatR

## Executive Summary

### Brief Description
Command Query Responsibility Segregation (CQRS) separates read and write operations into distinct models. Commands mutate state and return Result, while Queries retrieve data without side effects. The implementation uses MediatR as the mediator infrastructure with strongly-typed command/query interfaces.

### Technical Implementation

**Location**: 
- Commands: `src/Qlt.Platform/Qlt.Platform.Messaging.Abstractions/Commands/`
- Domain Events Interface: `src/Qlt.Domain/Qlt.Domain.Core/SeedWork/IDomainEvent.cs`
- Domain Events Handler Interface: `src/Qlt.Platform/Qlt.Platform.Messaging.Abstractions/DomainEvents/`
- Behaviors: `src/Qlt.Domain/Qlt.Domain.Application/Behaviors/`

**Core Components**:

1. **Command Abstractions**:
```csharp
// Non-generic command returning Result
public interface ICommand : IRequest<Result> { }

// Generic command returning Result<TResponse>
public interface ICommand<TResponse> : IRequest<Result<TResponse>> { }

// Command handlers
public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand { }

public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse> { }
```

2. **Domain Event Abstractions**:
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

3. **Pipeline Behaviors** (Applied in order):
   - **LoggingBehavior**: Tracks request/correlation IDs, measures execution time, detects slow commands (>500ms)
   - **ValidationBehavior**: Executes FluentValidation validators, throws `DomainValidationException` on failure
   - **IdempotencyBehavior**: Checks if idempotent commands were already processed
   - **TransactionBehavior**: Wraps commands in database transaction, publishes outbox events after commit

4. **Example Implementation**:
```csharp
// Command Definition
public record CreateOrganizationCommand : IdempotentCommand<bool>
{
    public string Name { get; private set; }
    public string TaxIdNumber { get; private set; }
    // ... other properties
}

// Command Handler
public class CreateOrganizationCommandHandler : ICommandHandler<CreateOrganizationCommand, bool>
{
    public async Task<Result<bool>> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var organization = Organization.Create(
            request.Name,
            request.TaxIdNumber,
            // ... other parameters
        );

        await _repository.AddAsync(organization);
        await _unitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success(true);
    }
}

// Validator
public class CreateOrganizationCommandValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TaxIdNumber).NotEmpty().MaximumLength(20);
    }
}
```

5. **Domain Event Dispatching**:
```csharp
// In QltDBContextBase.SaveEntitiesAsync()
await _mediator.DispatchDomainEventsAsync(this, cancellationToken);

// Domain Event Handler
public class OrganizationCreatedDomainEventHandler : IDomainEventHandler<OrganizationCreatedDomainEvent>
{
    public async Task Handle(OrganizationCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        // Publish integration event to event bus
        var integrationEvent = new OrganizationCreatedIntegrationEvent(...);
        await _eventBusOutboxService.AddAndSaveEventAsync(integrationEvent);
    }
}
```

### Critical Points Detected

1. **⚠️ No Query Abstractions**:
   - Only `ICommand` interfaces exist, no `IQuery<TResponse>` interface
   - Queries appear to be implemented as direct repository calls (e.g., `IOrganizationQueries`)
   - **Risk**: Inconsistent patterns between commands and queries
   - **Impact**: Medium - queries bypass pipeline behaviors (logging, validation)

2. **✅ Strong Pipeline Behavior Chain**:
   - Well-ordered behaviors: Logging → Validation → Idempotency → Transaction
   - Each behavior has single responsibility
   - OpenTelemetry integration for distributed tracing

3. **⚠️ Domain Events Dispatched Inside Transaction**:
   - Events dispatched in `SaveEntitiesAsync()` AFTER `SaveChanges()` but BEFORE transaction commit
   - Comment indicates awareness: "Right BEFORE committing data... single transaction"
   - **Risk**: Domain event handlers can fail after data saved but before commit
   - **Impact**: Medium - could lead to inconsistent state if domain event handler throws

4. **⚠️ Validation Throws Exception**:
   - `ValidationBehavior` throws `DomainValidationException` on validation failure
   - Breaks Railway-Oriented Programming paradigm
   - **Risk**: Inconsistent error handling (validation uses exceptions, business logic uses Result)
   - **Impact**: Medium - requires exception handling at API layer

5. **✅ Idempotency First-Class Citizen**:
   - Idempotency implemented as pipeline behavior (applies automatically)
   - No need to check manually in handlers

6. **⚠️ Logging Behavior Position**:
   - LoggingBehavior is outermost, so it captures all pipeline execution time
   - However, validation failures still get logged with elapsed time
   - **Risk**: Log noise from invalid requests
   - **Impact**: Low - acceptable tradeoff for observability

7. **⚠️ No Query Result Caching**:
   - No caching behavior in pipeline
   - All queries hit database/repository
   - **Impact**: Medium - could benefit from Redis/memory cache for read-heavy scenarios

8. **⚠️ TransactionBehavior Always Active**:
   - Wraps ALL commands in transactions, even if they don't need it
   - Checks `_dbContext.HasActiveTransaction` for nested scenarios
   - **Risk**: Performance overhead for commands that only query or don't modify state
   - **Impact**: Low - strategy.ExecuteAsync handles retries, generally acceptable

### Possible Improvements

1. **Add Query Abstractions**:
```csharp
// Query interfaces
public interface IQuery<TResponse> : IRequest<Result<TResponse>> { }

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse> { }

// Usage
public record GetOrganizationQuery(Guid Id) : IQuery<OrganizationViewModel>;

public class GetOrganizationQueryHandler : IQueryHandler<GetOrganizationQuery, OrganizationViewModel>
{
    public async Task<Result<OrganizationViewModel>> Handle(...)
    {
        var org = await _queries.GetAsync(request.Id);
        return org != null 
            ? Result.Success(org) 
            : Result.Failure<OrganizationViewModel>(Error.NotFound);
    }
}
```

2. **Add Caching Behavior for Queries**:
```csharp
public interface ICacheableQuery
{
    string CacheKey { get; }
    TimeSpan? CacheDuration { get; }
}

public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IQuery<TResponse>, ICacheableQuery
{
    private readonly IDistributedCache _cache;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        var cached = await _cache.GetAsync<TResponse>(request.CacheKey);
        if (cached != null) return cached;

        var response = await next();
        
        if (response.IsSuccess)
            await _cache.SetAsync(request.CacheKey, response, request.CacheDuration);

        return response;
    }
}
```

3. **Make Validation Return Result Instead of Throwing**:
```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(...)
    {
        var failures = // ... validation logic
        
        if (failures.Any())
        {
            // Instead of throwing, return failure Result
            var error = Error.BadRequest.WithMessage(string.Join("; ", failures));
            return (TResponse)(object)Result.Failure(error);
        }

        return await next(cancellationToken);
    }
}
```

4. **Separate Read and Write DbContext**:
```csharp
// Write context with change tracking
public class OrganizationWriteContext : QltDBContextBase { }

// Read context without change tracking (AsNoTracking)
public class OrganizationReadContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }
}
```

5. **Add Domain Event Dispatcher Behavior** (instead of in SaveChanges):
```csharp
public class DomainEventDispatcherBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand
{
    public async Task<TResponse> Handle(...)
    {
        var response = await next(cancellationToken);

        // Dispatch domain events AFTER transaction commits
        if (response.IsSuccess)
        {
            await _mediator.DispatchDomainEventsAsync(_dbContext, cancellationToken);
        }

        return response;
    }
}
```

6. **Add Authorization Behavior**:
```csharp
public interface IAuthorizedCommand
{
    string[] RequiredPermissions { get; }
}

public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand, IAuthorizedCommand
{
    public async Task<TResponse> Handle(...)
    {
        if (!await _authService.HasPermissions(request.RequiredPermissions))
        {
            return (TResponse)(object)Result.Failure(Error.Unauthorized);
        }

        return await next();
    }
}
```

7. **Add Performance Monitoring Behavior**:
```csharp
public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private const int LongRunningThreshold = 3000; // 3 seconds

    public async Task<TResponse> Handle(...)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > LongRunningThreshold)
        {
            _logger.LogWarning(
                "Long-running command {CommandName} took {ElapsedMs}ms",
                typeof(TRequest).Name,
                stopwatch.ElapsedMilliseconds);
        }

        return response;
    }
}
```

## Conclusion

The CQRS implementation is **well-structured and production-ready** with excellent pipeline behavior support. The integration with Result pattern, MediatR, and domain events is solid. However, there's an **inconsistency between commands and queries** - commands have rich abstractions and behaviors, while queries appear to bypass the pipeline entirely.

The most critical improvement would be:
1. **Adding IQuery abstractions** to unify read/write patterns
2. **Moving domain event dispatching** outside SaveChanges to after transaction commit
3. **Converting ValidationBehavior** to return Result instead of throwing exceptions

**Overall Risk Level**: 🟡 Medium - Solid for commands, queries need standardization
