# Idempotency Pattern (Command Deduplication)

## Executive Summary

### Brief Description
Command idempotency ensures that executing the same command multiple times has the same effect as executing it once. This prevents duplicate processing of commands that arrive multiple times due to retries, network issues, or message broker redelivery. Implementation uses a database-enforced unique constraint on RequestId for atomic duplicate detection.

### Technical Implementation

**Location**:
- Interface: `src/Blocks/Shared.Messaging.Abstractions/Idempotency/IIdempotentCommand.cs`
- Base Class: `src/Blocks/Shared.Messaging/Idempotency/IdempotentCommand.cs`
- Behavior: `src/Blocks/Shared.Messaging/Behaviors/IdempotencyBehavior.cs`
- Service: `src/Blocks/Shared.Persistence.EF/Idempotency/RequestIdempotencyService.cs`
- Entity: `src/Blocks/Shared.Messaging/Idempotency/RequestIdempotency.cs`

**Core Components**:

1. **IIdempotentCommand Interface**:
```csharp
public interface IIdempotentCommand<TResponse> : ICommand<TResponse>
{
    Guid RequestId { get; }
    
    Result<TResponse> CreateResultForDuplicateRequest()
    {
        return Result.Success(default(TResponse)!);
    }
}

public interface IIdempotentCommand : ICommand
{
    Guid RequestId { get; }
    
    Result CreateResultForDuplicateRequest()
    {
        return Result.Success();
    }
}
```

2. **IdempotentCommand Base Class**:
```csharp
public abstract record IdempotentCommand<TResponse> : IIdempotentCommand<TResponse>
{
    protected IdempotentCommand(Guid? requestId = null)
    {
        RequestId = requestId ?? Guid.NewGuid();
    }

    public Guid RequestId { get; init; } = Guid.NewGuid();

    public virtual Result<TResponse> CreateResultForDuplicateRequest()
    {
        return Result.Success(default(TResponse)!);
    }
}
```

3. **RequestIdempotency Entity**:
```csharp
public class RequestIdempotency
{
    public Guid RequestId { get; set; } // PRIMARY KEY
    public string CommandName { get; set; }
    public DateTime ProcessedAt { get; set; }
}
```

4. **Database Configuration** (Unique Constraint):
```csharp
// In RequestIdempotencyConfiguration.cs
builder.HasKey(r => r.RequestId);
builder.HasIndex(r => r.RequestId).IsUnique();
```

5. **IdempotencyBehavior Pipeline**:
```csharp
public class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, ...)
    {
        // Check if command implements IIdempotentCommand
        var idempotentCommandType = request.GetType().GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && 
                (i.GetGenericTypeDefinition() == typeof(IIdempotentCommand<>) ||
                 i == typeof(IIdempotentCommand)));

        if (idempotentCommandType == null)
            return await next(); // Not idempotent, continue

        var requestId = GetRequestId(request);

        // Check if already processed
        if (await _requestManager.ExistAsync(requestId))
        {
            _logger.LogInformation("Duplicate request {RequestId} detected", requestId);
            return GetDuplicateResult(request);
        }

        // Mark as being processed (BEFORE executing handler)
        await _requestManager.CreateRequestForCommandAsync<TRequest>(requestId);

        // Execute command handler
        return await next();
    }
}
```

6. **RequestIdempotencyService Implementation**:
```csharp
public class RequestIdempotencyService<TContext> : IRequestIdempotencyService
    where TContext : QltDBContextBase
{
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
        }
        catch (DbUpdateException ex)
        {
            // Check if unique constraint violation (expected for duplicates)
            if (_context.IsUniqueConstraintViolation(ex))
            {
                _logger.LogWarning("Duplicate {RequestId} detected (database enforced)", id);
                return; // Swallow exception - this is expected
            }

            throw; // Rethrow if different error
        }
    }
}
```

7. **Example Usage**:
```csharp
[DataContract]
public record CreateOrganizationCommand : IdempotentCommand<bool>
{
    [DataMember]
    public string Name { get; private set; }
    
    [DataMember]
    public string TaxIdNumber { get; private set; }

    public CreateOrganizationCommand(string name, string taxId, Guid? requestId = null) 
        : base(requestId)
    {
        Name = name;
        TaxIdNumber = taxId;
    }
}

// API Controller
[HttpPost]
public async Task<IActionResult> CreateOrganization([FromBody] CreateOrganizationRequest request)
{
    // Extract request ID from header (or generate)
    var requestId = Request.Headers["X-Request-ID"].FirstOrDefault();
    var guid = string.IsNullOrEmpty(requestId) ? Guid.NewGuid() : Guid.Parse(requestId);

    var command = new CreateOrganizationCommand(
        request.Name, 
        request.TaxId, 
        requestId: guid);

    var result = await _mediator.Send(command);

    return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
}
```

### Critical Points Detected

1. **✅ Database-Enforced Idempotency**:
   - Unique constraint on RequestId provides atomic duplicate detection
   - No race conditions even with concurrent requests
   - **Benefit**: Reliable even under high load

2. **⚠️ Request Marked Before Handler Execution**:
   - `CreateRequestForCommandAsync()` called BEFORE `next()` (handler execution)
   - If handler fails, RequestId still marked as processed
   - **Risk**: HIGH - Failed commands cannot be retried with same RequestId
   - **Impact**: CRITICAL - User must retry with new RequestId, losing idempotency benefit

3. **⚠️ No Cleanup Mechanism**:
   - RequestIdempotency table grows indefinitely
   - No TTL or cleanup job
   - **Risk**: Medium - Table bloat over time
   - **Impact**: Medium - Performance degradation, storage costs

4. **⚠️ Duplicate Check Uses FindAsync**:
   - `ExistAsync()` uses `FindAsync()` which checks local cache first, then database
   - Could return false positive if entity in change tracker but not committed
   - **Risk**: Low - unlikely in pipeline behavior context
   - **Impact**: Low - edge case scenario

5. **⚠️ Generic Type Reflection**:
   - IdempotencyBehavior uses reflection to detect IIdempotentCommand
   - Performance overhead on every request
   - **Risk**: Low - reflection is fast for interface checks
   - **Impact**: Low - negligible performance impact

6. **✅ Custom Duplicate Response**:
   - Commands can override `CreateResultForDuplicateRequest()`
   - Allows custom behavior for different command types
   - **Benefit**: Flexibility for commands that need special duplicate handling

7. **⚠️ RequestId Generation**:
   - Base class generates RequestId with `Guid.NewGuid()` if not provided
   - Auto-generation defeats purpose of idempotency (each retry gets new ID)
   - **Risk**: Medium - Developers may forget to pass RequestId from client
   - **Impact**: Medium - Idempotency not enforced without explicit RequestId

8. **✅ OpenTelemetry Integration**:
   - Idempotency checks tracked with activities and metrics
   - `IdempotencyHelper.RecordDuplicateCommand()` logs duplicates
   - Good observability

9. **⚠️ Swallowed DbUpdateException**:
   - Unique constraint violations are caught and ignored (by design)
   - Other DbUpdateExceptions (connection issues, etc.) are rethrown
   - **Risk**: Medium - Relies on `IsUniqueConstraintViolation()` correctly identifying constraint violations
   - **Impact**: Medium - If misidentified, real errors could be swallowed

### Possible Improvements

1. **🔥 CRITICAL: Mark Request AFTER Successful Handler Execution**:
```csharp
public class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(...)
    {
        if (await _requestManager.ExistAsync(requestId))
            return GetDuplicateResult(request);

        try
        {
            // Execute handler FIRST
            var result = await next();

            // Only mark as processed if successful
            if (IsSuccessResult(result))
            {
                await _requestManager.CreateRequestForCommandAsync<TRequest>(requestId);
            }

            return result;
        }
        catch (Exception)
        {
            // Don't mark as processed on exception
            throw;
        }
    }

    private bool IsSuccessResult(TResponse response)
    {
        if (response is Result result)
            return result.IsSuccess;
        
        return true; // Non-Result responses considered successful
    }
}
```

2. **Add TTL and Cleanup Job**:
```csharp
public class RequestIdempotency
{
    public Guid RequestId { get; set; }
    public string CommandName { get; set; }
    public DateTime ProcessedAt { get; set; }
    public DateTime ExpiresAt { get; set; } // NEW
}

// Background cleanup job
public class IdempotencyCleanupJob : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Remove entries older than 30 days
            await _context.Set<RequestIdempotency>()
                .Where(r => r.ExpiresAt < DateTime.UtcNow)
                .ExecuteDeleteAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}

// In CreateRequestForCommandAsync:
ExpiresAt = DateTime.UtcNow.AddDays(30)
```

3. **Cache Recent RequestIds in Memory** (for performance):
```csharp
public class CachedRequestIdempotencyService : IRequestIdempotencyService
{
    private readonly IRequestIdempotencyService _inner;
    private readonly IMemoryCache _cache;
    private const int CacheDurationMinutes = 5;

    public async Task<bool> ExistAsync(Guid id)
    {
        // Check memory cache first
        if (_cache.TryGetValue(id, out _))
            return true;

        // Check database
        var exists = await _inner.ExistAsync(id);

        // Cache result
        if (exists)
        {
            _cache.Set(id, true, TimeSpan.FromMinutes(CacheDurationMinutes));
        }

        return exists;
    }

    public async Task CreateRequestForCommandAsync<T>(Guid id)
    {
        await _inner.CreateRequestForCommandAsync<T>(id);
        
        // Add to cache immediately
        _cache.Set(id, true, TimeSpan.FromMinutes(CacheDurationMinutes));
    }
}
```

4. **Add Request Status Tracking**:
```csharp
public enum RequestStatus
{
    Processing,
    Completed,
    Failed
}

public class RequestIdempotency
{
    public Guid RequestId { get; set; }
    public string CommandName { get; set; }
    public RequestStatus Status { get; set; } // NEW
    public DateTime ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; } // NEW
}

// In IdempotencyBehavior:
await _requestManager.MarkAsProcessing(requestId);

try
{
    var result = await next();
    
    if (result.IsSuccess)
        await _requestManager.MarkAsCompleted(requestId);
    else
        await _requestManager.MarkAsFailed(requestId, result.Error.Message);

    return result;
}
catch (Exception ex)
{
    await _requestManager.MarkAsFailed(requestId, ex.Message);
    throw;
}
```

5. **Require Explicit RequestId** (prevent auto-generation):
```csharp
public abstract record IdempotentCommand<TResponse> : IIdempotentCommand<TResponse>
{
    // Remove default generation - force caller to provide
    protected IdempotentCommand(Guid requestId)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("RequestId cannot be empty", nameof(requestId));
        
        RequestId = requestId;
    }

    public Guid RequestId { get; init; }
}

// In API:
[HttpPost]
public async Task<IActionResult> CreateOrganization([FromBody] CreateOrganizationRequest request)
{
    // Require X-Request-ID header
    if (!Request.Headers.TryGetValue("X-Request-ID", out var requestIdHeader))
        return BadRequest("X-Request-ID header is required for idempotent operations");

    if (!Guid.TryParse(requestIdHeader, out var requestId))
        return BadRequest("X-Request-ID must be a valid GUID");

    var command = new CreateOrganizationCommand(..., requestId);
    // ...
}
```

6. **Add Idempotency Middleware** (for automatic header handling):
```csharp
public class IdempotencyMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Only for POST/PUT/PATCH
        if (HttpMethods.IsPost(context.Request.Method) || 
            HttpMethods.IsPut(context.Request.Method) ||
            HttpMethods.IsPatch(context.Request.Method))
        {
            // Generate RequestId if not provided
            if (!context.Request.Headers.ContainsKey("X-Request-ID"))
            {
                var requestId = Guid.NewGuid().ToString();
                context.Request.Headers.Add("X-Request-ID", requestId);
                context.Response.Headers.Add("X-Request-ID", requestId);
            }
        }

        await next(context);
    }
}
```

7. **Add Partitioning for Scale**:
```csharp
// Partition by date for easier cleanup
public class RequestIdempotency
{
    public Guid RequestId { get; set; }
    public string CommandName { get; set; }
    public DateTime ProcessedAt { get; set; }
    public int PartitionKey => ProcessedAt.Year * 100 + ProcessedAt.Month; // YYYYMM
}

// Configuration
builder.HasKey(r => new { r.RequestId, r.PartitionKey });
builder.HasIndex(r => new { r.PartitionKey, r.ProcessedAt });
```

## Conclusion

The Idempotency implementation is **architecturally sound** with database-enforced atomic duplicate detection. However, it has a **CRITICAL flaw**: requests are marked as processed BEFORE handler execution, preventing retries of failed commands with the same RequestId.

**Recommended Actions**:
1. 🔥 **CRITICAL**: Mark requests as processed AFTER successful handler execution
2. 🔥 **HIGH**: Add TTL and cleanup job to prevent table bloat
3. 🟡 **MEDIUM**: Add request status tracking (Processing/Completed/Failed)
4. 🟡 **MEDIUM**: Add memory caching for recent requests (performance)
5. 🟢 **LOW**: Require explicit RequestId instead of auto-generation

**Overall Risk Level**: 🔴 HIGH - Prevents retry of failed commands

The pattern is well-implemented from an infrastructure perspective, but the timing of when requests are marked as processed needs immediate correction to support proper retry semantics.
