using Sirius.Contracts.Identity.IntegrationEvents;
using Sirius.Infrastructure.Identity.Infrastructure;
using Sirius.Infrastructure.Identity.Models;
using Sirius.Infrastructure.Identity.Repositories;
using Sirius.Platform;
using Sirius.Platform.Messaging.Commands;
using Sirius.Platform.Messaging.Services;
using Sirius.Platform.Persistence;

namespace Sirius.Infrastructure.Identity.Commands;

/// <summary>
/// Provisions the customer admin user in Identity: ensures tenant exists, creates user if needed,
/// and publishes ExternalUserProvisionedIntegrationEvent through the outbox.
/// </summary>
public class ProvisionCustomerAdminCommandHandler
    : ICommandHandler<ProvisionCustomerAdminCommand, bool>
{
    private readonly IdentityContext _identityContext;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEventBusOutboxService _outboxService;
    private readonly ILogger<ProvisionCustomerAdminCommandHandler> _logger;

    public ProvisionCustomerAdminCommandHandler(
        IdentityContext identityContext,
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IEventBusOutboxService outboxService,
        ILogger<ProvisionCustomerAdminCommandHandler> logger)
    {
        _identityContext = identityContext ?? throw new ArgumentNullException(nameof(identityContext));
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool>> Handle(ProvisionCustomerAdminCommand message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Provisioning admin user for tenant {TenantId}, email={AdminEmail}",
            message.TenantId,
            message.AdminEmail);

        ApplicationUser? adminUser = null;

        await ResilientTransaction.New(_identityContext).ExecuteAsync(async () =>
        {
            await EnsureTenantExistsAsync(message);
            adminUser = await CreateAdminUserIfNotExistsAsync(message);
        });

        await _outboxService.AddAndSaveEventAsync(
            new ExternalUserProvisionedIntegrationEvent(
                Guid.Parse(adminUser!.Id),
                message.TenantId,
                message.CompanyId,
                message.SubscriptionId,
                message.OnboardingProcessId,
                message.AdminEmail,
                message.AdminFirstName,
                message.AdminLastName));

        _logger.LogInformation(
            "Admin user {UserId} provisioned for tenant {TenantId}. Published ExternalUserProvisionedIntegrationEvent.",
            adminUser.Id,
            message.TenantId);

        return Result.Success(true);
    }

    private async Task EnsureTenantExistsAsync(ProvisionCustomerAdminCommand message)
    {
        var tenant = await _tenantRepository.GetByIdAsync(message.TenantId);
        if (tenant is not null)
            return;

        _logger.LogInformation(
            "Tenant {TenantId} not found locally. Creating from provisioning command.",
            message.TenantId);

        var newTenant = Tenant.Create(
            id: message.TenantId,
            companyId: message.CompanyId,
            name: message.TenantName);

        await _tenantRepository.CreateAsync(newTenant);
    }

    private async Task<ApplicationUser> CreateAdminUserIfNotExistsAsync(ProvisionCustomerAdminCommand message)
    {
        var existingUser = await _userRepository.GetByEmailAsync(message.AdminEmail);
        if (existingUser is not null)
        {
            _logger.LogInformation(
                "Admin user with email {AdminEmail} already exists (UserId={UserId}). Skipping creation.",
                message.AdminEmail,
                existingUser.Id);
            return existingUser;
        }

        var adminUser = ApplicationUser.CreateAsUnConfirmed(
            userName: message.AdminEmail,
            email: message.AdminEmail,
            tenantId: message.TenantId,
            firstName: message.AdminFirstName,
            lastName: message.AdminLastName);

        return await _userRepository.CreateAsync(adminUser);
    }
}
