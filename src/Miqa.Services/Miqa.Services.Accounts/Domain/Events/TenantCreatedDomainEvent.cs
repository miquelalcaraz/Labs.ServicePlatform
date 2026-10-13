using MediatR;

using Miqa.Domain.SeedWork;

namespace Miqa.Services.Accounts.Domain.Events;

public record TenantCreatedDomainEvent(
    Guid TenantId,
    string Name) : IDomainEvent, INotification;
