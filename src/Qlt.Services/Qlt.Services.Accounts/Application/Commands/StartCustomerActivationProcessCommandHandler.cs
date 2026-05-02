using Microsoft.Extensions.Logging;

using Qlt.Platform;
using Qlt.Platform.Messaging.Commands;
using Qlt.Services.Accounts.Domain.Models.CustomerActivationProcessAggregate;
using Qlt.Services.Accounts.Domain.Repositories;

namespace Qlt.Services.Accounts.Application.Commands;

public class StartCustomerActivationProcessCommandHandler
    : ICommandHandler<StartCustomerActivationProcessCommand, bool>
{
    private readonly ICustomerActivationProcessRepository _repository;
    private readonly ILogger<StartCustomerActivationProcessCommandHandler> _logger;

    public StartCustomerActivationProcessCommandHandler(
        ICustomerActivationProcessRepository repository,
        ILogger<StartCustomerActivationProcessCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<bool>> Handle(StartCustomerActivationProcessCommand message, CancellationToken cancellationToken)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = message.RequestId,
            ["SubscriptionId"] = message.SubscriptionId
        }))
        {
            var existing = await _repository.GetBySubscriptionIdAsync(message.SubscriptionId);
            if (existing is not null)
            {
                _logger.LogInformation(
                    "Activation process already exists for subscription {SubscriptionId}. ProcessId={ProcessId}",
                    message.SubscriptionId, existing.Id);
                return Result.Success(true);
            }

            var process = CustomerActivationProcess.Create(
                message.SubscriptionId,
                message.TenantId,
                message.OwnerCompanyId,
                message.AdminUserId,
                message.AdminMembershipId,
                message.RequestId,
                message.CompanyLegalName,
                message.SubscriptionPlanCode,
                message.AdminEmail);

            process.Start();
            process.RequestActivation();

            _repository.Add(process);

            var saved = await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

            if (saved <= 0)
                return Result.Failure<bool>(Error.BadRequest.WithMessage("Failed to start customer activation process."));

            _logger.LogInformation(
                "Customer activation process started. ProcessId={ProcessId}, SubscriptionId={SubscriptionId}",
                process.Id, process.SubscriptionId);

            CustomerActivationDiagnostics.ProcessesStarted.Add(1,
                new KeyValuePair<string, object?>("subscription_plan", message.SubscriptionPlanCode ?? "unknown"));

            return Result.Success(true);
        }
    }
}
