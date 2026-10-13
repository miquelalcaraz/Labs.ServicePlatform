---
name: aspire
description: >-
  **WORKFLOW SKILL** - Router for running and observing the Sirius Aspire AppHost
  (src/Sirius.Infrastructure/Sirius.Infrastructure.AppHost) with the Aspire CLI. Enforces the
  agent safety guardrails and routes to aspire-orchestration (start/stop/wait/rebuild, file-lock
  recovery) or aspire-monitoring (logs, traces, spans, describe, export). Also explains how to
  verify the building blocks (idempotency, outbox, domain/integration events) end to end through
  traces. USE FOR: run the app, aspire start/stop/wait, aspire describe/ps, aspire logs, aspire
  otel logs/traces/spans, aspire export, "is the outbox publishing", "did the consumer run",
  MSB3491/CS2012 file locks while the app runs. DO NOT USE FOR: unit tests (dotnet test),
  functional tests (they host their own Aspire fixtures, see docs/developer-guide/04-testing.md),
  creating a new AppHost, deployment.
  INVOKES: aspire-orchestration, aspire-monitoring.
license: MIT
metadata:
  author: Microsoft (adapted for Sirius)
  source: dotnet/eShop .github/skills/aspire
---

# Aspire (Sirius)

Use this skill when a task needs the **running** Sirius application — operating the AppHost or its
resources through the Aspire CLI rather than ad-hoc `dotnet`, `docker` or shell workflows.

> Adapted from the Microsoft Aspire agent skills shipped with dotnet/eShop (MIT). The bootstrap
> (`aspire-init`, `aspireify`) and `aspire-deployment` skills were intentionally **not** imported:
> the AppHost is already wired and this lab has no deployment target.

## This Repository

| Item | Value |
|---|---|
| AppHost | `src/Sirius.Infrastructure/Sirius.Infrastructure.AppHost` (C# SDK-style, `AppHost.cs`) |
| Aspire version | 13.6 (`Aspire.AppHost.Sdk/13.6.1`) |
| Resources | `sqlserver` (`SiriusDb`, `IdentityDb`), `cache` (Redis), `eventbus` (Service Bus emulator, topic `sirius-event-bus`), `identity`, `accounts`, `notifications`, `backoffice-bff`, `BackOfficeApp` |
| Transport switch | `#define USE_AZURE_SERVICE_BUS` / `USE_RABBITMQ` in `AppHost.cs` (see README) |
| Requirements | Docker running; Aspire CLI (`dotnet tool install -g Aspire.Cli`) |

If the Aspire CLI is not installed, say so and ask the user before installing a global tool. Humans
may keep using `dotnet run --project src/Sirius.Infrastructure/Sirius.Infrastructure.AppHost`
(foreground, like `aspire run`); agents must use `aspire start`.

## Key Rules

- **Always** `aspire start --non-interactive`, **never** `dotnet run` on the AppHost from an agent
- **Always** `aspire wait <resource>`, **never** manual HTTP polling
- **Always** `aspire stop` before rebuilding when the app is running — `MSB3491` / `CS2012` mean
  Aspire holds the output files, not that the project is broken
- Use `aspire resource <name> rebuild` when only one service changed; rerun `aspire start` only when
  `AppHost.cs` changed
- Use `--format Json` for anything you parse; `--include-hidden` when a resource seems missing
- Check `aspire docs search <topic>` / `aspire docs api search <query> --language csharp` before
  editing `AppHost.cs`; use the `dotnet-inspect` skill for local overloads
- **Never** install the obsolete Aspire workload
- Stop the app (`aspire stop`) when you are done unless the user asked to keep it running

## Routing

| Task | Route To |
|------|----------|
| Start, stop, wait, restart, rebuild, file locks, port conflicts | → [aspire-orchestration](../aspire-orchestration/SKILL.md) |
| Logs, traces, spans, resource state, telemetry export | → [aspire-monitoring](../aspire-monitoring/SKILL.md) |
| Change `AppHost.cs` (new resource, subscription, env var) | Edit directly after `aspire docs search`; the change belongs to the platform architect or feature developer skill that owns it |

## Verifying the Building Blocks End to End

Unit tests do not cover the cross-service path. When a change touches transactions, idempotency, the
outbox or the event bus, run the app and check the trace (span names are documented in
[docs/architecture/06-telemetry.md](../../../docs/architecture/06-telemetry.md#trace-structure)):

```bash
aspire start --non-interactive
aspire wait accounts
aspire wait notifications
# trigger the flow (BackOffice UI, or PUT /api/accounts/companies with an x-requestid header)
aspire otel traces accounts --search "CreateCompanyCommand"
aspire otel spans --search "\"Save Event to Outbox\""
aspire otel spans --search "\"Publish Outbox Events\""
aspire otel spans notifications --search "CompanyCreatedIntegrationEvent"
aspire otel logs --search "severity:error"
aspire stop
```

What to look for:

| Building block | Expected evidence |
|---|---|
| Command idempotency | `Idempotency Check (Command)` span; a replay with the same `x-requestid` returns `200` without a new `Execute …` span |
| Single transaction + domain events | `Transaction …` → `DomainEventProcessor …` → `Handle …DomainEvent` → `Save Event to Outbox` in one trace |
| Outbox publishing | `Publish Outbox Events` + `Publish {Event}` (Producer) after the commit; nested commands publish later via `Outbox Background Processing` |
| Idempotent consumer | Consumer span in the other service joined to the same trace, followed by `Idempotency Check (Event)` |

## Prerequisites

| Requirement | Install |
|-------------|---------|
| .NET 10.0 SDK | https://dotnet.microsoft.com/download |
| Aspire CLI (NativeAOT global tool) | `dotnet tool install -g Aspire.Cli` |
| Docker Desktop (or Podman) | https://www.docker.com/products/docker-desktop/ |

## References

- [../aspire-orchestration/references/agent-workflows.md](../aspire-orchestration/references/agent-workflows.md) — worktrees, code changes, investigation, integrations
- [../aspire-orchestration/references/app-commands.md](../aspire-orchestration/references/app-commands.md) — app lifecycle and discovery commands
- [../aspire-orchestration/references/resource-management.md](../aspire-orchestration/references/resource-management.md) — resource wait and resource commands
- [../aspire-monitoring/references/monitoring.md](../aspire-monitoring/references/monitoring.md) — logs, traces, search filtering, export
