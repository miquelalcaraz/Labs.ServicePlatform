# Developer Guide: Testing

## Overview

Testing strategy for QltS platform components and business services. Tests are split into two top-level groups under `tests/`:

- **`Platform/`** — architecture and infrastructure tests (independent of any business service)
- **`Services/`** — business service tests (Accounts, Identity, Notifications)

---

## Test Projects

| Project | Path | Scope | Frameworks |
|---------|------|-------|------------|
| `Platform.UnitTests` | `tests/Platform/` | Unit | xUnit, Moq, FluentAssertions, EF InMemory |
| `Platform.IntegrationTests` | `tests/Platform/` | Integration | xUnit, Testcontainers (SQL Server, Service Bus) |
| `Platform.FunctionalTests` | `tests/Platform/` | Functional | Aspire AppHost, xUnit v3, WebApplicationFactory |
| `Platform.TestWebApi` | `tests/Platform/` | Test host | Web API host used by Platform.FunctionalTests |
| `Services.FunctionalTests` | `tests/Services/` | Functional | Aspire AppHost, xUnit v3, end-to-end API tests (Accounts, Identity, Notifications) |
| `Services.UnitTests` | `tests/Services/` | Unit | xUnit, Moq, FluentAssertions (Accounts, Identity, Notifications) |

---

## Naming Convention

```csharp
[Fact]
public async Task MethodName_WhenCondition_ExpectedBehavior()
{
    // Arrange
    // Act
    // Assert
}
```

---

## Testing Guidelines

### Test Independence

Each test must:
- Set up its own data
- Clean up after itself
- Not depend on execution order
- Use InMemory DB with unique names (for unit tests)

### Mocking Strategy

- Mock external dependencies (Redis, HTTP, event bus)
- Use real EF Core InMemory for database logic (unit tests)
- Use Testcontainers for real database interactions (integration tests)
- Mock loggers to verify logging behavior

### Coverage Focus

Priority areas:
1. Critical path (happy path)
2. Error handling (fallback, resilience)
3. Edge cases (null, empty, duplicates)
4. Integration points (cache + DB, behaviors)

### Assertions

Use FluentAssertions:

```csharp
result.Should().BeTrue();
actual.Should().Be("expected");
collection.Should().HaveCount(3);
```

---

## Running Tests

```bash
# All tests
dotnet test

# All Platform tests
dotnet test tests/Platform/Platform.UnitTests
dotnet test tests/Platform/Platform.IntegrationTests
dotnet test tests/Platform/Platform.FunctionalTests

# All Services tests
dotnet test tests/Services/Services.UnitTests
dotnet test tests/Services/Services.FunctionalTests

# Specific test class
dotnet test --filter "FullyQualifiedName~IdempotencyTests"
dotnet test --filter "FullyQualifiedName~SubscriptionsApiTests"

# With coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

---

## Test Structure

### Platform

```
tests/Platform/
├── Platform.UnitTests/
│   ├── Behaviors/
│   │   └── ValidationBehaviorTests.cs
│   ├── Core/
│   │   ├── ErrorTests.cs
│   │   ├── ResultTests.cs
│   │   └── ValidationErrorTests.cs
│   └── Messaging/
│       ├── EntityFramework/
│       │   ├── IdempotencyEfServiceTests.cs
│       │   └── OutboxMessageServiceConcurrencyTests.cs
│       └── Redis/
│           └── IdempotencyRedisServiceTests.cs
├── Platform.IntegrationTests/
│   ├── EventBus/
│   │   └── ServiceBusEventBusTests.cs
│   ├── Messaging/
│   │   └── OutboxConcurrencyIntegrationTests.cs
│   └── Persistence/
│       └── EntityAuditIntegrationTests.cs
└── Platform.FunctionalTests/
    └── Tests/
        ├── DomainEventsTests.cs
        ├── IdempotencyTests.cs
        └── OutboxPatternTests.cs
```

### Services

```
tests/Services/
├── Services.UnitTests/
│   ├── Accounts/
│   │   └── (AccountsTestBase.cs + tests per use case)
│   ├── Identity/
│   │   └── (IdentityTestBase.cs + tests)
│   └── Notifications/
│       └── (NotificationsTestBase.cs + tests)
└── Services.FunctionalTests/
    ├── Accounts/
    │   ├── AccountsApiTests.cs
    │   └── SubscriptionsApiTests.cs
    ├── Identity/
    │   └── (IdentityFixture.cs + tests)
    └── Notifications/
        └── (NotificationsFixture.cs + tests)
```

### Template

```csharp
using FluentAssertions;
using Moq;
using Xunit;

namespace Platform.UnitTests.Messaging.ComponentType;

public class ComponentNameTests
{
    private readonly Mock<IDependency> _mockDependency;

    public ComponentNameTests()
    {
        _mockDependency = new Mock<IDependency>();
    }

    [Fact]
    public async Task MethodName_WhenCondition_ExpectedBehavior()
    {
        // Arrange
        var sut = CreateSystemUnderTest();

        // Act
        var result = await sut.MethodName();

        // Assert
        result.Should().NotBeNull();
    }

    private ComponentName CreateSystemUnderTest() => new(...);
}
```

---

## Current Coverage

### Platform

| Component | Project | Tests | Status |
|-----------|---------|------:|--------|
| Result / Error / ValidationError | `Platform.UnitTests` | 3 classes | ✅ Complete |
| ValidationBehavior | `Platform.UnitTests` | ✅ | ✅ Complete |
| IdempotencyEfService | `Platform.UnitTests` | ✅ | ✅ Complete |
| OutboxMessageService (concurrency) | `Platform.UnitTests` | ✅ | ✅ Complete |
| IdempotencyRedisService | `Platform.UnitTests` | ✅ | ✅ Complete |
| EntityAudit (integration) | `Platform.IntegrationTests` | ✅ | ✅ Complete |
| OutboxConcurrency (integration) | `Platform.IntegrationTests` | ✅ | ✅ Complete |
| ServiceBusEventBus (integration) | `Platform.IntegrationTests` | ✅ | ✅ Complete |
| DomainEvents (functional) | `Platform.FunctionalTests` | ✅ | ✅ Complete |
| Idempotency (functional) | `Platform.FunctionalTests` | ✅ | ✅ Complete |
| OutboxPattern (functional) | `Platform.FunctionalTests` | ✅ | ✅ Complete |

### Services

| Component | Project | Tests | Status |
|-----------|---------|------:|--------|
| AccountsApi (functional) | `Services.FunctionalTests` | ✅ | ✅ Complete |
| SubscriptionsApi (functional) | `Services.FunctionalTests` | ✅ | ✅ Complete |
| Accounts unit tests | `Services.UnitTests` | — | Pending |
| Identity unit tests | `Services.UnitTests` | — | Pending |
| Notifications unit tests | `Services.UnitTests` | — | Pending |
