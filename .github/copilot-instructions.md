# Copilot Instructions

## General Guidelines
- All code including comments, messages, labels, and strings must be written in English, regardless of the conversation language with the user being in Spanish.

## Project Guidelines
- Working on a microservices architecture with reusable NuGet packages. Currently implementing core components in the "Core" folder as projects (not yet packaged as NuGets) referenced by initial services like Accounts and Identity. 
- Project uses .NET 10, C# 14.0, and Blazor. Implementing patterns like idempotency, domain events, and integration events.
- **Critical Patterns Implemented**: 
  - Result Pattern for Railway-Oriented Programming
  - CQRS with MediatR (Commands with ICommand<T>)
  - Domain Events with IDomainEvent
  - Idempotency with database-enforced unique constraint
  - Generic RequestManager<TContext> for any DbContext
  - Transactional Outbox
  - Idempotent Consumer
  - Unit of Work
  - Pipeline Behavior
  - Eventual Consistency

## Razor Pages and Components
- When modifying Razor pages or components, always consult Radzen documentation first using #radzen_search to ensure compatibility with Radzen Blazor component library best practices and recommendations.

