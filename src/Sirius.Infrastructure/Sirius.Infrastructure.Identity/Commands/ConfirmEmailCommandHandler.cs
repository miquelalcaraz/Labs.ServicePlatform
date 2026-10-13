using Microsoft.AspNetCore.Identity;

using Sirius.Contracts.Identity.IntegrationEvents;
using Sirius.Infrastructure.Identity.Models;
using Sirius.Platform;
using Sirius.Platform.Messaging.Commands;
using Sirius.Platform.Messaging.Services;

namespace Sirius.Infrastructure.Identity.Commands;

/// <summary>
/// Activates a user by confirming their email and publishes a UserActivatedIntegrationEvent through the outbox.
/// </summary>
public class ConfirmEmailCommandHandler : ICommandHandler<ConfirmEmailCommand, ConfirmEmailResult>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEventBusOutboxService _outboxService;
    private readonly ILogger<ConfirmEmailCommandHandler> _logger;

    public ConfirmEmailCommandHandler(
        UserManager<ApplicationUser> userManager,
        IEventBusOutboxService outboxService,
        ILogger<ConfirmEmailCommandHandler> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ConfirmEmailResult>> Handle(ConfirmEmailCommand message, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(message.UserId);
        if (user is null)
        {
            _logger.LogWarning("ConfirmEmail - User {UserId} not found.", message.UserId);
            return Result.Success(new ConfirmEmailResult(ConfirmEmailStatus.Error, null));
        }

        if (user.Status == UserStatus.Active)
        {
            _logger.LogInformation("ConfirmEmail - User {UserId} is already active.", message.UserId);
            return Result.Success(new ConfirmEmailResult(ConfirmEmailStatus.AlreadyConfirmed, user.Email));
        }

        user.Activate();
        user.EmailConfirmed = true;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogError(
                "ConfirmEmail - Failed to activate user {UserId}: {Errors}",
                message.UserId,
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return Result.Success(new ConfirmEmailResult(ConfirmEmailStatus.Error, null));
        }

        await _outboxService.AddAndSaveEventAsync(
            new UserActivatedIntegrationEvent(
                UserId: Guid.Parse(user.Id),
                Email: user.Email!,
                TenantId: user.TenantId == Guid.Empty ? null : user.TenantId,
                ActivatedAtUtc: DateTimeOffset.UtcNow,
                CorrelationId: null));

        _logger.LogInformation(
            "ConfirmEmail - User {UserId} activated. UserActivatedIntegrationEvent published.",
            message.UserId);

        return Result.Success(new ConfirmEmailResult(ConfirmEmailStatus.Success, user.Email));
    }
}
