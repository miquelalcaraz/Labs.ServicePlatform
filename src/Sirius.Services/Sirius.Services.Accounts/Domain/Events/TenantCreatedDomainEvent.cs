using MediatR;

using Sirius.Domain.SeedWork;

namespace Sirius.Services.Accounts.Domain.Events;

public record TenantCreatedDomainEvent(
    Guid TenantId,
    string Name) : IDomainEvent, INotification;
