# Shared Building Blocks Tests

Unit tests for all shared building blocks, ensuring quality and reusability across services.

## 🎯 Objectives

- **Independent Testing**: Test components without service dependencies
- **Fast Execution**: Unit tests only, no integration with external systems
- **Comprehensive Coverage**: Focus on critical paths and edge cases
- **Maintainability**: Clear structure and reusable fixtures

## 📁 Structure

```
Messaging/
├── Redis/
│   └── RedisIdempotencyCacheTests.cs       # Fast duplicate detection tests
├── EntityFramework/
│   ├── RequestIdempotencyServiceTests.cs   # Hybrid cache + DB tests
│   ├── OutboxMessageServiceTests.cs        # (TODO)
│   └── IntegrationEventInboxServiceTests.cs # (TODO)
└── Behaviors/
    ├── IdempotencyBehaviorTests.cs         # (TODO)
    └── TransactionBehaviorTests.cs         # (TODO)

Persistence/
└── EntityFramework/
    ├── RepositoryTests.cs                  # (TODO)
    └── QltDBContextTests.cs                # (TODO - Two-SaveChanges pattern)
```

## 🛠️ Technologies

- **xUnit**: Test framework
- **FluentAssertions**: Readable assertions
- **Moq**: Mocking framework
- **EF Core InMemory**: For database tests

## ✅ Testing Guidelines

### 1. **Naming Convention**
```csharp
[Fact]
public async Task MethodName_WhenCondition_ExpectedBehavior()
{
    // Arrange
    // Act
    // Assert
}
```

### 2. **Test Independence**
Each test must:
- Set up its own data
- Clean up after itself
- Not depend on execution order
- Use InMemory DB with unique names

### 3. **Mocking Strategy**
- Mock external dependencies (Redis, HTTP, etc.)
- Use real EF Core InMemory for database logic
- Mock loggers to verify logging behavior

### 4. **Coverage Focus**
Priority areas:
1. ✅ Critical path (happy path)
2. ✅ Error handling (fallback, resilience)
3. ✅ Edge cases (null, empty, duplicates)
4. ✅ Integration points (cache + DB, behaviors)

### 5. **Assertions**
Use FluentAssertions for readability:
```csharp
// ❌ Bad
Assert.True(result);
Assert.Equal("expected", actual);

// ✅ Good
result.Should().BeTrue();
actual.Should().Be("expected");
```

## 🚀 Running Tests

### Run all tests
```bash
dotnet test tests/Shared.BuildingBlocks.Tests
```

### Run specific test class
```bash
dotnet test --filter "FullyQualifiedName~RedisIdempotencyCacheTests"
```

### Run with coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## 📊 Current Coverage

| Component | Tests | Status |
|-----------|-------|--------|
| RedisIdempotencyCache | 10 | ✅ Complete |
| RequestIdempotencyService | 8 | ✅ Complete |
| OutboxMessageService | 0 | ⏳ TODO |
| IntegrationEventInbox | 0 | ⏳ TODO |
| IdempotencyBehavior | 0 | ⏳ TODO |
| TransactionBehavior | 0 | ⏳ TODO |
| QltDBContext (Two-SaveChanges) | 0 | ⏳ TODO |
| Repository | 0 | ⏳ TODO |

## 🔨 Adding New Tests

### 1. Create test file in appropriate folder
```
Messaging/[ComponentType]/[ComponentName]Tests.cs
```

### 2. Follow template structure
```csharp
using FluentAssertions;
using Moq;
using Xunit;

namespace Shared.BuildingBlocks.Tests.Messaging.[ComponentType];

public class [ComponentName]Tests
{
    private readonly Mock<IDependency> _mockDependency;

    public [ComponentName]Tests()
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

    private [ComponentName] CreateSystemUnderTest()
    {
        return new [ComponentName](_mockDependency.Object);
    }
}
```

### 3. Test critical scenarios
- ✅ Happy path
- ✅ Null arguments
- ✅ Empty collections
- ✅ Exceptions and fallback
- ✅ Logging behavior

## 🎓 Examples

### Testing Redis Fallback
```csharp
[Fact]
public async Task ExistsAsync_WhenRedisThrowsException_ReturnsFalseAndLogs()
{
    // Arrange
    _mockDatabase.Setup(db => db.KeyExistsAsync(...))
        .ThrowsAsync(new RedisConnectionException(...));
    
    var cache = CreateCache();

    // Act
    var result = await cache.ExistsAsync(requestId);

    // Assert
    result.Should().BeFalse(); // Fallback behavior
    _mockLogger.Verify(/* verify warning logged */, Times.Once);
}
```

### Testing Hybrid Cache + DB
```csharp
[Fact]
public async Task ExistAsync_WithCache_WhenNotInCacheButInDb_ReturnsTrue()
{
    // Arrange
    await _context.Add(new RequestIdempotency { ... });
    await _context.SaveChangesAsync();
    
    _mockCache.Setup(c => c.ExistsAsync(...)).ReturnsAsync(false);
    
    // Act
    var result = await service.ExistAsync(requestId);

    // Assert
    result.Should().BeTrue(); // Found in DB
}
```

## 🐛 Debugging Tests

### Visual Studio
1. Right-click test method → Debug Test
2. Or use Test Explorer window

### CLI
```bash
dotnet test --logger "console;verbosity=detailed"
```

## 📚 Next Steps

1. ✅ Complete OutboxMessageService tests
2. ✅ Complete IntegrationEventInbox tests
3. ✅ Complete MediatR behaviors tests
4. ✅ Complete QltDBContext two-SaveChanges tests
5. ✅ Add integration tests for complex scenarios

## 🤝 Contributing

When adding tests:
1. Follow naming conventions
2. Add summary comments for complex scenarios
3. Update this README with coverage status
4. Ensure tests are independent and fast
