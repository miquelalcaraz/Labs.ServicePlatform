# ADR-002: Separate BackOffice and Manager Apps

**Status:** Accepted  
**Date:** 2025-07-01  
**Context:** Épica 2 — Close Subscription Lifecycle

---

## Context

The platform serves two distinct audiences:

1. **Internal operations** — Platform management, customer onboarding, support (L2/L3)
2. **Customer users** — Extensive business functionality for tenant users

The question is whether to serve both audiences from a single Blazor application or from two separate applications.

Additionally, internal staff occasionally need access to customer-facing features for support purposes.

---

## Decision

**Two separate applications:**

| App | Audience | Purpose |
|-----|----------|---------|
| `QltBackOfficeApp` | Internal operations team | Platform management, onboarding, monitoring, support escalation |
| `QltManagerApp` | Customer tenant users | Business features, tenant administration, day-to-day operations |

Internal staff accesses customer features via `QltManagerApp` using a support membership in the target tenant — not by merging both apps.

---

## Architecture

```
┌──────────────────────────────────────────────────────┐
│                    BFF (YARP)                         │
├──────────────────┬───────────────────────────────────┤
│  /backoffice/*   │  /app/*                           │
│  QltBackOfficeApp│  QltManagerApp                    │
│  (Blazor Server) │  (Blazor Server)                  │
│                  │                                    │
│  Auth:           │  Auth:                            │
│  InternalUser    │  TenantUser (via Membership)      │
│  policies        │  policies                         │
├──────────────────┴───────────────────────────────────┤
│            Shared Microservices (APIs)                │
│    Accounts · Identity · Notifications · ...         │
└──────────────────────────────────────────────────────┘
```

### Shared code (no duplication)

| Layer | Mechanism |
|-------|-----------|
| Contracts (DTOs, requests) | `Qlt.Contracts.*` NuGet packages |
| Reusable UI components | `Qlt.WebApps.Shared` (Razor Class Library) |
| Typed API clients | Shared project or NuGet package |
| Auth token handling | Common library, same BFF with distinct policies per route |

---

## Rationale

### Why separate

| Factor | Benefit |
|--------|---------|
| **Independent deployment** | A backoffice bug does not block customer releases |
| **Security isolation** | Customer app never exposes admin endpoints |
| **Independent scaling** | ManagerApp will have significantly higher traffic |
| **UX optimization** | Each app tailored to its audience without design compromise |
| **Simpler auth model** | Each app has a clear, focused authorization model |
| **Bundle size** | Customers do not load backoffice code they never use |

### Internal support access to customer features

Solved via the existing `Membership` model:

- An internal user gets a support membership in the customer's tenant
- They access `QltManagerApp` with that membership
- No need to merge apps; the domain model already supports this

### Trade-offs accepted

| Cost | Mitigation |
|------|-----------|
| Two CI/CD pipelines | Same monorepo, shared build infrastructure |
| Potential UI duplication | Razor Class Library (`Qlt.WebApps.Shared`) for common components |
| Two apps to maintain | Clear separation reduces cognitive load per app |

---

## Alternatives Considered

### Single unified app

- ❌ Mixed authorization concerns (internal + tenant policies in every page)
- ❌ Larger attack surface (admin routes reachable from customer domain)
- ❌ Coupled deployment cycles
- ❌ Customer UX compromised by admin UI patterns
- Would only make sense if customer features were minimal (< 5 pages) — not the case here

---

## Consequences

1. Create `QltManagerApp` project when customer-facing features begin (Épica 3+)
2. Extract shared UI components to `Qlt.WebApps.Shared` Razor Class Library
3. BFF routes: `/backoffice/*` → BackOfficeApp, `/app/*` → ManagerApp
4. Support membership pattern documented and implemented in Identity service
5. Each app maintains its own set of authorization policies
