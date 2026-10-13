using Sirius.Platform.Messaging.Queries;

namespace Sirius.Services.Accounts.Application.Queries;

/// <summary>
/// Retrieves the full detail of an onboarding process for UI consumption.
/// </summary>
public record GetOnboardingProcessByIdQuery(Guid Id) : IQuery<OnboardingProcessDetailResponse>;
