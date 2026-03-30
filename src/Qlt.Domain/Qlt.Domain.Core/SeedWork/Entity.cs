using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Qlt.Domain.SeedWork;

/// <summary>
/// Base class for all entities in the domain.
/// Entities have a unique identity (Guid) that is generated when the entity is created.
/// </summary>
public abstract class Entity
{
    private int? _requestedHashCode;
    private Guid _id;

    /// <summary>
    /// Unique identifier for the entity.
    /// Generated automatically when the entity is created.
    /// </summary>
    public virtual Guid Id
    {
        get => _id;
        protected set => _id = value;
    }

    /// <summary>
    /// Protected constructor that automatically generates a new GUID for the entity.
    /// This ensures every entity has an identity from the moment it's created.
    /// </summary>
    protected Entity()
    {
        _id = Guid.NewGuid();
    }

    /// <summary>
    /// Protected constructor that allows specifying a custom GUID.
    /// Use this only for special cases like rehydration from events or testing.
    /// </summary>
    /// <param name="id">The specific GUID to assign to the entity</param>
    protected Entity(Guid id)
    {
        _id = id;
    }

    [NotMapped]
    [JsonIgnore]
    private List<IDomainEvent>? _domainEvents;
    public IReadOnlyCollection<IDomainEvent>? DomainEvents => _domainEvents?.AsReadOnly();

    private List<AuditLog> _audits = [];
    [NotMapped]
    [JsonIgnore]
    public IReadOnlyCollection<AuditLog> AuditLogs => _audits.AsReadOnly();

    public Entity AddDomainEvent(IDomainEvent eventItem)
    {
        _domainEvents = _domainEvents ?? new List<IDomainEvent>();
        _domainEvents.Add(eventItem);
        return this;
    }

    public void RemoveDomainEvent(IDomainEvent eventItem)
    {
        _domainEvents?.Remove(eventItem);
    }
    public void RemoveAudits() => _audits.Clear();

    public void ClearDomainEvents()
    {
        _domainEvents?.Clear();
    }

    /// <summary>
    /// Determines whether this entity is transient (not yet persisted).
    /// An entity is transient if its ID is empty (Guid.Empty).
    /// </summary>
    public bool IsTransient()
    {
        return this.Id == Guid.Empty;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity entity)
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        if (GetType() != obj.GetType())
            return false;

        if (entity.IsTransient() || IsTransient())
            return false;

        return entity.Id == Id;
    }

    public override int GetHashCode()
    {
        if (!IsTransient())
        {
            if (!_requestedHashCode.HasValue)
                _requestedHashCode = Id.GetHashCode() ^ 31;

            return _requestedHashCode.Value;
        }
        else
            return base.GetHashCode();
    }

    public static bool operator ==(Entity? left, Entity? right)
    {
        if (Equals(left, null))
            return Equals(right, null);
        else
            return left.Equals(right);
    }

    public static bool operator !=(Entity? left, Entity? right)
    {
        return !(left == right);
    }
}
