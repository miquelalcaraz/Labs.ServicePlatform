using Miqa.Infrastructure.Identity.Infrastructure;
using Miqa.Infrastructure.Identity.Models;
using Miqa.Infrastructure.Identity.Repositories;
using Miqa.Platform;
using Miqa.Platform.Messaging.Commands;
using Miqa.Platform.Persistence;

namespace Miqa.Infrastructure.Identity.Commands;

/// <summary>
/// Creates a local Tenant projection in Identity when the Accounts service publishes a tenant created event.
/// </summary>
public class CreateLocalTenantCommandHandler
    : ICommandHandler<CreateLocalTenantCommand, bool>
{
    private readonly IdentityContext _identityContext;
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<CreateLocalTenantCommandHandler> _logger;

    public CreateLocalTenantCommandHandler(
        IdentityContext identityContext,
        ITenantRepository tenantRepository,
        ILogger<CreateLocalTenantCommandHandler> logger)
    {
        _identityContext = identityContext ?? throw new ArgumentNullException(nameof(identityContext));
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool>> Handle(CreateLocalTenantCommand message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing create local tenant command: TenantId={TenantId}, Name={Name}",
            message.TenantId,
            message.Name);

        var existingTenant = await _tenantRepository.GetByIdAsync(message.TenantId);
        if (existingTenant is not null)
        {
            _logger.LogInformation(
                "Tenant {TenantId} already exists locally. Skipping creation.",
                message.TenantId);
            return Result.Success(true);
        }

        await ResilientTransaction.New(_identityContext).ExecuteAsync(async () =>
        {
            var tenant = Tenant.Create(
                id: message.TenantId,
                companyId: message.OwnerCompanyId,
                name: message.Name);

            await _tenantRepository.CreateAsync(tenant);
        });

        _logger.LogInformation(
            "Local tenant {TenantId} created for company {CompanyId}.",
            message.TenantId,
            message.OwnerCompanyId);

        return Result.Success(true);
    }
}
