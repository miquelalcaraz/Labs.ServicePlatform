# Command Idempotency

## Overview

The **Command Idempotency** component implements the **Idempotent Command Pattern** to prevent duplicate execution of commands in distributed systems. It ensures that processing the same command multiple times has the same effect as processing it once.

## Problem Statement

In distributed systems, commands can be received multiple times due to:
- Network retries
- Client-side retries after timeout
- Message broker redelivery
- Load balancer duplications

Without idempotency, duplicate commands would cause:
- Duplicate data creation (e.g., two orders instead of one)
- Incorrect business logic execution
- Data inconsistencies
- Financial discrepancies

## Solution

This component tracks processed commands using a unique identifier (correlation ID) and prevents duplicate execution by:

1. **Checking** if a command with the given ID was already processed
2. **Executing** the command only if it's the first time
3. **Recording** the command ID after successful execution
4. **Database-enforced** unique constraint for fail-safe protection

## Architecture

### Layer Separation

```
┌─────────────────────────────────────────────────────┐
│          EventBus (Abstractions)                    │
│  • IRequestManager (interface)                      │
│  • IdentifiedCommand<TRequest, TResponse>           │
│  • IdentifiedCommandHandler<...>                    │
└─────────────────────────────────────────────────────┘
                        ▲
                        │
┌─────────────────────────────────────────────────────┐
│       Persistence.EF (EF Core - Agnostic)           │
│  • ClientRequest (entity)                           │
│  • RequestManager<TContext> (implementation)        │
│  • AddCommandIdempotencyCore() [internal]           │
└─────────────────────────────────────────────────────┘
                        ▲
                        │
┌─────────────────────────────────────────────────────┐
│   Persistence.EF.Relational (SQL/PostgreSQL/etc)    │
│  • ClientRequestConfiguration (ToTable, indexes)    │
│  • AddCommandIdempotency<TContext>() [public API]   │
└─────────────────────────────────────────────────────┘
```

### Key Components

#### 1. **IRequestManager** (Abstraction)
```csharp
public interface IRequestManager
{
    Task<bool> ExistAsync(Guid id);
    Task CreateRequestForCommandAsync<T>(Guid id);
}
```

#### 2. **ClientRequest** (Entity)
```csharp
public class ClientRequest
{
    public Guid Id { get; set; }        // Correlation/Request ID
    public string Name { get; set; }     // Command type name
    public DateTime Time { get; set; }   // Processing timestamp
}
```

#### 3. **RequestManager<TContext>** (Implementation)
- Checks if command was already processed
- Records command execution
- Handles unique constraint violations gracefully

#### 4. **IdentifiedCommand<TRequest, TResponse>** (Wrapper)
- Wraps any command with a unique identifier
- Used with MediatR for automatic idempotency

## Usage

### 1. Registration (Service Startup)

**For relational databases (SQL Server, PostgreSQL, MySQL):**

```csharp
using Core.Persistence.EF.Relational.Extensions;

services.AddCommandIdempotency<AccountContext>();
```

**Future: For Cosmos DB (when implemented):**

```csharp
using Core.Persistence.EF.Cosmos.Extensions;

services.AddCommandIdempotency<AccountContext>();  // Same method name
```

### 2. Basic Usage (Manual)

```csharp
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result>
{
    private readonly IRequestManager _requestManager;
    
    public async Task<Result> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        var requestId = request.CorrelationId;  // Client-provided unique ID
        
        // Check if already processed
        if (await _requestManager.ExistAsync(requestId))
        {
            _logger.LogInformation("Duplicate request {RequestId} detected", requestId);
            return Result.Success();  // Already processed - return success
        }
        
        // Process command
        var order = new Order(request.CustomerId, request.Items);
        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();
        
        // Record that we processed this command
        await _requestManager.CreateRequestForCommandAsync<CreateOrderCommand>(requestId);
        
        return Result.Success();
    }
}
```

### 3. Automatic Usage (with IdentifiedCommand)

**Step 1: Wrap your command**

```csharp
// Instead of sending this:
var command = new CreateOrderCommand(customerId, items);

// Send this:
var identifiedCommand = new IdentifiedCommand<CreateOrderCommand, Result>(
    command,
    correlationId  // Unique ID from client
);

await _mediator.Send(identifiedCommand);
```

**Step 2: Generic handler does the work**

The `IdentifiedCommandHandler<TRequest, TResponse>` automatically:
- Checks if the command was processed
- Executes it only if it's the first time
- Records the execution

**No changes needed to your original command handler!**

## Database Schema

### ClientRequests Table

```sql
CREATE TABLE ClientRequests (
    Id          UNIQUEIDENTIFIER PRIMARY KEY,
    Name        NVARCHAR(200) NOT NULL,
    Time        DATETIME2 NOT NULL
);

CREATE UNIQUE INDEX IX_ClientRequests_Id ON ClientRequests(Id);
```

### Example Data

| Id | Name | Time |
|----|------|------|
| `550e8400-e29b-41d4-a716-446655440000` | `CreateOrderCommand` | `2024-03-15 10:30:00` |
| `6ba7b810-9dad-11d1-80b4-00c04fd430c8` | `UpdateInventoryCommand` | `2024-03-15 10:31:15` |

## Patterns and Best Practices

### 1. **Correlation ID Generation**

**Client-side (recommended):**
```csharp
// Generate once on client
var correlationId = Guid.NewGuid();

// Use same ID for retries
var command = new CreateOrderCommand { CorrelationId = correlationId };
await httpClient.PostAsync("/api/orders", command);
```

**Server-side (alternative):**
```csharp
// Extract from HTTP headers
var correlationId = HttpContext.Request.Headers["X-Correlation-ID"];
```

### 2. **Integration with HTTP APIs**

```csharp
[HttpPost("orders")]
public async Task<IActionResult> CreateOrder(
    [FromBody] CreateOrderRequest request,
    [FromHeader(Name = "X-Correlation-ID")] Guid? correlationId)
{
    var requestId = correlationId ?? Guid.NewGuid();
    
    var command = new IdentifiedCommand<CreateOrderCommand, Result>(
        new CreateOrderCommand(request.CustomerId, request.Items),
        requestId
    );
    
    var result = await _mediator.Send(command);
    return result.IsSuccess ? Ok() : BadRequest();
}
```

### 3. **Cleanup Strategy**

**Option A: Time-based cleanup (recommended)**
```csharp
public class ClientRequestCleanupService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Delete records older than 30 days
            await _dbContext.Database.ExecuteSqlAsync(
                $"DELETE FROM ClientRequests WHERE Time < {DateTime.UtcNow.AddDays(-30)}"
            );
            
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
```

**Option B: Sliding window**
- Keep only last N days of requests
- Archive old records to separate table

### 4. **Error Handling**

```csharp
try
{
    await _requestManager.CreateRequestForCommandAsync<TCommand>(requestId);
}
catch (DbUpdateException ex) when (_context.IsUniqueConstraintViolation(ex))
{
    // Another instance processed this request concurrently
    _logger.LogWarning("Concurrent duplicate request {RequestId} detected", requestId);
    // This is OK - request was processed by another instance
}
```

## Configuration

### Default Behavior

- **Table name**: `ClientRequests`
- **Unique constraint**: On `Id` column (database-enforced)
- **Registration**: Scoped lifetime for `IRequestManager`

### Customization (if needed in future)

```csharp
// Currently not needed, but extensible
services.AddCommandIdempotency<AccountContext>(options =>
{
    options.CleanupAfterDays = 30;
    options.TableName = "ProcessedCommands";  // Custom table name
});
```

## Testing

### Unit Testing

```csharp
public class RequestManagerTests
{
    [Fact]
    public async Task ExistAsync_ReturnsFalse_ForNewRequest()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var manager = new RequestManager<TestContext>(context, logger);
        var requestId = Guid.NewGuid();
        
        // Act
        var exists = await manager.ExistAsync(requestId);
        
        // Assert
        exists.Should().BeFalse();
    }
    
    [Fact]
    public async Task ExistAsync_ReturnsTrue_ForProcessedRequest()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var manager = new RequestManager<TestContext>(context, logger);
        var requestId = Guid.NewGuid();
        
        await manager.CreateRequestForCommandAsync<TestCommand>(requestId);
        
        // Act
        var exists = await manager.ExistAsync(requestId);
        
        // Assert
        exists.Should().BeTrue();
    }
}
```

### Integration Testing

```csharp
public class CommandIdempotencyIntegrationTests : IClassFixture<WebApplicationFactory>
{
    [Fact]
    public async Task CreateOrder_ProcessedOnce_WhenCalledMultipleTimes()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var request = new CreateOrderRequest { CustomerId = 123, Items = [...] };
        
        // Act - Send same request 3 times
        var response1 = await PostWithCorrelationId("/api/orders", request, correlationId);
        var response2 = await PostWithCorrelationId("/api/orders", request, correlationId);
        var response3 = await PostWithCorrelationId("/api/orders", request, correlationId);
        
        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);  // Idempotent
        response3.StatusCode.Should().Be(HttpStatusCode.OK);  // Idempotent
        
        // Verify only one order was created
        var orders = await _dbContext.Orders.Where(o => o.CustomerId == 123).ToListAsync();
        orders.Should().HaveCount(1);
    }
}
```

## Performance Considerations

### Database Impact

**Per command execution:**
- 1 SELECT query (check if exists)
- 1 INSERT query (record execution)

**Optimization tips:**
1. **Index on Id column** (already created by configuration)
2. **Regular cleanup** to keep table size manageable
3. **Partitioning** for very high-volume systems (future enhancement)

### Concurrency

The component handles concurrent duplicate requests gracefully:
- First request: Executes and records
- Concurrent requests: Fail on unique constraint → detected and ignored
- Subsequent requests: Detected by SELECT → skipped

## Migration from Old Implementation

### Before (manual registration)
```csharp
services.AddScoped<IRequestManager, RequestManager<AccountContext>>();
// Also needed manual UseClientRequestIdempotency() in OnModelCreating
```

### After (automatic)
```csharp
services.AddCommandIdempotency<AccountContext>();
// Configuration is automatic via IEntityTypeConfiguration
```

## Troubleshooting

### Issue: Duplicate commands are still being processed

**Possible causes:**
1. Correlation ID is not being passed correctly
2. Different correlation IDs for retries
3. `AddCommandIdempotency` not registered

**Solution:**
```csharp
// Verify correlation ID is consistent
_logger.LogInformation("Processing command with CorrelationId: {Id}", correlationId);
```

### Issue: Database errors on command execution

**Possible causes:**
1. Unique constraint violation not being caught
2. ClientRequests table not created

**Solution:**
```csharp
// Ensure migration includes ClientRequests table
dotnet ef migrations add AddClientRequests
dotnet ef database update
```

### Issue: Old requests not being cleaned up

**Possible causes:**
1. No cleanup job implemented

**Solution:**
Implement background service (see "Cleanup Strategy" above)

## Related Patterns

- **Event Idempotency** (`EventBus.Idempotency`): For incoming integration events
- **Transactional Outbox** (`EventBus.TransactionalOutbox`): For outgoing integration events
- **Circuit Breaker**: Complements idempotency for resilient systems

## References

- [Microsoft eShopOnContainers - Idempotency](https://github.com/dotnet-architecture/eShopOnContainers)
- [Idempotency Patterns - Martin Fowler](https://martinfowler.com/articles/patterns-of-distributed-systems/idempotent-receiver.html)
- [Entity Framework Core - Concurrency Tokens](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
