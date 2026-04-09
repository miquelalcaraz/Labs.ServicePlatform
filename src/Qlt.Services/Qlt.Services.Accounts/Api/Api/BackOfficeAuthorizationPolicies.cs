namespace Qlt.Services.Accounts.Api;

/// <summary>
/// Authorization policies for backoffice onboarding endpoints based on the role matrix.
/// </summary>
public static class BackOfficeAuthorizationPolicies
{
    public const string OnboardingWrite = "BackOffice.Onboarding.Write";
    public const string OnboardingRead = "BackOffice.Onboarding.Read";
    public const string OnboardingRetry = "BackOffice.Onboarding.Retry";

    public static class Roles
    {
        public const string ProjectManager = "ProjectManager";
        public const string BusinessAdmin = "BusinessAdmin";
        public const string SupportAgent = "SupportAgent";
        public const string PlatformAdmin = "PlatformAdmin";
    }
}
