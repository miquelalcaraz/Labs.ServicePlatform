# BuildingBlocks Functional Tests

## Overview

This project contains **functional tests** for the Sirius Building Blocks architecture. Unlike unit tests, these tests validate end-to-end behavior against real infrastructure (SQL Server, RabbitMQ) using Aspire.

## Purpose

The Building Blocks (Shared and Core projects) implement critical architectural patterns:
- **Idempotency** - Duplicate request detection
- **Outbox Pattern** - Transactional event publishing
- **Inbox Pattern** - Idempotent event consumers
- **Domain Events** - In-process event handling
- **Transactional Behavior** - Unit of Work with rollback
- **Eventual Consistency** - Cross-service coordination

These patterns are complex and span multiple layers. Functional tests validate their **integration** without depending on specific services (Accounts, Identity, etc.).

## Architecture

### TestWebApi
A minimal ASP.NET Core Web API that uses the Building Blocks:

- **TestDbContext** - Inherits from `SiriusDBContext` (base for all contexts)
- **TestEntity** - Simple aggregate root with domain events
- **Commands** - `CreateTestEntityCommand`, `UpdateTestEntityCommand`
- **Domain Event Handlers** - Transform domain events → integration events
- **Controllers** - HTTP endpoints for triggering commands

### BuildingBlocksFixture
Aspire-based test fixture that provisions:
- SQL Server (persistent container on port 1446)
- RabbitMQ (ephemeral container)
- TestWebApi configured with these resources

### Test Categories

#### 1. IdempotencyTests
Validates that duplicate requests (same `x-requestid` header) return the same result without side effects.

**Pattern tested:**
- `IdempotencyBehavior<TRequest, TResponse>` in pipeline
- `RequestIdempotency` table with unique constraint
- `IRequestIdempotencyService<TContext>`

#### 2. OutboxPatternTests
Validates that domain events generate integration events stored in the outbox table.

**Pattern tested:**
- Domain events dispatched after `SaveChangesAsync()`
- Integration events stored in `OutboxMessage` table
- Events remain in `NotPublished` state until background service processes them

#### 3. DomainEventsTests
Validates the complete flow: Command → Domain Event → Integration Event.

**Pattern tested:**
- Domain events added to aggregate root
- Dispatcher extension (`DomainEventsDispatcherExtension`)
- Domain event handlers (`IDomainEventHandler<T>`)
- Transactional consistency (events saved in same transaction)

## Running Tests

### Prerequisites
- Docker Desktop running (for SQL Server + RabbitMQ containers)
- .NET 10 SDK

### Commands

```bash
# Run all tests
dotnet test

# Run specific category
dotnet test --filter "FullyQualifiedName~IdempotencyTests"
dotnet test --filter "FullyQualifiedName~OutboxPatternTests"
dotnet test --filter "FullyQualifiedName~DomainEventsTests"

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Aspire Dashboard
By default, the Aspire dashboard is **disabled** (`DisableDashboard = true`) to speed up test execution.

To enable it for debugging:
1. Open `BuildingBlocksFixture.cs`
2. Set `DisableDashboard = false`
3. Run tests
4. Open `https://localhost:15888` to see resource status

## Adding New Tests

### For new Building Block features:

1. **Add test entity/command** (if needed) in `TestWebApi/`
2. **Create test class** in `Tests/` folder
3. **Use BuildingBlocksFixture** as `IClassFixture<BuildingBlocksFixture>`
4. **Access TestDbContext** via DI to verify database state

### Example:

```csharp
public sealed class NewFeatureTests : IClassFixture<BuildingBlocksFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public NewFeatureTests(BuildingBlocksFixture fixture)
    {
        _factory = fixture;
        _client = _factory.CreateDefaultClient();
    }

    [Fact]
    public async Task TestNewFeature()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        
        // Act
        var response = await _client.PostAsync("api/test", content);
        
        // Assert
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        // Verify database state...
    }
}
```

## Key Differences from Service Tests (Accounts.FunctionalTests)

| Aspect | Platform.FunctionalTests | Accounts.FunctionalTests |
|--------|-------------------------------|--------------------------|
| **Purpose** | Test architectural patterns | Test service endpoints |
| **Scope** | Building Blocks only | Full service (Accounts + Identity) |
| **Dependencies** | SQL Server + RabbitMQ | SQL Server + RabbitMQ + Identity API |
| **Test Entity** | Minimal `TestEntity` | Real domain (Organization, Account) |
| **Focus** | Infrastructure concerns | Business logic |

## Troubleshooting

### Tests fail with "Connection refused"
- Ensure Docker Desktop is running
- Check port 1446 is available (SQL Server)
- Check RabbitMQ port (5672) is available

### Idempotency tests fail
- Verify `RequestIdempotency` table exists
- Check unique constraint on `RequestId` column
- Review migrations in `TestDbContext`

### Outbox messages not found
- Verify `OutboxMessage` table exists
- Check domain event handler registered in DI
- Ensure `EnableDomainEventsDispatch = true` in configuration

## Future Test Coverage

Planned tests for upcoming features:
- **InboxPatternTests** - Idempotent consumer behavior
- **TransactionBehaviorTests** - Rollback scenarios
- **EventualConsistencyTests** - Cross-service saga patterns
- **AuditLogsTests** - Audit trail validation
