# Developer Guide: Testing

## Overview

Testing strategy for QltS platform components and business services. Tests are organized by scope: unit tests for isolated component logic, integration tests for infrastructure interactions, and functional tests for end-to-end API verification.

---

## Test Projects

| Project | Scope | Frameworks |
|---------|-------|------------|
| `Platform.UnitTests` | Unit | xUnit, Moq, FluentAssertions, EF InMemory |
| `Platform.IntegrationTests` | Integration | xUnit, Testcontainers (SQL Server, Service Bus) |
| `Platform.FunctionalTests` | Functional | Aspire AppHost, xUnit v3, WebApplicationFactory |
| `Platform.TestWebApi` | Test host | Web API host used by FunctionalTests |
| `Services.Accounts.FunctionalTests` | Functional | Aspire AppHost, xUnit v3, end-to-end API tests |

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

# Specific project
dotnet test tests/Platform.UnitTests

# Specific test class
dotnet test --filter "FullyQualifiedName~RedisIdempotencyCacheTests"

# With coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

---

## Unit Test Structure

```
Platform.UnitTests/
├── Messaging/
│   ├── Redis/
│   │   └── RedisIdempotencyCacheTests.cs
│   ├── EntityFramework/
│   │   └── RequestIdempotencyServiceTests.cs
│   └── Behaviors/
│       └── (IdempotencyBehaviorTests, TransactionBehaviorTests)
└── Persistence/
    └── EntityFramework/
        └── (RepositoryTests, QltDBContextTests)
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

| Component | Tests | Status |
|-----------|-------|--------|
| RedisIdempotencyCache | 10 | ✅ Complete |
| RequestIdempotencyService | 8 | ✅ Complete |
| OutboxMessageService | — | Pending |
| IdempotencyBehavior | — | Pending |
| TransactionBehavior | — | Pending |
| QltDBContext | — | Pending |
| Repository | — | Pending |
