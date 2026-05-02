using Qlt.Platform.Messaging.Commands;

namespace Qlt.Infrastructure.Identity.Commands;

public record CreateLocalTenantCommand(
    Guid TenantId,
    string Name,
    Guid OwnerCompanyId) : ICommand<bool>;
