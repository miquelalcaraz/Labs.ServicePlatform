using Sirius.Platform.Messaging.Commands;

namespace Sirius.Infrastructure.Identity.Commands;

public record CreateLocalTenantCommand(
    Guid TenantId,
    string Name,
    Guid OwnerCompanyId) : ICommand<bool>;
