# Copilot Development Rules — QltS Services

This document defines the rules and patterns that **must** be followed when implementing features in `Qlt.Services.*` microservices. The platform is consumed as NuGet packages — do not modify or recreate platform internals.

---

## Service Project Structure

Every service follows a **5-project structure**:

```
Qlt.Services.{ServiceName}/
├── Api/                    ← Web host, endpoints, Program.cs
│   ├── Api/                ← Endpoint classes + Services class
│   └── Extensions/         ← AddApplicationServices (DI)
├── Application/            ← Commands, Queries, Handlers, Validators, Domain Event Handlers
│   ├── Commands/
│   ├── Queries/
│   ├── Validators/
│   ├── DomainEventHandlers/
│   └── AssemblyReference.cs
├── Domain/                 ← Entities, Value Objects, Repositories, Domain Events
│   ├── Models/{Aggregate}/
│   ├── Events/
│   ├── Repositories/
│   └── Exceptions/
├── Infrastructure/         ← DbContext, Repositories, Configurations, DI
│   ├── Persistence/
│   │   ├── EntityConfigurations/
│   │   ├── Queries/        ← Query handlers (use read context)
│   │   ├── Repositories/
│   │   ├── Migrations/
│   │   └── Seed/
│   ├── Extensions/         ← AddInfrastructureServices (DI)
│   └── AssemblyReference.cs
└── Contracts/              ← Integration events shared with other services
    ├── IntegrationEvents/
    └── AssemblyReference.cs
```

### AssemblyReference Pattern

Every project that needs assembly scanning (Application, Infrastructure, Contracts) must have:

```csharp
using System.Reflection;

namespace Qlt.Services.{ServiceName}.{Layer};

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
```

---

## Contracts Project — Integration Event Sharing

Integration events and their models live in a **separate Contracts project** that is shared between microservices.

### Rules

- The Contracts project only references `Platform.Messaging.Abstractions` — nothing else
- It contains **only** integration event records and any DTOs they carry
- Other services that consume these events add a project reference (or NuGet reference) to the Contracts project
- Never put commands, queries, domain events, or domain entities in Contracts

### Example

```csharp
// Contracts/IntegrationEvents/OrderCompletedIntegrationEvent.cs
using Qlt.Platform.Messaging.IntegrationEvents;

namespace Qlt.Services.Orders.Contracts.IntegrationEvents;

public record OrderCompletedIntegrationEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal Total,
    string Currency) : IntegrationEvent;
```

### Contracts .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>Qlt.Services.{ServiceName}.Contracts</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Qlt.Platform\Qlt.Platform.Messaging.Abstractions\Platform.Messaging.Abstractions.csproj" />
  </ItemGroup>
</Project>
```

---

## DI Configuration — Two-Layer Pattern

### Layer 1: Application (`Api/Extensions/Extensions.cs`)

```csharp
public static IServiceCollection AddApplicationServices(this IHostApplicationBuilder builder)
{
    var services = builder.Services;

    builder.AddInfrastructureServices();
    builder.AddRedisClient("cache");

    services.AddHttpContextAccessor();

    services.AddQltMediator(cfg =>
    {
        cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly);          // Application handlers
        cfg.RegisterServicesFromAssembly(typeof(Infrastructure.AssemblyReference).Assembly); // Query handlers
        cfg.WithLogging(typeof(LoggingBehavior<,>));
        cfg.WithValidation(typeof(ValidationBehavior<,>));
    });

    services.AddValidatorsFromAssembly(AssemblyReference.Assembly);

    return services;
}
```

### Layer 2: Infrastructure (`Infrastructure/Extensions/Extensions.cs`)

```csharp
public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
{
    var services = builder.Services;
    var connectionString = builder.Configuration.GetConnectionString("{ServiceName}Db");
    var readConnectionString = builder.Configuration.GetConnectionString("{servicename}db-readonly")
        ?? connectionString;

    // Write context
    services.AddQltSDBContext<{ServiceName}Context>(
        conf => conf
            .SetDefaultSchema("{servicename}")
            .WithIdempotency()
            .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
        options => options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(AssemblyReference).Assembly.FullName);
        })
    );

    // Read context
    services.AddQltReadDbContext<{ServiceName}ReadContext>(
        conf => conf
            .SetDefaultSchema("{servicename}")
            .RegisterModelsFromAssembly(typeof(AssemblyReference).Assembly),
        options => options.UseSqlServer(readConnectionString)
    );

    services.AddMigration<{ServiceName}Context, {ServiceName}ContextSeed>();

    // Event bus + subscriptions
    builder.AddServiceBusEventBus("eventbus")
        .AddEventBusSubscriptions();

    // Idempotency: Redis (fast) + EF (transactional guarantee)
    services.AddRedisIdempotencyCache(builder.Configuration)
        .ForApiRequests()
        .ForIntegrationEventHandlers();

    services.AddIdempotencyEntityFramework<{ServiceName}Context>()
        .ForApiRequests()
        .ForIntegrationEventHandlers();

    // Transactional outbox
    services.AddTransactionalOutbox<{ServiceName}Context>(options =>
    {
        options.RegisterEventTypes(typeof(Contracts.AssemblyReference).Assembly);
    });

    // Repositories
    services.AddScoped<I{Entity}Repository, {Entity}Repository>();
}

private static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
{
    // Register integration event subscriptions from OTHER services
    eventBus.AddSubscription<SomeExternalEvent, SomeExternalEventHandler>();
}
```

### Critical DI Rules

- **Always use `AddQltMediator`** — never call `AddMediatR` directly
- **Never manually register telemetry** — `AddQltMediator` handles it automatically
- **Register handlers from both assemblies** — Application (commands, domain event handlers) and Infrastructure (query handlers)
- **Infrastructure calls go in `AddInfrastructureServices`** — database, event bus, idempotency, outbox, repositories

---

## Commands

### Idempotent Command (default for API-facing commands)

```csharp
[DataContract]
public record CreateOrderCommand : IdempotentCommand<bool>
{
    [DataMember]
    public string CustomerName { get; private set; } = null!;

    // ... other properties

    public CreateOrderCommand() { }

    public CreateOrderCommand(string customerName, /* ... */ Guid? requestId = null)
        : base(requestId)
    {
        CustomerName = customerName;
    }

    public override Result<bool> CreateResultForDuplicateRequest()
    {
        return Result.Success(true);
    }
}
```

### Command Handler

```csharp
public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository;

    public CreateOrderCommandHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
    }

    public async Task<Result<bool>> Handle(CreateOrderCommand message, CancellationToken cancellationToken)
    {
        var order = Order.Create(message.CustomerName, /* ... */);
        _orderRepository.Add(order);

        // FLUSH — not commit. The transaction commits later.
        var saved = await _orderRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return saved > 0
            ? Result.Success(true)
            : Result.Failure<bool>(Error.BadRequest.WithMessage("Failed to create order"));
    }
}
```

### Command Rules

- `SaveChangesAsync()` **flushes** to DB but does NOT commit — the `TransactionBehavior` commits later
- Always return `Result<T>`, never throw exceptions for expected failures
- Override `CreateResultForDuplicateRequest()` to return a meaningful value (e.g., `true` for creation commands)
- Use `[DataContract]` / `[DataMember]` for serialization support

---

## Queries

### Query Definition (Application layer)

```csharp
public record GetOrderByIdQuery(Guid Id) : IQuery<OrderViewModel>;

public record GetAllOrdersQuery : IQuery<IEnumerable<OrderSummary>>;
```

### Query Handler (Infrastructure layer — uses read context)

```csharp
public class GetOrderByIdQueryHandler(OrderReadContext context)
    : IQueryHandler<GetOrderByIdQuery, OrderViewModel>
{
    public async Task<Result<OrderViewModel>> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await context.Orders
            .Where(o => o.Id == request.Id)
            .Select(o => new OrderViewModel { Id = o.Id, Name = o.Name /* ... */ })
            .FirstOrDefaultAsync(cancellationToken);

        return order is not null
            ? Result.Success(order)
            : Result.Failure<OrderViewModel>(Error.NotFound);
    }
}
```

### Query Rules

- Query handlers go in `Infrastructure/Persistence/Queries/`
- Always inject the **read context** (`{Service}ReadContext`), never the write context
- No need for `AsNoTracking()` — the read context has it by default
- Return `Result<T>` for single items (may not exist), `Result<IEnumerable<T>>` for collections
- ViewModels/DTOs go in `Application/Queries/`

---

## Domain Layer

### Aggregate Root

```csharp
public class Order : Entity, IAggregateRoot
{
    public string CustomerName { get; private set; } = null!;
    // ... properties with private setters

    protected Order() { }  // EF constructor

    public static Order Create(string customerName /* ... */)
    {
        var order = new Order
        {
            CustomerName = customerName.Trim(),
            // ...
        };

        order.AddDomainEvent(new OrderCreatedDomainEvent(order.Id, order.CustomerName));
        return order;
    }
}
```

### Domain Event

```csharp
using MediatR;
using Qlt.Domain.SeedWork;

public record OrderCreatedDomainEvent(
    Guid OrderId,
    string CustomerName) : IDomainEvent, INotification;
```

### Repository Interface (Domain layer)

```csharp
using Qlt.Domain.SeedWork;

public interface IOrderRepository : IRepository<Order>
{
    Order Add(Order order);
    void Update(Order order);
    Task<Order?> GetAsync(Guid orderId);
}
```

### Repository Implementation (Infrastructure layer)

```csharp
using Qlt.Platform.Persistence.Repositories;

public class OrderRepository : Repository<Order>, IOrderRepository
{
    public OrderRepository(OrderContext context) : base(context) { }
}
```

### Domain Rules

- Entities have **private setters** — mutation through methods only
- Use **factory methods** (`Create(...)`) with business validation
- Raise domain events via `AddDomainEvent()` in factory methods or mutation methods
- Domain events must implement both `IDomainEvent` and `INotification`
- Repository interfaces in Domain, implementations in Infrastructure
- Value Objects use `ValueObject` base class with `GetEqualityComponents()`
- `Entity` base class provides `CreatedAt`, `UpdatedAt`, and `RowVersion` automatically — **never declare these in your entities**. The platform sets `CreatedAt` on insert, `UpdatedAt` on update, and `RowVersion` is managed by the database as a concurrency token

---

## Domain Event Handlers

Domain event handlers run **inside the same transaction** as the command. They can modify aggregates and save integration events to the outbox.

```csharp
public class OrderCreatedDomainEventHandler : IDomainEventHandler<OrderCreatedDomainEvent>
{
    private readonly IOrderRepository _repository;
    private readonly IEventBusOutboxService _outboxService;

    public OrderCreatedDomainEventHandler(
        IOrderRepository repository,
        IEventBusOutboxService outboxService)
    {
        _repository = repository;
        _outboxService = outboxService;
    }

    public async Task Handle(OrderCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        var order = await _repository.GetAsync(notification.OrderId);
        if (order is null) return;

        await _outboxService.AddAndSaveEventAsync(
            new OrderCompletedIntegrationEvent(
                order.Id,
                order.CustomerId,
                order.Total,
                order.Currency));
    }
}
```

### Domain Event Handler Rules

- Keep handlers **lightweight** (under ~100ms)
- **Do NOT** call `SaveChangesAsync()` — the transaction handles it
- **Do NOT** start new transactions
- **Do NOT** call external HTTP APIs or heavy I/O — use integration events for async work
- Use `IEventBusOutboxService.AddAndSaveEventAsync()` to publish integration events
- Handlers CAN query entities and modify aggregates (within same transaction)

---

## Integration Event Handlers (Consuming Events)

When your service consumes events from another service:

```csharp
using Qlt.Platform.Messaging.IntegrationEvents;
using Qlt.Services.Orders.Contracts.IntegrationEvents;  // Reference to producer's Contracts

public class OrderCompletedIntegrationEventHandler : IIntegrationEventHandler<OrderCompletedIntegrationEvent>
{
    private readonly ILogger<OrderCompletedIntegrationEventHandler> _logger;

    public OrderCompletedIntegrationEventHandler(
        ILogger<OrderCompletedIntegrationEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(OrderCompletedIntegrationEvent @event)
    {
        _logger.LogInformation("Processing order completed: {OrderId}", @event.OrderId);

        // Telemetry and idempotency are handled automatically by decorators
        // Just implement business logic here
    }
}
```

### Registration

```csharp
private static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
{
    eventBus.AddSubscription<OrderCompletedIntegrationEvent, OrderCompletedIntegrationEventHandler>();
}
```

### Integration Event Handler Rules

- Implement `IIntegrationEventHandler<T>` — telemetry and idempotency decorators are applied automatically
- Reference the **Contracts project** of the producing service
- Register via `AddSubscription<TEvent, THandler>()` in `AddEventBusSubscriptions`
- The handler runs outside MediatR — it is registered directly in DI

---

## Entity Configurations

Only configure **your domain entities**. Never create configurations for platform tables.

```csharp
internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> entity)
    {
        entity.ToTable("Order");
        entity.HasKey(e => e.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();

        entity.Property(e => e.CustomerName)
            .IsRequired()
            .HasMaxLength(200);

        // Value Objects as owned types
        entity.OwnsOne(e => e.ShippingAddress, addr =>
        {
            addr.Property(a => a.Street).HasMaxLength(200);
            addr.Property(a => a.City).HasMaxLength(100);
        });
    }
}
```

### Entity Configuration Rules

- Configurations go in `Infrastructure/Persistence/EntityConfigurations/`
- Mark as `internal sealed class`
- **Never create configurations for**: `OutboxMessage`, `IdempotencyEntry`, `AuditLog` — the platform manages these
- **Never create or modify migration entries for platform tables** — they are configured automatically by `.WithIdempotency()`, `.WithAuditLogs()`
- Use `ValueGeneratedNever()` for entity IDs (generated in code via `Entity` base constructor)
- Configurations are auto-discovered from the assembly registered via `RegisterModelsFromAssembly`

---

## DbContext

### Write Context

```csharp
public class OrderContext : QltDBContext
{
    public DbSet<Order> Orders { get; set; } = null!;

    public OrderContext(
        DbContextOptions<OrderContext> options,
        IMediator mediator,
        QltDbContextConfiguration configuration)
        : base(options, configuration) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Entity configurations auto-discovered — no manual ApplyConfiguration needed
    }
}
```

### Read Context

```csharp
public class OrderReadContext : QltReadDbContext
{
    public DbSet<Order> Orders { get; set; } = null!;

    public OrderReadContext(
        DbContextOptions<OrderReadContext> options,
        QltReadDbContextConfiguration configuration)
        : base(options, configuration) { }
}
```

### DbContext Rules

- Write context inherits `QltDBContext` — read context inherits `QltReadDbContext`
- Call `base.OnModelCreating(modelBuilder)` in write context — it applies platform configurations
- Read context does **not** need `OnModelCreating` override
- Never call `SaveChanges` on the read context (throws `InvalidOperationException`)

---

## API Endpoints

```csharp
public static class OrdersApi
{
    public static RouteGroupBuilder MapOrdersApiV1(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("api/orders").HasApiVersion(1.0);
        api.MapPut("/", CreateOrderAsync);
        api.MapGet("/{id:guid}", GetOrderByIdAsync);
        api.MapGet("/", GetAllOrdersAsync);
        return api;
    }

    public static async Task<Results<Ok<bool>, ValidationProblem, BadRequest<string>>> CreateOrderAsync(
        [FromHeader(Name = "x-requestid")] Guid requestId,
        CreateOrderRequest request,
        [AsParameters] OrdersServices services)
    {
        if (requestId == Guid.Empty)
            return TypedResults.BadRequest("Empty GUID is not valid for request ID");

        var command = new CreateOrderCommand(/* ... */ requestId: requestId);
        var result = await services.Mediator.Send(command);
        return result.ToHttpResult();
    }

    public static async Task<Results<Ok<OrderViewModel>, NotFound>> GetOrderByIdAsync(
        Guid id,
        [AsParameters] OrdersServices services)
    {
        var result = await services.Mediator.Send(new GetOrderByIdQuery(id));
        return result.IsFailure
            ? TypedResults.NotFound()
            : TypedResults.Ok(result.Value);
    }
}
```

### API Services class

```csharp
public class OrdersServices(
    IMediator mediator,
    IUserHttpContext identityService,
    ILogger<ApiServices> logger) : ApiServices(mediator, identityService, logger) { }
```

### API Rules

- Use `[FromHeader(Name = "x-requestid")]` for idempotent commands
- Validate `requestId != Guid.Empty` before sending
- Use `result.ToHttpResult()` to convert `Result<T>` to HTTP responses automatically
- Duplicate requests return `200 OK` with same body — never `409 Conflict`
- Create a service-specific `{Service}Services` class extending `ApiServices`

---

## Validators

```csharp
public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Customer name is required")
            .MaximumLength(200).WithMessage("Customer name cannot exceed 200 characters");
    }
}
```

### Validator Rules

- Validators go in `Application/Validators/`
- Use FluentValidation's `AbstractValidator<T>`
- Validators are auto-discovered from the assembly registered via `AddValidatorsFromAssembly`
- Validation runs inside the transaction (after `TransactionBehavior`, before handler)

---

## What NOT To Do

### Never create or configure platform infrastructure tables

```csharp
// ❌ WRONG — Platform manages these
public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage> { ... }
public class IdempotencyEntryConfiguration : IEntityTypeConfiguration<IdempotencyEntry> { ... }
entity.ToTable("OutboxMessage"); // in any configuration
entity.ToTable("IdempotencyEntry"); // in any configuration
modelBuilder.Entity<OutboxMessage>(...); // in OnModelCreating
```

### Never define CreatedAt, UpdatedAt, or RowVersion in entities

```csharp
// ❌ WRONG — These are inherited from Entity and configured by the platform
public class Order : Entity, IAggregateRoot
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; }
}
```

### Never call AddMediatR directly

```csharp
// ❌ WRONG
services.AddMediatR(cfg => { ... });

// ✅ CORRECT
services.AddQltMediator(cfg => { ... });
```

### Never register telemetry manually

```csharp
// ❌ WRONG — These are implicit in AddQltMediator
services.AddIntegrationEventTelemetryDecorator();
services.AddDomainEventTelemetryBehavior();
services.AddCommandTelemetryBehavior();
services.AddQueryTelemetryBehavior();
```

### Never call SaveChangesAsync in domain event handlers

```csharp
// ❌ WRONG — CommitTransactionAsync handles the final save
public async Task Handle(OrderCreatedDomainEvent notification, CancellationToken ct)
{
    await _outboxService.AddAndSaveEventAsync(integrationEvent);
    await _context.SaveChangesAsync(ct); // ← NEVER do this
}
```

### Never use the write context for queries

```csharp
// ❌ WRONG
public class GetOrderByIdQueryHandler(OrderContext context) { ... }

// ✅ CORRECT
public class GetOrderByIdQueryHandler(OrderReadContext context) { ... }
```

### Never put domain types in Contracts

```csharp
// ❌ WRONG — Contracts should only have integration events
namespace Qlt.Services.Orders.Contracts;
public class Order : Entity, IAggregateRoot { ... }
```

### Never start transactions manually

```csharp
// ❌ WRONG — TransactionBehavior manages this
await using var tx = await _context.Database.BeginTransactionAsync();
```

### Never publish to event bus directly from handlers

```csharp
// ❌ WRONG — Use the outbox
await _eventBus.PublishAsync(integrationEvent);

// ✅ CORRECT — Saves to outbox within the transaction
await _outboxService.AddAndSaveEventAsync(integrationEvent);
```
