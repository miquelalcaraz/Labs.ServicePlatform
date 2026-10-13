using MediatR;

using Miqa.Domain.SeedWork;

namespace Platform.TestWebApi.Infrastructure;

public class TestEntity : Entity, IAggregateRoot
{
    public string Name { get; private set; } = default!;
    public int Value { get; private set; }

    private TestEntity() { }

    public TestEntity(string name, int value)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Value = value;

        this.AddDomainEvent(new TestEntityCreatedDomainEvent(name, value));
    }

    public void UpdateValue(int newValue)
    {
        Value = newValue;
        this.AddDomainEvent(new TestEntityUpdatedDomainEvent(Id, newValue));
    }
}

public record TestEntityCreatedDomainEvent(string Name, int Value) : IDomainEvent, INotification;

public record TestEntityUpdatedDomainEvent(Guid EntityId, int NewValue) : IDomainEvent, INotification;
