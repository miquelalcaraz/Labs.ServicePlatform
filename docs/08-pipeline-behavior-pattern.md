# Pipeline Behavior Pattern

## Executive Summary

### Brief Description
Pipeline Behaviors are cross-cutting concerns that wrap command/query execution in MediatR. They implement the Chain of Responsibility pattern, allowing pre-processing and post-processing of requests. Behaviors execute in order: Logging → Validation → Idempotency → Transaction, providing separation of concerns for infrastructure responsibilities.

### Technical Implementation

**Location**:
- Core Behaviors: `src/Blocks/Core.Application/Behaviors/`
- Persistence Behaviors: `src/Blocks/Shared.Persistence.EF/Behaviors/`
- Messaging Behaviors: `src/Blocks/Shared.Messaging/Behaviors/`

**Core Components**:

1. **LoggingBehavior** (Outermost):
```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly IUserHttpContext? _contextAccessor;
    private const int SlowCommandThresholdMs = 500;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        var commandName = GetCommandName(request);
        var requestId = _contextAccessor?.RequestId ?? "N/A";
        var correlationId = _contextAccessor?.CorrelationId ?? "N/A";

        using var activity = BehaviorHelper.StartCommandActivity(commandName);
        BehaviorHelper.SetCommandContextTags(activity, commandName, requestId, correlationId);

        _logger.LogInformation("Handling command {CommandName} with RequestId {RequestId}", commandName, requestId);

        var stopwatch = Stopwatch.StartNew();
        TResponse response;

        try
        {
            response = await next(cancellationToken);
            stopwatch.Stop();

            var elapsedMs = stopwatch.ElapsedMilliseconds;
            var isSlow = elapsedMs > SlowCommandThresholdMs;

            BehaviorHelper.RecordCommandExecution(commandName, elapsedMs, success: true);

            if (isSlow)
            {
                _logger.LogWarning("Command {CommandName} took {ElapsedMs}ms (threshold: {ThresholdMs}ms)",
                    commandName, elapsedMs, SlowCommandThresholdMs);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            BehaviorHelper.RecordCommandFailure(activity, stopwatch.ElapsedMilliseconds, ex);
            _logger.LogError(ex, "Error handling command {CommandName}", commandName);
            throw;
        }
    }
}
```

2. **ValidationBehavior**:
```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        if (!_validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .Where(r => r.Errors.Any())
            .SelectMany(r => r.Errors)
            .ToList();

        if (failures.Any())
        {
            throw new DomainValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
```

3. **IdempotencyBehavior**:
```csharp
public class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        var idempotentCommandType = request.GetType().GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && 
                (i.GetGenericTypeDefinition() == typeof(IIdempotentCommand<>) ||
                 i == typeof(IIdempotentCommand)));

        if (idempotentCommandType == null)
            return await next(); // Not idempotent, continue

        var requestId = GetRequestId(request);

        if (await _requestManager.ExistAsync(requestId))
        {
            _logger.LogInformation("Duplicate request {RequestId} detected", requestId);
            return GetDuplicateResult(request);
        }

        // Mark as being processed
        await _requestManager.CreateRequestForCommandAsync<TRequest>(requestId);

        return await next();
    }
}
```

4. **TransactionBehavior** (Innermost):
```csharp
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly QltDBContextBase _dbContext;
    private readonly IEventBusOutboxService? _integrationEventService;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        var response = default(TResponse);

        if (_dbContext.HasActiveTransaction)
        {
            return await next(cancellationToken); // Nested transaction, skip
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            Guid transactionId;

            await using var transaction = await _dbContext.BeginTransactionAsync();
            
            _logger.LogInformation("Begin transaction {TransactionId} for {CommandName}", 
                transaction.TransactionId, typeName);

            response = await next(cancellationToken);

            _logger.LogInformation("Commit transaction {TransactionId} for {CommandName}", 
                transaction.TransactionId, typeName);

            await _dbContext.CommitTransactionAsync(transaction);

            transactionId = transaction.TransactionId;

            // Publish integration events AFTER successful commit
            if (_integrationEventService != null)
            {
                await _integrationEventService.PublishEventsThroughEventBusAsync(transactionId);
            }
        });

        return response;
    }
}
```

5. **Behavior Registration Order** (in DI):
```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(assembly);
    
    // Order matters! Outer to inner:
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));         // 1. Outermost - logs everything
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));      // 2. Validates before processing
    cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));     // 3. Checks duplicates
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));     // 4. Innermost - wraps in transaction
});
```

6. **Pipeline Flow**:
```
Request
  ↓
LoggingBehavior (start timer, log begin)
  ↓
ValidationBehavior (FluentValidation)
  ↓
IdempotencyBehavior (check duplicate)
  ↓
TransactionBehavior (begin transaction)
  ↓
Command Handler (business logic)
  ↓
TransactionBehavior (commit transaction, publish outbox)
  ↓
IdempotencyBehavior (mark as processed)
  ↓
ValidationBehavior (return)
  ↓
LoggingBehavior (log end, detect slow commands)
  ↓
Response
```

### Critical Points Detected

1. **✅ Clear Separation of Concerns**:
   - Each behavior has single responsibility
   - Logging, validation, idempotency, transaction handled independently
   - **Benefit**: Easy to test, maintain, and extend

2. **✅ Correct Behavior Ordering**:
   - Logging outermost captures total execution time
   - Validation before idempotency prevents storing invalid requests
   - Transaction innermost ensures atomic data changes
   - **Benefit**: Logical execution flow

3. **⚠️ Validation Throws Exception**:
   - `ValidationBehavior` throws `DomainValidationException`
   - Breaks Railway-Oriented Programming (Result pattern)
   - **Risk**: Medium - Inconsistent error handling
   - **Impact**: Medium - Requires try-catch at API layer

4. **⚠️ Idempotency Marks Before Handler**:
   - Request marked as processed BEFORE handler execution
   - If handler fails, cannot retry with same RequestId
   - **Risk**: HIGH - See Idempotency Pattern doc
   - **Impact**: CRITICAL - Failed commands cannot be retried

5. **✅ OpenTelemetry Integration**:
   - `BehaviorHelper` creates activities for distributed tracing
   - Metrics recorded for command execution, validation, transactions
   - **Benefit**: Excellent observability

6. **⚠️ No Authorization Behavior**:
   - No built-in authorization/permission check
   - Must be implemented in handlers or at API layer
   - **Risk**: Low - Authorization often domain-specific
   - **Impact**: Medium - Security concern if forgotten

7. **⚠️ No Rate Limiting Behavior**:
   - No built-in rate limiting or throttling
   - High-frequency commands could overwhelm system
   - **Risk**: Medium - DDoS/abuse vulnerability
   - **Impact**: Medium - Performance degradation under load

8. **✅ Slow Command Detection**:
   - LoggingBehavior detects commands > 500ms
   - Logs warnings for performance investigation
   - **Benefit**: Proactive performance monitoring

9. **⚠️ No Retry Behavior**:
   - No automatic retry for transient failures
   - TransactionBehavior uses `CreateExecutionStrategy()` for database retries only
   - **Risk**: Low - Most transient failures are database-related
   - **Impact**: Low - Acceptable for command execution

10. **⚠️ No Caching Behavior**:
    - No caching for query results
    - All requests hit database/repository
    - **Risk**: Low - Commands shouldn't be cached
    - **Impact**: Medium - Queries could benefit from caching

11. **✅ Conditional Application**:
    - Behaviors check if applicable (e.g., IdempotencyBehavior checks for IIdempotentCommand)
    - Skip processing if not needed
    - **Benefit**: Performance optimization

### Possible Improvements

1. **Add Authorization Behavior**:
```csharp
public interface IAuthorizedCommand
{
    string[] RequiredPermissions { get; }
    string? ResourceId { get; } // For resource-level authorization
}

public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IAuthorizationService _authService;
    private readonly IUserHttpContext _userContext;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        if (request is not IAuthorizedCommand authorizedCommand)
            return await next(); // Not requiring authorization

        var userId = _userContext.UserId;
        var permissions = authorizedCommand.RequiredPermissions;

        var hasPermission = await _authService.HasPermissionsAsync(userId, permissions);

        if (!hasPermission)
        {
            throw new UnauthorizedAccessException(
                $"User {userId} lacks required permissions: {string.Join(", ", permissions)}");
        }

        // Resource-level authorization
        if (authorizedCommand.ResourceId != null)
        {
            var hasAccess = await _authService.CanAccessResourceAsync(
                userId, 
                authorizedCommand.ResourceId);

            if (!hasAccess)
            {
                throw new ForbiddenException(
                    $"User {userId} cannot access resource {authorizedCommand.ResourceId}");
            }
        }

        return await next();
    }
}

// Registration (after logging, before validation):
cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>)); // NEW
cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
```

2. **Add Rate Limiting Behavior**:
```csharp
public interface IRateLimitedCommand
{
    string RateLimitKey { get; }
    int MaxRequests { get; }
    TimeSpan Window { get; }
}

public class RateLimitingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IRateLimiter _rateLimiter;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        if (request is not IRateLimitedCommand rateLimited)
            return await next();

        var isAllowed = await _rateLimiter.TryAcquireAsync(
            rateLimited.RateLimitKey,
            rateLimited.MaxRequests,
            rateLimited.Window);

        if (!isAllowed)
        {
            throw new TooManyRequestsException(
                $"Rate limit exceeded for {rateLimited.RateLimitKey}. " +
                $"Max {rateLimited.MaxRequests} requests per {rateLimited.Window.TotalSeconds}s.");
        }

        return await next();
    }
}
```

3. **Add Caching Behavior for Queries**:
```csharp
public interface ICacheableQuery
{
    string CacheKey { get; }
    TimeSpan? CacheDuration { get; }
}

public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IDistributedCache _cache;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        if (request is not ICacheableQuery cacheableQuery)
            return await next();

        // Try to get from cache
        var cached = await _cache.GetAsync<TResponse>(cacheableQuery.CacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Cache hit for {CacheKey}", cacheableQuery.CacheKey);
            return cached;
        }

        // Execute query
        var response = await next();

        // Cache result if successful
        if (IsSuccessResult(response))
        {
            var duration = cacheableQuery.CacheDuration ?? TimeSpan.FromMinutes(5);
            await _cache.SetAsync(cacheableQuery.CacheKey, response, duration, cancellationToken);
        }

        return response;
    }
}

// Registration (only for queries):
cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
```

4. **Add Retry Behavior with Polly**:
```csharp
public interface IRetriableCommand
{
    int MaxRetries { get; }
    TimeSpan[] RetryDelays { get; }
}

public class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        if (request is not IRetriableCommand retriable)
            return await next();

        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retriable.MaxRetries,
                retryAttempt => retriable.RetryDelays[retryAttempt - 1],
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount}/{MaxRetries} for {CommandName} after {Delay}ms due to {Exception}",
                        retryCount,
                        retriable.MaxRetries,
                        timeSpan.TotalMilliseconds,
                        exception.GetType().Name);
                });

        return await retryPolicy.ExecuteAsync(async () => await next());
    }
}
```

5. **Add Dead Letter Behavior**:
```csharp
public class DeadLetterBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IDeadLetterQueue _deadLetterQueue;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            // Send to dead letter queue for manual investigation
            await _deadLetterQueue.PublishAsync(new DeadLetterMessage
            {
                RequestType = typeof(TRequest).Name,
                RequestData = JsonSerializer.Serialize(request),
                ExceptionType = ex.GetType().Name,
                ExceptionMessage = ex.Message,
                StackTrace = ex.StackTrace,
                OccurredAt = DateTime.UtcNow
            });

            throw; // Re-throw to maintain error flow
        }
    }
}
```

6. **Make Validation Return Result Instead of Throwing**:
```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        if (!_validators.Any())
            return await next(cancellationToken);

        var failures = // ... validation logic

        if (failures.Any())
        {
            // Instead of throwing, return Result with validation errors
            var errors = failures.Select(f => 
                Error.BadRequest.WithMessage(f.ErrorMessage).WithPropertyName(f.PropertyName));

            // For Result<T> responses
            if (typeof(TResponse).IsGenericType && 
                typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var failureMethod = typeof(Result).GetMethod(nameof(Result.Failure))
                    .MakeGenericMethod(typeof(TResponse).GetGenericArguments()[0]);
                
                return (TResponse)failureMethod.Invoke(null, new object[] { errors.First() });
            }

            // For Result responses
            if (typeof(TResponse) == typeof(Result))
            {
                return (TResponse)(object)Result.Failure(errors.First());
            }

            // Fallback to throwing for non-Result responses
            throw new DomainValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
```

7. **Add Performance Budget Behavior**:
```csharp
public interface IPerformanceBudget
{
    TimeSpan MaxExecutionTime { get; }
}

public class PerformanceBudgetBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        if (request is not IPerformanceBudget budget)
            return await next();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(budget.MaxExecutionTime);

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await next(cts.Token);
            stopwatch.Stop();

            var elapsedMs = stopwatch.ElapsedMilliseconds;
            var budgetMs = budget.MaxExecutionTime.TotalMilliseconds;

            if (elapsedMs > budgetMs * 0.8) // 80% of budget
            {
                _logger.LogWarning(
                    "Command {CommandName} used {ElapsedMs}ms of {BudgetMs}ms performance budget",
                    typeof(TRequest).Name,
                    elapsedMs,
                    budgetMs);
            }

            return response;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Command {typeof(TRequest).Name} exceeded performance budget of {budget.MaxExecutionTime.TotalMilliseconds}ms");
        }
    }
}
```

8. **Add Request Deduplication Behavior** (different from idempotency):
```csharp
// For in-flight request deduplication (prevent concurrent processing)
public class DeduplicationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        var key = GetDeduplicationKey(request);
        if (key == null)
            return await next();

        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await next();
        }
        finally
        {
            semaphore.Release();
        }
    }

    private string? GetDeduplicationKey(TRequest request)
    {
        // Example: Use request ID or aggregate ID
        if (request is IIdempotentCommand cmd)
            return cmd.RequestId.ToString();
        
        return null;
    }
}
```

## Conclusion

The Pipeline Behavior implementation is **architecturally excellent** with clear separation of concerns and proper ordering. Behaviors are well-designed for their specific responsibilities and integrate seamlessly with OpenTelemetry for observability.

**Strengths**:
- ✅ Clear separation of concerns (logging, validation, idempotency, transaction)
- ✅ Correct execution order
- ✅ Excellent OpenTelemetry integration
- ✅ Slow command detection
- ✅ Conditional application of behaviors

**Areas for Improvement**:
- ⚠️ Validation throws exception instead of returning Result
- ⚠️ No authorization behavior (security concern)
- ⚠️ No rate limiting (abuse vulnerability)
- ⚠️ No caching for queries (performance opportunity)
- ⚠️ Idempotency marks before handler execution (see Idempotency doc)

**Recommended Actions**:
1. 🔥 **HIGH**: Add AuthorizationBehavior for security
2. 🟡 **MEDIUM**: Add RateLimitingBehavior for abuse prevention
3. 🟡 **MEDIUM**: Add CachingBehavior for query performance
4. 🟡 **MEDIUM**: Make ValidationBehavior return Result instead of throwing
5. 🟢 **LOW**: Add RetryBehavior for transient failures
6. 🟢 **LOW**: Add DeadLetterBehavior for failed command tracking

**Overall Risk Level**: 🟢 Low - Well-implemented, missing some optional features

The pipeline behavior pattern is one of the strongest parts of the architecture. The main gaps are missing authorization and rate limiting behaviors, which should be added for production security and performance.
