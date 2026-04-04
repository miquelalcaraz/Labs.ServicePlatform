using MediatR;

using Qlt.Domain.SeedWork;

namespace Qlt.Services.Accounts.Domain.Events;

public record TenantCreatedDomainEvent(
    Guid TenantId,
    string Name) : IDomainEvent, INotification;
