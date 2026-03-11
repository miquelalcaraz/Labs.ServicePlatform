# Integration Event Log Service

## Overview
This service provides the Outbox Pattern implementation for reliably publishing integration events. It stores integration events in a database table and tracks their publication status.

## Recent Refactoring ✅
The service has been refactored to register integration event types at application startup instead of using reflection on each service instantiation.

### Before (❌ Anti-pattern)
```csharp
// Reflection ran on every service instantiation
public IntegrationEventLogService(TContext context)
{
    _eventTypes = Assembly.Load(Assembly.GetEntryAssembly().FullName)
        .GetTypes()
        .Where(t => t.Name.EndsWith(nameof(IntegrationEvent)))
        .ToArray();
}

// Manual registration (2 lines)
services.AddTransient<IIntegrationEventLogService, IntegrationEventLogService<AccountContext>>();
services.AddTransient<IIntegrationEventService, AccountIntegrationEventService>();
```

### After (✅ Best Practice)
```csharp
// Option A: Action configuration (most flexible)
services.AddIntegrationEventLogService<AccountContext, AccountIntegrationEventService>(options =>
{
    options.RegisterEventTypes(typeof(Program).Assembly);
});

// Option B: Convenient shorthand (same result)
services.AddIntegrationEventLogService<AccountContext, AccountIntegrationEventService>(
    typeof(Program).Assembly
);
```

## Usage

### Option 1: Register with Action Configuration (Most Flexible)
Use the `Action<IntegrationEventLogOptions>` pattern for maximum control and customization:

```csharp
services.AddIntegrationEventLogService<AccountContext>(options =>
{
    // Scan single assembly
    options.RegisterEventTypes(typeof(Program).Assembly);

    // Or scan multiple assemblies
    // options.RegisterEventTypes(
    //     typeof(AccountsAssembly).Assembly,
    //     typeof(OrdersAssembly).Assembly
    // );

    // Or register explicit types
    // options.RegisterEventTypes(
    //     typeof(OrganizationCreatedIntegrationEvent),
    //     typeof(OrganizationUpdatedIntegrationEvent)
    // );
});
```

### Option 2: Register with Assembly Scanning (Convenient Shorthand)
Automatically discovers all integration event types in the specified assembly:

```csharp
services.AddIntegrationEventLogService<AccountContext>(typeof(Program).Assembly);
```

### Option 3: Register with Custom IIntegrationEventService (Recommended for Most Services)
Registers both the event log service and your custom integration event service implementation:

```csharp
// With Action configuration
services.AddIntegrationEventLogService<AccountContext, AccountIntegrationEventService>(options =>
{
    options.RegisterEventTypes(typeof(AssemblyReference).Assembly);
});

// Or with convenient shorthand
services.AddIntegrationEventLogService<AccountContext, AccountIntegrationEventService>(
    typeof(AssemblyReference).Assembly
);
```

### Option 4: Register with Multiple Assemblies
Useful when integration events are spread across multiple assemblies:

```csharp
services.AddIntegrationEventLogService<AccountContext>(
    typeof(AccountsAssembly).Assembly,
    typeof(OrdersAssembly).Assembly,
    typeof(CatalogAssembly).Assembly
);
```

### Option 5: Register with Explicit Types
For fine-grained control when you want to explicitly specify which event types to register:

```csharp
services.AddIntegrationEventLogService<AccountContext>(
    typeof(OrganizationCreatedIntegrationEvent),
    typeof(OrganizationUpdatedIntegrationEvent),
    typeof(OrganizationDeletedIntegrationEvent)
);
```

## Benefits of the Refactoring

1. **Performance**: Event type discovery happens once at startup, not on every service instantiation
2. **Explicit Dependencies**: Event types are registered explicitly in DI configuration
3. **Better Testability**: Can inject mock event types for testing
4. **Cleaner Code**: No reflection logic in the service itself
5. **Multiple Assemblies**: Easily support events from different assemblies
6. **Unified Registration**: Optional registration of `IIntegrationEventService` implementation in one call
7. **Flexible Configuration**: Use `Action<IntegrationEventLogOptions>` for advanced scenarios

## Available Overloads

The extension provides **8 overloads** organized in 2 groups:

### Group 1: Register only IntegrationEventLogService

```csharp
// With Action (most flexible)
AddIntegrationEventLogService<TContext>(Action<IntegrationEventLogOptions> configure)

// Convenient shorthands
AddIntegrationEventLogService<TContext>(Assembly assembly)
AddIntegrationEventLogService<TContext>(params Assembly[] assemblies)
AddIntegrationEventLogService<TContext>(params Type[] eventTypes)
```

### Group 2: Register IntegrationEventLogService + Custom IIntegrationEventService

```csharp
// With Action (most flexible)
AddIntegrationEventLogService<TContext, TService>(Action<IntegrationEventLogOptions> configure)

// Convenient shorthands
AddIntegrationEventLogService<TContext, TService>(Assembly assembly)
AddIntegrationEventLogService<TContext, TService>(params Assembly[] assemblies)
AddIntegrationEventLogService<TContext, TService>(params Type[] eventTypes)
```

## IntegrationEventLogOptions API

The `IntegrationEventLogOptions` class provides multiple ways to register event types:

```csharp
options.RegisterEventTypes(IEnumerable<Type> eventTypes)
options.RegisterEventTypes(params Type[] eventTypes)
options.RegisterEventTypes(Assembly assembly)
options.RegisterEventTypes(params Assembly[] assemblies)
```

## Architecture

```
Startup/Configuration
    │
    ├─► AddIntegrationEventLogService<TContext>(Action<Options>)
    │       │
    │       ├─► services.Configure<IntegrationEventLogOptions>(configure)
    │       │       │
    │       │       └─► options.RegisterEventTypes(Assembly/Types)
    │       │               │
    │       │               └─► Scans for IntegrationEvent types
    │       │                       │
    │       │                       └─► Caches in IOptions<IntegrationEventLogOptions>
    │       │
    │       └─► services.AddScoped<IIntegrationEventLogService, IntegrationEventLogService<TContext>>()
    │
    └─► Runtime: IntegrationEventLogService(TContext, IOptions<IntegrationEventLogOptions>)
            │
            └─► Uses cached event types (no reflection at runtime!)
```

## Implementation Details

### Key Components

- **`IntegrationEventLogOptions`**: Configuration class with fluent API for registering event types
  - `RegisterEventTypes(IEnumerable<Type>)`: Register from collection
  - `RegisterEventTypes(params Type[])`: Register explicit types
  - `RegisterEventTypes(Assembly)`: Scan single assembly
  - `RegisterEventTypes(params Assembly[])`: Scan multiple assemblies

- **`ServiceCollectionExtensions`**: Extension methods following .NET conventions
  - Core method: `AddIntegrationEventLogService<TContext>(Action<IntegrationEventLogOptions>)`
  - Convenient overloads for common scenarios (Assembly, Type[])
  - Additional overloads for registering `IIntegrationEventService` implementation

- **`IntegrationEventLogService<TContext>`**: Service implementation that receives event types via `IOptions<IntegrationEventLogOptions>`

### Design Pattern

This implementation follows the **Options Pattern** recommended by Microsoft:

```csharp
// 1. Options class
public class IntegrationEventLogOptions
{
    public void RegisterEventTypes(...) { }
}

// 2. Extension method with Action<TOptions>
services.AddIntegrationEventLogService<TContext>(options =>
{
    options.RegisterEventTypes(...);
});

// 3. Service receives IOptions<TOptions>
public IntegrationEventLogService(TContext context, IOptions<IntegrationEventLogOptions> options)
{
    _eventTypes = options.Value.EventTypes;
}
```

### Event Type Discovery
The extension methods scan assemblies looking for types that:
- Are not abstract
- Are classes
- Inherit from `IntegrationEvent`

## Example: Accounts Service

```csharp
// In Accounts.Infrastructure/Extensions/Extensions.cs

// Before (2 separate registrations)
services.AddIntegrationEventLogService<AccountContext>(typeof(AssemblyReference).Assembly);
services.AddTransient<IIntegrationEventService, AccountIntegrationEventService>();

// After (1 unified registration)
services.AddIntegrationEventLogService<AccountContext, AccountIntegrationEventService>(typeof(AssemblyReference).Assembly);
```

This automatically discovers and registers:
- All integration events in the Accounts.Infrastructure assembly
- `IntegrationEventLogService<AccountContext>` as `IIntegrationEventLogService`
- `AccountIntegrationEventService` as `IIntegrationEventService`
