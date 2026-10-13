namespace Sirius.BackOfficeApp.Models;

public record ActivationProcessDetailResponse
{
    public Guid ActivationProcessId { get; init; }
    public Guid? CorrelationId { get; init; }
    public string Status { get; init; } = null!;
    public string StatusDisplayName { get; init; } = null!;
    public string CurrentStep { get; init; } = null!;
    public string CurrentStepDisplayName { get; init; } = null!;
    public int ProgressPercent { get; init; }
    public int RetryCount { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset LastUpdatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public ActivationSummaryResponse Summary { get; init; } = null!;
    public ActivationReferencesResponse References { get; init; } = null!;
    public ActivationErrorResponse? LastError { get; init; }
    public IReadOnlyList<ActivationStepResponse> Steps { get; init; } = [];
    public ActivationDetailActionsResponse Actions { get; init; } = null!;
}

public record ActivationProcessListResponse
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<ActivationProcessListItemResponse> Items { get; init; } = [];
}

public record ActivationProcessListItemResponse
{
    public Guid ActivationProcessId { get; init; }
    public string Status { get; init; } = null!;
    public string StatusDisplayName { get; init; } = null!;
    public string CurrentStep { get; init; } = null!;
    public string CurrentStepDisplayName { get; init; } = null!;
    public string? CompanyLegalName { get; init; }
    public string? SubscriptionPlanCode { get; init; }
    public string? AdminEmail { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset LastUpdatedAtUtc { get; init; }
    public int RetryCount { get; init; }
    public string? LastErrorCode { get; init; }
    public ActivationListActionsResponse Actions { get; init; } = null!;
}

public record ActivationProcessRetryResponse
{
    public Guid ActivationProcessId { get; init; }
    public string Status { get; init; } = null!;
    public string CurrentStep { get; init; } = null!;
    public string StatusDisplayName { get; init; } = null!;
    public string CurrentStepDisplayName { get; init; } = null!;
    public int RetryCount { get; init; }
    public DateTimeOffset LastUpdatedAtUtc { get; init; }
    public ActivationLinksResponse Links { get; init; } = null!;
}

public record ActivationProcessResendResponse
{
    public Guid ActivationProcessId { get; init; }
    public string Status { get; init; } = null!;
    public string CurrentStep { get; init; } = null!;
    public string StatusDisplayName { get; init; } = null!;
    public string CurrentStepDisplayName { get; init; } = null!;
}

public record ActivationProcessResultResponse
{
    public Guid ActivationProcessId { get; init; }
    public string Status { get; init; } = null!;
    public ActivationResultSubscriptionResponse Subscription { get; init; } = null!;
    public ActivationResultTenantResponse Tenant { get; init; } = null!;
    public ActivationResultCompanyResponse OwnerCompany { get; init; } = null!;
    public ActivationResultAdminUserResponse? AdminUser { get; init; }
    public ActivationResultMembershipResponse? AdminMembership { get; init; }
    public ActivationResultResponse? Activation { get; init; }
}

public record ActivationSummaryResponse
{
    public string? CompanyLegalName { get; init; }
    public string? SubscriptionPlanCode { get; init; }
    public string? AdminEmail { get; init; }
}

public record ActivationReferencesResponse
{
    public Guid SubscriptionId { get; init; }
    public Guid TenantId { get; init; }
    public Guid? OwnerCompanyId { get; init; }
    public Guid? AdminUserId { get; init; }
    public Guid? AdminMembershipId { get; init; }
}

public record ActivationErrorResponse
{
    public string? Code { get; init; }
    public string? Message { get; init; }
    public string? FailedStep { get; init; }
    public DateTimeOffset? OccurredAtUtc { get; init; }
}

public record ActivationStepResponse
{
    public string Code { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public string Status { get; init; } = null!;
    public DateTimeOffset? CompletedAtUtc { get; init; }
}

public record ActivationDetailActionsResponse
{
    public bool CanRetry { get; init; }
    public bool CanResendActivation { get; init; }
    public bool CanRefresh { get; init; }
    public bool CanOpenCustomer { get; init; }
}

public record ActivationListActionsResponse
{
    public bool CanOpen { get; init; }
    public bool CanRetry { get; init; }
    public bool CanResendActivation { get; init; }
}

public record ActivationLinksResponse
{
    public string? Self { get; init; }
    public string? Result { get; init; }
}

public record ActivationResultResponse
{
    public DateTimeOffset? ActivatedAtUtc { get; init; }
    public DateTimeOffset? GoLiveAtUtc { get; init; }
}

public record ActivationResultSubscriptionResponse
{
    public Guid SubscriptionId { get; init; }
    public string? SubscriptionPlanCode { get; init; }
    public string Status { get; init; } = null!;
}

public record ActivationResultTenantResponse
{
    public Guid TenantId { get; init; }
    public string Name { get; init; } = null!;
    public string Status { get; init; } = null!;
}

public record ActivationResultCompanyResponse
{
    public Guid CompanyId { get; init; }
    public string LegalName { get; init; } = null!;
    public string TradeName { get; init; } = null!;
    public string CompanyType { get; init; } = null!;
    public string Status { get; init; } = null!;
}

public record ActivationResultAdminUserResponse
{
    public Guid UserId { get; init; }
    public string? Email { get; init; }
    public string Status { get; init; } = null!;
}

public record ActivationResultMembershipResponse
{
    public Guid MembershipId { get; init; }
    public string? RoleCode { get; init; }
    public string Status { get; init; } = null!;
}
