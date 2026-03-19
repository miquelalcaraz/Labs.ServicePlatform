# EventBus.TransactionalOutbox

Isolated and optional transactional outbox package for integration event publishers. Ensures reliable event publishing using the Transactional Outbox pattern.

## Features

- **Isolated & Optional**: Add only to services that publish integration events
- **Automatic Configuration**: Uses `IEntityTypeConfiguration<T>` pattern, no manual DbContext setup required
- **Flexible Registration**: Works regardless of registration order with other components
- **Database-Backed**: Reliable persistence with EF Core
- **Exactly-Once Delivery**: Events are saved within the same transaction as business data
- **Generic**: Works with any `DbContext` implementation

## Installation

Add package reference to your service project:

```xml
<ProjectReference Include="..\..\Core\EventBus.TransactionalOutbox\Core.EventBus.TransactionalOutbox.csproj" />
```

## Usage

### 1. Register Services

```csharp
using QltS.Core.EventBus.TransactionalOutbox.Extensions;

// Register your DbContext
services.AddQltSDBContext<YourContext>(config =>
{
    config.RegisterModelsFromAssembly(typeof(YourContext).Assembly);
}, dbOptions =>
{
    dbOptions.UseSqlServer(connectionString);
});

// Register transactional outbox - automatically registers configuration
services.AddTransactionalOutbox<YourContext>(options =>
{
    options.RegisterEventTypes(typeof(YourIntegrationEvents).Assembly);
});
```

**Note**: Registration order doesn't matter. You can call `AddTransactionalOutbox` before or after `AddQltSDBContext`.

### 2. Use in Command Handlers (with TransactionBehavior)

The transactional outbox works automatically with the `TransactionBehavior` from MediatR pipeline:

```csharp
using MediatR;
using QltS.Core.Domain.SeedWork;

public class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, Result>
{
    private readonly AccountContext _context;

    public CreateAccountCommandHandler(AccountContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        // 1. Create your domain entity
        var account = new Account(request.Name, request.Email);
        _context.Accounts.Add(account);

        // 2. Domain entity raises a domain event
        account.AddDomainEvent(new AccountCreatedDomainEvent(account.Id, account.Name));

        // 3. SaveEntitiesAsync is called by TransactionBehavior
        // - Saves account
        // - Dispatches domain events
        // - Domain event handler converts to integration event
        // - Integration event is saved in outbox (same transaction)
        
        return Result.Success();
    }
}
```

### 3. Domain Event Handler (converts to Integration Event)

```csharp
using MediatR;
using QltS.Core.EventBus.Abstractions;
using QltS.Core.EventBus.Extensions;

public class AccountCreatedDomainEventHandler : INotificationHandler<AccountCreatedDomainEvent>
{
    private readonly AccountContext _context;
    private readonly IIntegrationEventService _integrationEventService;

    public AccountCreatedDomainEventHandler(
        AccountContext context,
        IIntegrationEventService integrationEventService)
    {
        _context = context;
        _integrationEventService = integrationEventService;
    }

    public async Task Handle(AccountCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        // Create integration event
        var integrationEvent = new AccountCreatedIntegrationEvent(
            notification.AccountId,
            notification.AccountName);

        // Add to outbox (will be saved in the same transaction)
        await _integrationEventService.AddAndSaveEventAsync(integrationEvent);
    }
}
```

### 4. Event Publishing (Background Service)

Events are published by a background service that polls the outbox:

```csharp
// This is typically provided by the IntegrationEventService
// Events with state=NotPublished are retrieved and published to the message broker
```

### 5. Create Migration

```bash
dotnet ef migrations add AddTransactionalOutbox
dotnet ef database update
```

## How It Works

1. **Save Phase** (within transaction):
   - Business data is saved
   - Domain events are dispatched
   - Domain event handlers create integration events
   - Integration events are saved to `IntegrationEventLog` table in the **same transaction**

2. **Publish Phase** (background process):
   - Background service polls for events with `State = NotPublished`
   - Events are published to message broker
   - On success, state is updated to `Published`
   - On failure, state is updated to `PublishedFailed` and retry logic applies

3. **Configuration** (automatic):
   - `AddTransactionalOutbox<TContext>()` registers the service and assembly
   - `DBContextBase.OnModelCreating` applies `IntegrationEventLogConfiguration` via `ApplyConfigurationsFromAssembly`

## Database Schema

### IntegrationEventLog Table

| Column | Type | Description |
|--------|------|-------------|
| EventId | Guid | Integration event ID (PK) |
| EventTypeName | string | Full type name of the event |
| Content | string | JSON serialized event |
| CreationTime | DateTime | Event creation timestamp |
| State | EventStateEnum | Current state (NotPublished, InProgress, Published, PublishedFailed) |
| TimesSent | int | Number of publication attempts |
| TransactionId | Guid | Database transaction ID |

### EventStateEnum

- **NotPublished** (0): Event is waiting to be published
- **InProgress** (1): Event is currently being published
- **Published** (2): Event was successfully published
- **PublishedFailed** (3): Event publication failed

## Architecture

- **Pattern**: Transactional Outbox
- **Storage**: Database-backed via EF Core
- **Configuration**: Automatic via `IEntityTypeConfiguration<T>`
- **Dependencies**: Core.EventBus.Contracts, Core.Persistence.EF

## Services Without Outbox

For services that only consume events (don't publish), simply don't add this package:

```csharp
// Consumer service example - receives events, doesn't publish
services.AddQltSDBContext<ConsumerContext>(config => { ... });
services.AddEventIdempotency<ConsumerContext>(); // Only idempotency
// No AddTransactionalOutbox call - no outbox table created
```

## Combining with Idempotency

For services that both publish and consume events:

```csharp
using QltS.Core.EventBus.TransactionalOutbox.Extensions;
using QltS.Core.EventBus.Idempotency.Extensions;

services.AddQltSDBContext<YourContext>(config => { ... });

// Publisher side: Transactional Outbox
services.AddTransactionalOutbox<YourContext>(options =>
{
    options.RegisterEventTypes(typeof(YourEvents).Assembly);
});

// Consumer side: Idempotent Consumer
services.AddEventIdempotency<YourContext>();
```

Both patterns work independently and can be used together when needed.
