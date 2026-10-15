# Platform Functional Tests

## Overview

End-to-end tests for the Sirius building blocks, run against real infrastructure provisioned with Aspire. They validate the platform patterns independently of any business service (Accounts, Identity, Notifications):

- **Idempotency** — duplicate requests (same `x-requestid`) are detected and do not create side effects; failed requests are not recorded and can be retried
- **Domain events** — dispatched inside the command transaction
- **Transactional outbox** — integration events are stored in `OutboxMessage` in the same transaction as the business change

For the overall testing strategy see [Developer Guide: Testing](../../../docs/developer-guide/04-testing.md).

## Architecture

### Platform.TestWebApi

A minimal ASP.NET Core Web API (`tests/Platform/Platform.TestWebApi`) wired with the building blocks:

- **`TestDbContext`** — inherits from `SiriusDBContext` (write side); **`TestReadDbContext`** inherits from `SiriusReadDbContext`
- **`TestEntity`** — aggregate root that raises domain events
- **Commands** — `CreateTestEntityCommand`, `UpdateTestEntityCommand`, plus `CreateTestEntityCommandValidator`
- **Domain event handler** — `TestEntityCreatedDomainEventHandler` writes `TestEntityCreatedIntegrationEvent` to the outbox
- **`TestController`** — `POST api/test` and `PUT api/test/{id:guid}`; failures return `400` with the `Error`

### BuildingBlocksFixture

`WebApplicationFactory<Program>` that starts an Aspire distributed application and points the TestWebApi at it. It is shared by all test classes through the `BuildingBlocks` collection (`BuildingBlocksCollection`). Resources:

- SQL Server (persistent container, port 1446, database `testDb`)
- Redis (persistent container, `cache`)
- Azure Service Bus emulator (persistent, topic `sirius-event-bus`, subscription `Platform`)

### Tests

| Class | Tests | Validates |
|---|---|---|
| `IdempotencyTests` | `DuplicateRequest_ShouldReturnSuccess_WithoutCreatingDuplicateEntity`, `DifferentRequests_ShouldCreateDifferentEntities`, `RetryAfterValidationFailure_WithSameRequestId_ShouldProcessRequest`, `FailedCommand_ShouldNotPersistIdempotencyEntry` | `IdempotencyBehavior` returns `200 OK` for a duplicate without creating a second entity; a failed command (validation or handler `Result`) persists no `IdempotencyEntry`, so a retry with the same `x-requestid` is processed |
| `DomainEventsTests` | `CreateEntity_ShouldDispatchDomainEvent_AndGenerateIntegrationEvent` | Command → domain event → integration event in the outbox |
| `OutboxPatternTests` | `CreateEntity_ShouldStoreIntegrationEventInOutbox` | `TestEntityCreatedIntegrationEvent` is persisted in `OutboxMessage` |

## Running Tests

### Prerequisites

- Docker running (SQL Server, Redis and Service Bus emulator containers)
- .NET 10 SDK

### Commands

The project uses xUnit v3 on Microsoft Testing Platform, so it runs as an executable:

```bash
dotnet run --project tests/Platform/Platform.FunctionalTests

# Run a single class
dotnet run --project tests/Platform/Platform.FunctionalTests -- --filter-class "Platform.FunctionalTests.Tests.IdempotencyTests"
```

### Aspire Dashboard

The dashboard is disabled (`DisableDashboard = true` in `BuildingBlocksFixture`) to speed up test execution. Set it to `false` while debugging to inspect resource status.

## Adding New Tests

1. Add the entity, command or handler you need to `Platform.TestWebApi`.
2. Create the test class in `Tests/` and mark it with `[Collection("BuildingBlocks")]`.
3. Receive `BuildingBlocksFixture` in the constructor and use it as the `WebApplicationFactory<Program>`.
4. Resolve `TestDbContext` from a scope to verify database state.

```csharp
[Collection("BuildingBlocks")]
public sealed class NewFeatureTests
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public NewFeatureTests(BuildingBlocksFixture fixture)
    {
        _factory = fixture;
        _client = _factory.CreateDefaultClient();
    }

    [Fact]
    public async Task NewFeature_ShouldPersistExpectedState()
    {
        var response = await _client.PostAsync("api/test", content, TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
    }
}
```

## Differences from Services.FunctionalTests

| Aspect | Platform.FunctionalTests | Services.FunctionalTests |
|---|---|---|
| **Purpose** | Platform patterns | Service endpoints and authorization |
| **Host** | `Platform.TestWebApi` | `Accounts.API` and Identity |
| **Domain** | Minimal `TestEntity` | Real domain (companies, onboarding, activation) |
| **Focus** | Infrastructure concerns | Business behavior |

## Troubleshooting

- **Connection refused** — make sure Docker is running and port 1446 (SQL Server) is free.
- **Outbox messages not found** — check that the domain event handler is registered and that the transactional outbox registers the TestWebApi assembly's event types.
