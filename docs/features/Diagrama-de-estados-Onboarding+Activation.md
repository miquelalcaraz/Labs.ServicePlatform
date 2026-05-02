## Diagrama de estados — Onboarding + Activation

El proceso completo se implementa con **dos aggregates independientes** que se comunican mediante domain events e integration events:

1. **`OnboardingProcess`** — Crea la estructura base (Subscription, Tenant, Company) y provisiona el admin user via Identity.
2. **`CustomerActivationProcess`** — Se crea al completar el onboarding. Gestiona la invitación, activación del usuario y go-live.

---

## 1. OnboardingProcess

**Aggregate:** `Qlt.Services.Accounts.Domain.Models.OnboardingAggregate.OnboardingProcess`

```mermaid
stateDiagram-v2
    [*] --> Draft : Create()

    Draft --> InProgress : Start()

    state InProgress_Steps {
        [*] --> SubscriptionCreated : MarkSubscriptionCreated()
        SubscriptionCreated --> TenantCreated : MarkTenantCreated()
        TenantCreated --> OwnerCompanyCreated : MarkOwnerCompanyCreated()
        OwnerCompanyCreated --> AdminProvisioningRequested : MarkAdminProvisioningRequested()
    }

    InProgress --> WaitingForExternalProvisioning : MarkAdminProvisioningRequested()
    note right of WaitingForExternalProvisioning
        Publica AdminProvisioningRequestedDomainEvent
        Integration event a Identity
    end note

    WaitingForExternalProvisioning --> InProgress : MarkAdminUserProvisioned(userId)
    WaitingForExternalProvisioning --> Failed : Fail()

    state InProgress_PostProvisioning {
        [*] --> AdminUserProvisioned : MarkAdminUserProvisioned()
        AdminUserProvisioned --> AdminMembershipCreated : MarkAdminMembershipCreated()
    }

    InProgress --> Completed : Complete()
    note right of Completed : Requiere step = AdminMembershipCreated

    Failed --> InProgress : Retry()
    note left of Failed
        Retry() + RestartAdminProvisioning()
        si failedStep >= AdminProvisioningRequested
    end note

    Completed --> [*]
```

### Estados (`OnboardingStatus`)

| Valor | Significado |
|-------|-------------|
| `Draft` | Proceso creado, aún no iniciado |
| `InProgress` | Creando estructura base o post-provisioning |
| `WaitingForExternalProvisioning` | Esperando respuesta de Identity |
| `Completed` | Estructura lista — handoff a Activation |
| `Failed` | Fallo reintentable |

### Steps (`OnboardingStep`)

| Valor | Transición |
|-------|-----------|
| `None` | Estado inicial |
| `SubscriptionCreated` | Subscription creada |
| `TenantCreated` | Tenant creado |
| `OwnerCompanyCreated` | Owner company creada |
| `AdminProvisioningRequested` | Provisioning solicitado a Identity |
| `AdminUserProvisioned` | Identity respondió con userId |
| `AdminMembershipCreated` | Membership del admin creada |
| `Completed` | Proceso finalizado |

### Retry

- `Retry()` → Resetea status a `InProgress`, incrementa `RetryCount`
- Si `FailedStep >= AdminProvisioningRequested` → se llama `RestartAdminProvisioning()` que vuelve a `WaitingForExternalProvisioning` y re-publica el domain event

---

## 2. CustomerActivationProcess

**Aggregate:** `Qlt.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate.CustomerActivationProcess`

Se crea al completarse el onboarding (cuando `OnboardingProcess.Complete()` dispara el flujo de handoff).

```mermaid
stateDiagram-v2
    [*] --> Draft : Create()
    note right of Draft : Publica CustomerActivationProcessStartedDomainEvent

    Draft --> InProgress : Start()

    InProgress --> WaitingForNotificationDelivery : RequestActivation()
    note right of WaitingForNotificationDelivery
        Publica ActivationEmailRequestedDomainEvent
        Integration event a Notifications
    end note

    WaitingForNotificationDelivery --> WaitingForUserActivation : MarkActivationDelivered()
    WaitingForNotificationDelivery --> Failed : Fail()

    WaitingForUserActivation --> InProgress : MarkUserActivated(userId)
    note left of WaitingForUserActivation : Publica CustomerAdminActivatedDomainEvent
    WaitingForUserActivation --> Failed : Fail()

    state InProgress_PostActivation {
        [*] --> UserActivated : MarkUserActivated()
        UserActivated --> CustomerReady : MarkCustomerReady()
    }

    InProgress --> Completed : Complete()
    note right of Completed
        Publica CustomerGoLiveDomainEvent
        Integration events:
        CustomerActivationCompletedIntegrationEvent
        CustomerGoLiveIntegrationEvent
    end note

    Failed --> InProgress : Retry()
    note left of Failed
        Retry() + RestartActivationDelivery()
        si step = ActivationRequested
    end note

    Completed --> [*]
```

### Estados (`ActivationStatus`)

| Valor | Significado |
|-------|-------------|
| `Draft` | Proceso creado |
| `InProgress` | Ejecutando pasos pre/post activación |
| `WaitingForNotificationDelivery` | Esperando confirmación de envío de invitación |
| `WaitingForUserActivation` | Invitación entregada, esperando acción del cliente |
| `Completed` | Customer operativo (go-live) |
| `Failed` | Fallo reintentable |

### Steps (`ActivationStep`)

| Valor | Transición |
|-------|-----------|
| `None` | Estado inicial |
| `ActivationRequested` | Invitación solicitada |
| `ActivationDelivered` | Invitación entregada al usuario |
| `UserActivated` | Usuario activó su cuenta |
| `CustomerReady` | Preparación final completada |
| `Completed` | Go-live |

### Retry

- `Retry()` → Resetea status a `InProgress`, incrementa `RetryCount`
- Si `Step == ActivationRequested` → se llama `RestartActivationDelivery()` que vuelve a `WaitingForNotificationDelivery`

---

## 3. Handoff entre procesos

```mermaid
sequenceDiagram
    participant OB as OnboardingProcess
    participant DEH as DomainEventHandler
    participant AP as CustomerActivationProcess

    OB->>OB: Complete()
    Note over OB: Status = Completed<br/>Step = Completed

    OB-->>DEH: OnboardingCompletedDomainEvent<br/>(o integration event)
    DEH->>AP: Create(subscriptionId, tenantId,<br/>ownerCompanyId, adminUserId, adminMembershipId)
    AP->>AP: Start()
    AP->>AP: RequestActivation()
```

---

## 4. Cómo leerlo

El proceso completo queda dividido en 5 macrofases repartidas entre dos aggregates:

1. **Preparación** → `OnboardingProcess.Draft`
2. **Construcción de estructura base** → `OnboardingProcess.InProgress` (Subscription, Tenant, OwnerCompany)
3. **Provisioning del admin** → `OnboardingProcess.WaitingForExternalProvisioning` (espera a Identity)
4. **Activación** → `CustomerActivationProcess` (invitación, entrega, activación del usuario, customer ready)
5. **Go-live** → `CustomerActivationProcess.Completed` (publica integration events para el resto del sistema)