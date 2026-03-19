# EventBus.Idempotency

Isolated and optional idempotency package for integration event consumers. Prevents duplicate event processing using the Idempotent Consumer pattern.

## Features

- **Isolated & Optional**: Add only to services that need idempotency
- **Automatic Configuration**: Uses `IEntityTypeConfiguration<T>` pattern, no manual DbContext setup required
- **Flexible Registration**: Works regardless of registration order with other components
- **Database-Backed**: Reliable persistence with EF Core
- **Generic**: Works with any `DbContext` implementation

## Installation

Add package reference to your service project:

```xml
<ProjectReference Include="..\..\Core\EventBus.Idempotency\EventBus.Idempotency.csproj" />
```

## Usage

### 1. Register Services

```csharp
using QltS.Core.EventBus.Idempotency.Extensions;

// Register your DbContext
services.AddQltSDBContext<YourContext>(config =>
{
    config.RegisterModelsFromAssembly(typeof(YourContext).Assembly);
}, dbOptions =>
{
    dbOptions.UseSqlServer(connectionString);
});

// Register idempotency - automatically registers configuration
services.AddEventIdempotency<YourContext>();
```

**Note**: Registration order doesn't matter. You can call `AddEventIdempotency` before or after `AddQltSDBContext`.

### 2. Use in Event Handlers

```csharp
using QltS.Core.EventBus.Services;

public class OrganizationCreatedHandler : IIntegrationEventHandler<OrganizationCreatedEvent>
{
    private readonly IEventIdempotencyService _idempotencyService;
    private readonly YourContext _context;

    public OrganizationCreatedHandler(
        IEventIdempotencyService idempotencyService,
        YourContext context)
    {
        _idempotencyService = idempotencyService;
        _context = context;
    }

    public async Task Handle(OrganizationCreatedEvent @event)
    {
        var handlerName = GetType().Name;

        // Check if already processed
        if (await _idempotencyService.IsProcessedAsync(@event.Id, handlerName))
            return;

        // Process event (your business logic)
        var account = new Account 
        { 
            OrganizationId = @event.OrganizationId,
            Name = @event.OrganizationName 
        };
        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        // Mark as processed
        await _idempotencyService.MarkAsProcessedAsync(@event.Id, handlerName, @event.GetType().Name);
    }
}
```

### 3. Create Migration

```bash
dotnet ef migrations add AddEventIdempotency
dotnet ef database update
```

## How It Works

1. `AddEventIdempotency<TContext>()` registers:
   - `IEventIdempotencyService` implementation
   - EventBus.Idempotency assembly in `QltDbContextConfiguration`

2. `DBContextBase.OnModelCreating` automatically applies:
   - `ProcessedIntegrationEventConfiguration` via `ApplyConfigurationsFromAssembly`

3. Creates `ProcessedIntegrationEvents` table with composite key `(EventId, HandlerName)`

## Database Schema

| Column | Type | Description |
|--------|------|-------------|
| EventId | Guid | Integration event ID (PK) |
| HandlerName | string(255) | Handler type name (PK) |
| ProcessedAt | DateTime | Processing timestamp |
| EventType | string(255) | Event type (optional, for auditing) |

## Architecture

- **Pattern**: Idempotent Consumer
- **Storage**: Database-backed via EF Core
- **Configuration**: Automatic via `IEntityTypeConfiguration<T>`
- **Dependencies**: Core.EventBus, Core.Persistence.EF

## Services Without Idempotency

Simply don't add the package reference or call `AddEventIdempotency`. No idempotency table will be created.

```csharp
// Logging service example - duplicates are acceptable
services.AddQltSDBContext<LoggingContext>(config => { ... });
// No AddEventIdempotency call - no idempotency tracking
```
