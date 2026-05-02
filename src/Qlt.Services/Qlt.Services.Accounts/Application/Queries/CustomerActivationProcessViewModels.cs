namespace Qlt.Services.Accounts.Application.Queries;

public record CustomerActivationProcessStartedResponse
{
    public Guid ActivationProcessId { get; init; }
    public Guid SubscriptionId { get; init; }
    public string Status { get; init; } = null!;
    public string StatusDisplayName { get; init; } = null!;
    public string CurrentStep { get; init; } = null!;
    public string CurrentStepDisplayName { get; init; } = null!;
    public DateTimeOffset StartedAtUtc { get; init; }
    public ActivationLinksResponse Links { get; init; } = null!;
}

public record CustomerActivationProcessDetailResponse
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
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public ActivationSummaryResponse Summary { get; init; } = null!;
    public ActivationReferencesResponse References { get; init; } = null!;
    public ActivationErrorResponse? LastError { get; init; }
    public IReadOnlyList<ActivationStepResponse> Steps { get; init; } = [];
    public ActivationDetailActionsResponse Actions { get; init; } = null!;
}

public record CustomerActivationProcessListResponse
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<CustomerActivationProcessListItemResponse> Items { get; init; } = [];
}

public record CustomerActivationProcessListItemResponse
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
    public int RetryCount { get; init; }
    public string? LastErrorCode { get; init; }
    public ActivationListActionsResponse Actions { get; init; } = null!;
}

public record CustomerActivationProcessRetryResponse
{
    public Guid ActivationProcessId { get; init; }
    public string Status { get; init; } = null!;
    public string StatusDisplayName { get; init; } = null!;
    public string CurrentStep { get; init; } = null!;
    public string CurrentStepDisplayName { get; init; } = null!;
    public int RetryCount { get; init; }
    public ActivationLinksResponse Links { get; init; } = null!;
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
    public Guid OwnerCompanyId { get; init; }
    public Guid AdminUserId { get; init; }
    public Guid AdminMembershipId { get; init; }
}

public record ActivationErrorResponse
{
    public string? Code { get; init; }
    public string? Message { get; init; }
}

public record ActivationStepResponse
{
    public string Step { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public bool IsCompleted { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsFailed { get; init; }
}

public record ActivationLinksResponse
{
    public string Self { get; init; } = null!;
}

public record ActivationDetailActionsResponse
{
    public bool CanRetry { get; init; }
    public bool CanResend { get; init; }
    public bool IsCompleted { get; init; }
}

public record ActivationListActionsResponse
{
    public bool CanRetry { get; init; }
    public bool CanResend { get; init; }
}
