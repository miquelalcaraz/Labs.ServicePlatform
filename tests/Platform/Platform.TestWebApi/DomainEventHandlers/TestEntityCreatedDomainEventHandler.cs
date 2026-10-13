using Platform.TestWebApi.Infrastructure;
using Platform.TestWebApi.IntegrationEvents;

using Miqa.Platform.Messaging.DomainEvents;
using Miqa.Platform.Messaging.Services;

namespace Platform.TestWebApi.DomainEventHandlers;

public class TestEntityCreatedDomainEventHandler : IDomainEventHandler<TestEntityCreatedDomainEvent>
{
    private readonly IEventBusOutboxService _outboxService;

    public TestEntityCreatedDomainEventHandler(IEventBusOutboxService outboxService)
    {
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
    }

    public async Task Handle(TestEntityCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new TestEntityCreatedIntegrationEvent(
            notification.Name,
            notification.Value,
            DateTime.UtcNow
        );

        await _outboxService.AddAndSaveEventAsync(integrationEvent);
    }
}
