# Epic 2 — Backlog técnico

Pendientes identificados durante la implementación del Epic 2. Cada ítem incluye contexto, impacto y plan de resolución.

---

## Pendientes activos

### Identity: `UserActivatedIntegrationEvent` nunca se publica

**Severidad**: Alta  
**Área**: `Qlt.Infrastructure.Identity`  
**Bloquea**: Cierre E2E del flujo de activación de cliente

El servicio Identity nunca emite `UserActivatedIntegrationEvent`. El contrato existe en `Qlt.Infrastructure.Identity.Contracts` y el consumidor (`UserActivatedIntegrationEventHandler`) está completamente implementado en `Qlt.Services.Accounts`, pero el lado productor está pendiente.

**Qué existe:**
- `ApplicationUser.Activate()` — establece `Status = UserStatus.Active` (nunca llamado)
- `UserActivatedIntegrationEvent` — contrato definido en `Identity.Contracts` (nunca publicado)
- `UserActivatedIntegrationEventHandler` en Accounts — consumidor completo ✅

**Qué falta:**
- Una Razor Page de confirmación de email (`/Account/ConfirmEmail`) en `Qlt.Infrastructure.Identity` que:
  1. Valide el token de activación via `UserManager.ConfirmEmailAsync`
  2. Llame a `user.Activate()` para actualizar el estado de dominio
  3. Publique `UserActivatedIntegrationEvent` via `IEventBusOutboxService` (outbox)
  4. Redirija al usuario a una pantalla de confirmación post-activación

**Impacto E2E:**  
`CustomerActivationProcess` llega correctamente al estado `WaitingForUserActivation` (el email de activación se envía), pero se queda indefinidamente porque Identity no tiene ningún mecanismo para procesar el clic en el enlace y avanzar el proceso hacia `GoingLive`.

**Plan de resolución:**
- Añadir `/Account/ConfirmEmail` Razor Page en `Qlt.Infrastructure.Identity`
- Inyectar `IEventBusOutboxService` y publicar via outbox (seguir el patrón de `CustomerAdminProvisioningRequestedIntegrationEventHandler`)
- Cubrir con un test funcional en `Services.FunctionalTests` o `Infrastructure.FunctionalTests`

**Referencia**: Epic 2 Phase 5 — el lado productor no fue implementado (solo el consumidor).

---

### Tests funcionales E2E cross-service

**Severidad**: Media  
**Área**: `Services.FunctionalTests`  
**Contexto**: Fase 6

Los tests funcionales actuales validan cada servicio de forma aislada (Accounts, Identity). No existe ningún test que valide el flujo completo inter-servicio:

```
StartActivation → ProvisioningRequested → AdminCreated → ActivationEmailSent
    → UserActivated → CustomerGoLive → CustomerActivationCompleted
```

**Qué falta:**
- Un fixture compartido que levante Accounts + Identity en memoria en el mismo test host
- Un test de tipo `FullActivationFlow_HappyPath_CompletesProcess` que recorra todos los estados
- Manejo del bus de eventos entre servicios en el entorno de test (in-memory event bus o bus simulado)

**Dependencia**: Requiere resolver primero el gap de Identity (`UserActivatedIntegrationEvent`).

---

### Aplicación de mejoras de plataforma en Epic 2

**Severidad**: Baja (limpieza técnica)  
**Área**: `Qlt.Services.Accounts`, `Qlt.Infrastructure.Identity`  
**Referencia plataforma**: [`docs/backlog/platform-observability-improvements.md`](../../../docs/backlog/platform-observability-improvements.md)

Cuando se implementen las dos mejoras de plataforma propuestas, Epic 2 es el primer beneficiario directo:

#### 1. Auto DB health check en `AddQltSDBContext<TContext>`

Una vez implementado en la plataforma, eliminar el boilerplate manual en Accounts:

```csharp
// Eliminar de Accounts DI tras aplicar la mejora de plataforma:
services.AddHealthChecks()
    .AddDbContextCheck<AccountContext>(name: "accounts-db", tags: ["ready"]);
```

#### 2. `CorrelationIdLoggingIntegrationEventDecorator<T>`

Una vez implementado en la plataforma, los handlers de Epic 2 pueden eliminar el `BeginScope` manual de `CorrelationId`:

| Handler | Scope que permanece tras la mejora |
|---------|-----------------------------------|
| `ActivationEmailRequestedIntegrationEventHandler` | Solo `ActivationProcessId` |
| `ActivationDeliveredIntegrationEventHandler` | Solo `ActivationProcessId` |
| `UserActivatedIntegrationEventHandler` | Solo `ActivationProcessId` |

---

## Resueltos en esta épica

| Ítem | Resolución | Fase |
|------|-----------|------|
| Proceso de activación de cliente (dominio) | `CustomerActivationProcess` aggregate con máquina de estados | Fase 1-3 |
| Provisioning de admin en Identity | `CustomerAdminProvisioningRequestedIntegrationEventHandler` | Fase 4 |
| Envío de email de activación | `ActivationEmailRequestedIntegrationEventHandler` + `NotificationsContext` | Fase 4-5 |
| Reintento de proceso fallido | `RetryActivationProcessCommand` + endpoint backoffice | Fase 3-4 |
| Reenvío de email de activación | `ResendActivationEmailCommand` + endpoint backoffice | Fase 4-5 |
| API backoffice (list, detail, retry, resend) | 4 endpoints con versioning, validación e idempotencia | Fase 4-5 |
| Health endpoints + diagnósticos | `/health/live`, `/health/ready`, `CustomerActivationDiagnostics` metrics | Fase 7 |
| Tests funcionales de API | 13 tests en `BackOfficeActivationApiTests` | Fase 6 |
| Tests funcionales de autorización | 10 tests en `BackOfficeActivationAuthorizationTests` | Fase 6 |
