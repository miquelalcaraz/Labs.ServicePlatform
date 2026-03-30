namespace Qlt.Domain;

/// <summary>
/// Marker interface for domain events following DDD principles.
/// Domain events represent something that happened in the domain that domain experts care about.
/// This interface is intentionally kept free of infrastructure dependencies (e.g., MediatR).
/// </summary>
public interface IDomainEvent
{
}
