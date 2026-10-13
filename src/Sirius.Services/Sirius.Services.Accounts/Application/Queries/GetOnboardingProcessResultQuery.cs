using Sirius.Platform.Messaging.Queries;

namespace Sirius.Services.Accounts.Application.Queries;

/// <summary>
/// Retrieves the result of a completed onboarding process with all created entities.
/// </summary>
public record GetOnboardingProcessResultQuery(Guid Id) : IQuery<OnboardingProcessResultResponse>;
