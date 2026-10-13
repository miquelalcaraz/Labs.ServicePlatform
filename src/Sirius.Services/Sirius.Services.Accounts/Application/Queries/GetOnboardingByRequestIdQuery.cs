using Sirius.Platform.Messaging.Queries;

namespace Sirius.Services.Accounts.Application.Queries;

/// <summary>
/// Retrieves an onboarding process detail by its correlation ID (request ID used during creation).
/// </summary>
public record GetOnboardingByRequestIdQuery(Guid CorrelationId) : IQuery<OnboardingProcessDetailResponse>;
