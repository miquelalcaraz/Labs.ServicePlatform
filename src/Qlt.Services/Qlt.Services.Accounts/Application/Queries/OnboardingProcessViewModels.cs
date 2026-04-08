namespace Qlt.Services.Accounts.Application.Queries;

public record OnboardingProcessStartedResponse
{
    public Guid OnboardingProcessId { get; init; }
    public Guid SubscriptionId { get; init; }
    public string Status { get; init; } = null!;
    public string CurrentStep { get; init; } = null!;
    public string StatusDisplayName { get; init; } = null!;
    public string CurrentStepDisplayName { get; init; } = null!;
    public DateTimeOffset StartedAtUtc { get; init; }
    public OnboardingLinksResponse Links { get; init; } = null!;
}

public record OnboardingProcessDetailResponse
{
    public Guid OnboardingProcessId { get; init; }
    public Guid? CorrelationId { get; init; }
    public string Status { get; init; } = null!;
    public string StatusDisplayName { get; init; } = null!;
    public string CurrentStep { get; init; } = null!;
    public string CurrentStepDisplayName { get; init; } = null!;
    public int ProgressPercent { get; init; }
    public int RetryCount { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public DateTimeOffset? FailedAtUtc { get; init; }
    public OnboardingSummaryResponse Summary { get; init; } = null!;
    public OnboardingReferencesResponse References { get; init; } = null!;
    public OnboardingErrorResponse? LastError { get; init; }
    public IReadOnlyList<OnboardingStepResponse> Steps { get; init; } = [];
    public OnboardingDetailActionsResponse Actions { get; init; } = null!;
}

public record OnboardingProcessListResponse
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<OnboardingProcessListItemResponse> Items { get; init; } = [];
}

public record OnboardingProcessListItemResponse
{
    public Guid OnboardingProcessId { get; init; }
    public string Status { get; init; } = null!;
    public string StatusDisplayName { get; init; } = null!;
    public string CurrentStep { get; init; } = null!;
    public string CurrentStepDisplayName { get; init; } = null!;
    public string? CompanyLegalName { get; init; }
    public string? SubscriptionPlanCode { get; init; }
    public string? AdminEmail { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public int RetryCount { get; init; }
    public string? LastErrorCode { get; init; }
    public OnboardingListActionsResponse Actions { get; init; } = null!;
}

public record OnboardingProcessRetryResponse
{
    public Guid OnboardingProcessId { get; init; }
    public string Status { get; init; } = null!;
    public string CurrentStep { get; init; } = null!;
    public string StatusDisplayName { get; init; } = null!;
    public string CurrentStepDisplayName { get; init; } = null!;
    public int RetryCount { get; init; }
    public OnboardingLinksResponse Links { get; init; } = null!;
}

public record OnboardingProcessResultResponse
{
    public Guid OnboardingProcessId { get; init; }
    public string Status { get; init; } = null!;
    public OnboardingResultSubscriptionResponse Subscription { get; init; } = null!;
    public OnboardingResultTenantResponse Tenant { get; init; } = null!;
    public OnboardingResultCompanyResponse OwnerCompany { get; init; } = null!;
    public OnboardingResultAdminUserResponse? AdminUser { get; init; }
    public OnboardingResultMembershipResponse? AdminMembership { get; init; }
    public OnboardingLinksResponse Links { get; init; } = null!;
}

public record OnboardingSummaryResponse
{
    public string? CompanyLegalName { get; init; }
    public string? SubscriptionPlanCode { get; init; }
    public string? AdminEmail { get; init; }
}

public record OnboardingReferencesResponse
{
    public Guid SubscriptionId { get; init; }
    public Guid TenantId { get; init; }
    public Guid? OwnerCompanyId { get; init; }
    public Guid? AdminUserId { get; init; }
    public Guid? AdminMembershipId { get; init; }
}

public record OnboardingErrorResponse
{
    public string? Code { get; init; }
    public string? Message { get; init; }
    public string? FailedStep { get; init; }
    public DateTimeOffset? OccurredAtUtc { get; init; }
}

public record OnboardingStepResponse
{
    public string Code { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public string Status { get; init; } = null!;
}

public record OnboardingDetailActionsResponse
{
    public bool CanRetry { get; init; }
    public bool CanRefresh { get; init; }
    public bool CanOpenCustomer { get; init; }
}

public record OnboardingListActionsResponse
{
    public bool CanOpen { get; init; }
    public bool CanRetry { get; init; }
}

public record OnboardingLinksResponse
{
    public string? Self { get; init; }
    public string? Result { get; init; }
}

public record OnboardingResultSubscriptionResponse
{
    public Guid SubscriptionId { get; init; }
    public string? SubscriptionPlanCode { get; init; }
    public string Status { get; init; } = null!;
}

public record OnboardingResultTenantResponse
{
    public Guid TenantId { get; init; }
    public string Name { get; init; } = null!;
    public string Status { get; init; } = null!;
}

public record OnboardingResultCompanyResponse
{
    public Guid CompanyId { get; init; }
    public string LegalName { get; init; } = null!;
    public string TradeName { get; init; } = null!;
    public string CompanyType { get; init; } = null!;
    public string Status { get; init; } = null!;
}

public record OnboardingResultAdminUserResponse
{
    public Guid UserId { get; init; }
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
}

public record OnboardingResultMembershipResponse
{
    public Guid MembershipId { get; init; }
    public Guid TenantId { get; init; }
    public Guid CompanyId { get; init; }
    public string RoleCode { get; init; } = null!;
    public string Status { get; init; } = null!;
}
