using Miqa.Platform.Messaging.Queries;

namespace Miqa.Services.Accounts.Application.Queries;

/// <summary>
/// Retrieves the result of a completed onboarding process with all created entities.
/// </summary>
public record GetOnboardingProcessResultQuery(Guid Id) : IQuery<OnboardingProcessResultResponse>;
