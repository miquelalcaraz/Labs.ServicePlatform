using Qlt.Platform.Messaging.Queries;

namespace Qlt.Services.Accounts.Application.Queries;

/// <summary>
/// Retrieves the full detail of an onboarding process for UI consumption.
/// </summary>
public record GetOnboardingProcessByIdQuery(Guid Id) : IQuery<OnboardingProcessDetailResponse>;
