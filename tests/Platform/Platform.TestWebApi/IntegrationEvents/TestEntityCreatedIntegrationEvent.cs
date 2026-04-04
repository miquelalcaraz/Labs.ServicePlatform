using Qlt.Platform.Messaging.IntegrationEvents;

namespace Platform.TestWebApi.IntegrationEvents;

public record TestEntityCreatedIntegrationEvent(
    string Name,
    int Value,
    DateTime CreatedAt
) : IntegrationEvent;
