namespace Qlt.Services.Accounts.Authorization;

/// <summary>
/// Authorization policies for Accounts service endpoints based on the role matrix.
/// </summary>
public static class AccountsAuthorizationPolicies
{
    public const string OnboardingWrite = "Accounts.Onboarding.Write";
    public const string OnboardingRead = "Accounts.Onboarding.Read";
    public const string OnboardingRetry = "Accounts.Onboarding.Retry";

    public const string ActivationRead = "Accounts.Activation.Read";
    public const string ActivationRetry = "Accounts.Activation.Retry";
    public const string ActivationResend = "Accounts.Activation.Resend";

    public static class Roles
    {
        public const string ProjectManager = "ProjectManager";
        public const string BusinessAdmin = "BusinessAdmin";
        public const string SupportAgent = "SupportAgent";
        public const string PlatformAdmin = "PlatformAdmin";
    }
}
