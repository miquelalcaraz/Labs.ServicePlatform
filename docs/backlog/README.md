# Backlog — Design Decisions & Parked Improvements

Improvements that were designed but deliberately deferred (YAGNI), and alternatives that were evaluated and rejected. Each document records the rationale and the design, so it can be picked up quickly when a real use case arises.

| Document | Description | Status |
|----------|-------------|--------|
| [MassTransit Comparison](masstransit-comparison.md) | Why the platform keeps its own event bus instead of MassTransit | Evaluated — not adopted |
| [Audit Log Proposal](audit-log-proposal.md) | Automatic ChangeTracker-based audit logging | Proposed |
| [Platform Observability Improvements](platform-observability-improvements.md) | Automatic DB health check in `AddSiriusSDBContext` and correlation-aware log scope for integration event handlers | Proposed |
| [Unit of Work Improvements](uow-future-improvements.md) | Isolation levels, explicit UoW scopes, save-time validation | Parked (YAGNI) |
