using Miqa.Platform.Messaging.Commands;

namespace Miqa.Infrastructure.Identity.Commands;

public record CreateLocalTenantCommand(
    Guid TenantId,
    string Name,
    Guid OwnerCompanyId) : ICommand<bool>;
