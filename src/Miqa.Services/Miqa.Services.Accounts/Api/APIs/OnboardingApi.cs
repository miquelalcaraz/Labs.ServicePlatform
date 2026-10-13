using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using Miqa.Platform;
using Miqa.Services.Accounts.Application.Commands;
using Miqa.Services.Accounts.Application.Queries;
using Miqa.Services.Accounts.Authorization;
using Miqa.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Miqa.Services.Accounts.APIs;

public static class OnboardingApi
{
    private const string BasePath = "/api/accounts/customer-onboarding";

    public static RouteGroupBuilder MapOnboardingApiV1(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("api/accounts/customer-onboarding").HasApiVersion(1.0);

        api.MapPost("/", StartOnboardingAsync)
        .RequireAuthorization(AccountsAuthorizationPolicies.OnboardingWrite);

        api.MapGet("/{id:guid}", GetOnboardingDetailAsync)
        .RequireAuthorization(AccountsAuthorizationPolicies.OnboardingRead);

        api.MapGet("/", GetOnboardingListAsync)
        .RequireAuthorization(AccountsAuthorizationPolicies.OnboardingRead);

        api.MapPost("/{id:guid}/retry", RetryOnboardingAsync)
        .RequireAuthorization(AccountsAuthorizationPolicies.OnboardingRetry);

        api.MapGet("/{id:guid}/result", GetOnboardingResultAsync)
        .RequireAuthorization(AccountsAuthorizationPolicies.OnboardingRead)
        .WithSummary("Get the result of a completed onboarding process.")
        .WithDescription("Returns the resolved entities created during the onboarding: Subscription, Tenant, Owner Company, Admin User and Admin Membership. Only available when the process status is Completed.");

        return api;
    }

    public static async Task<Results<Accepted<OnboardingProcessStartedResponse>, BadRequest<ApiErrorResponse>>> StartOnboardingAsync(
        [FromHeader(Name = "x-requestid")] Guid requestId,
        StartOnboardingProcessRequest request,        [AsParameters] AccountsServices services)
    {
        services.Logger.LogInformation(
            "Starting onboarding process - Company: {CompanyLegalName}, Plan: {PlanCode}",
            request.Company.LegalName,
            request.Subscription.SubscriptionPlanCode);

        if (requestId == Guid.Empty)
        {
            services.Logger.LogWarning("Invalid request - RequestId is missing - {@Request}", request);
            return TypedResults.BadRequest(new ApiErrorResponse { Code = "ValidationError", Message = "Empty GUID is not valid for request ID", TraceId = services.UserHttpContext.TraceId });
        }

        using (services.Logger.BeginScope(new List<KeyValuePair<string, object>> { new("IdentifiedCommandId", requestId) }))
        {
            if (!Guid.TryParse(services.UserHttpContext.GetUserIdentity(), out var internalUserId))
            {
                return TypedResults.BadRequest(new ApiErrorResponse { Code = "Unauthorized", Message = "Unable to resolve the current user identity.", TraceId = services.UserHttpContext.TraceId });
            }

            var command = new StartCustomerOnboardingCommand(
                planCode: request.Subscription.SubscriptionPlanCode,
                companyLegalName: request.Company.LegalName,
                companyTradeName: string.IsNullOrWhiteSpace(request.Company.TradeName) ? request.Company.LegalName : request.Company.TradeName,
                taxId: request.Company.TaxId,
                taxNumberType: request.Company.TaxNumberType ?? TaxNumberType.Vat,
                country: request.Company.Country ?? request.Company.CountryCode,
                countryCode: request.Company.CountryCode,
                adminEmail: request.AdminUser.Email,
                adminFirstName: request.AdminUser.FirstName,
                adminLastName: request.AdminUser.LastName,
                createdByInternalUserId: internalUserId,
                tenantName: request.Company.LegalName,
                requestId: requestId);

            var result = await services.Mediator.Send(command);

            if (result.IsFailure)
            {
                services.Logger.LogWarning(
                    "StartOnboarding failed - RequestId: {RequestId}, ErrorCode: {ErrorCode}, Error: {ErrorMessage}",
                    requestId, result.Error.Code, result.Error.Message);
                return TypedResults.BadRequest(ApiErrorResponse.From(result.Error, services.UserHttpContext.TraceId));
            }

            var detailResult = await services.Mediator.Send(new GetOnboardingByRequestIdQuery(requestId));
            if (detailResult.IsSuccess)
            {
                var detail = detailResult.Value;
                var response = new OnboardingProcessStartedResponse
                {
                    OnboardingProcessId = detail.OnboardingProcessId,
                    SubscriptionId = detail.References.SubscriptionId,
                    Status = detail.Status,
                    CurrentStep = detail.CurrentStep,
                    StatusDisplayName = detail.StatusDisplayName,
                    CurrentStepDisplayName = detail.CurrentStepDisplayName,
                    StartedAtUtc = detail.StartedAtUtc,
                    Links = new OnboardingLinksResponse
                    {
                        Self = $"{BasePath}/{detail.OnboardingProcessId}",
                        Result = $"{BasePath}/{detail.OnboardingProcessId}/result"
                    }
                };

                return TypedResults.Accepted($"{BasePath}/{detail.OnboardingProcessId}", response);
            }

            return TypedResults.Accepted<OnboardingProcessStartedResponse>((string?)null, null);
        }
    }

    public static async Task<Results<Ok<OnboardingProcessDetailResponse>, NotFound<ApiErrorResponse>>> GetOnboardingDetailAsync(
        Guid id,        [AsParameters] AccountsServices services)
    {
        var result = await services.Mediator.Send(new GetOnboardingProcessByIdQuery(id));

        if (result.IsFailure)
            return TypedResults.NotFound(new ApiErrorResponse { Code = "NotFound", Message = "Onboarding process not found.", TraceId = services.UserHttpContext.TraceId });

        return TypedResults.Ok(result.Value);
    }

    public static async Task<Results<Ok<OnboardingProcessListResponse>, BadRequest<ApiErrorResponse>>> GetOnboardingListAsync(
        [FromQuery] string? status,
        [FromQuery] string? companyLegalName,
        [FromQuery] string? adminEmail,
        [FromQuery] string? subscriptionPlanCode,        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "startedAtUtc",
        [FromQuery] string sortDirection = "desc",
        [AsParameters] AccountsServices services = default!)
    {
        if (pageSize > 100)
            return TypedResults.BadRequest(new ApiErrorResponse
            {
                Code = "ValidationError",
                Message = "The request query is invalid.",
                TraceId = services.UserHttpContext.TraceId,
                Errors = new Dictionary<string, string[]> { { "pageSize", ["Page size cannot exceed 100."] } }
            });

        var result = await services.Mediator.Send(new GetOnboardingProcessListQuery(
            status, companyLegalName, adminEmail, subscriptionPlanCode,
            page, pageSize, sortBy, sortDirection));

        return TypedResults.Ok(result.Value);
    }

    public static async Task<Results<Accepted<OnboardingProcessRetryResponse>, NotFound<ApiErrorResponse>, Conflict<ApiErrorResponse>, BadRequest<ApiErrorResponse>>> RetryOnboardingAsync(
        Guid id,
        [FromHeader(Name = "x-requestid")] Guid requestId,
        RetryOnboardingProcessRequest? request,        [AsParameters] AccountsServices services)
    {
        services.Logger.LogInformation(
            "Retrying onboarding process {OnboardingProcessId}",
            id);

        if (requestId == Guid.Empty)
        {
            services.Logger.LogWarning("Invalid request - RequestId is missing for retry of {OnboardingProcessId}", id);
            return TypedResults.BadRequest(new ApiErrorResponse { Code = "ValidationError", Message = "Empty GUID is not valid for request ID", TraceId = services.UserHttpContext.TraceId });
        }

        using (services.Logger.BeginScope(new List<KeyValuePair<string, object>> { new("IdentifiedCommandId", requestId) }))
        {
            var command = new RetryOnboardingProcessCommand(
                onboardingProcessId: id,
                reason: request?.Reason,
                requestId: requestId);

            var result = await services.Mediator.Send(command);

            if (result.IsFailure)
            {
                if (result.Error.Code == Error.NotFound.Code)
                {
                    services.Logger.LogWarning(
                        "RetryOnboarding - NotFound: {OnboardingProcessId}",
                        id);
                    return TypedResults.NotFound(new ApiErrorResponse { Code = "NotFound", Message = result.Error.Message, TraceId = services.UserHttpContext.TraceId });
                }

                if (result.Error.Code == Error.Conflict.Code)
                {
                    services.Logger.LogWarning(
                        "RetryOnboarding - RetryNotAllowed: {OnboardingProcessId}, Reason: {ErrorMessage}",
                        id, result.Error.Message);
                    return TypedResults.Conflict(new ApiErrorResponse { Code = "RetryNotAllowed", Message = result.Error.Message, TraceId = services.UserHttpContext.TraceId });
                }

                services.Logger.LogWarning(
                    "RetryOnboarding failed - {OnboardingProcessId}, ErrorCode: {ErrorCode}, Error: {ErrorMessage}",
                    id, result.Error.Code, result.Error.Message);
                return TypedResults.BadRequest(ApiErrorResponse.From(result.Error, services.UserHttpContext.TraceId));
            }

            var detailResult = await services.Mediator.Send(new GetOnboardingProcessByIdQuery(id));
            if (detailResult.IsSuccess)
            {
                var detail = detailResult.Value;
                var response = new OnboardingProcessRetryResponse
                {
                    OnboardingProcessId = detail.OnboardingProcessId,
                    Status = detail.Status,
                    CurrentStep = detail.CurrentStep,
                    StatusDisplayName = detail.StatusDisplayName,
                    CurrentStepDisplayName = detail.CurrentStepDisplayName,
                    RetryCount = detail.RetryCount,
                    Links = new OnboardingLinksResponse
                    {
                        Self = $"{BasePath}/{detail.OnboardingProcessId}"
                    }
                };

                return TypedResults.Accepted($"{BasePath}/{detail.OnboardingProcessId}", response);
            }

            return TypedResults.Accepted<OnboardingProcessRetryResponse>((string?)null, null);
        }
    }

    public static async Task<Results<Ok<OnboardingProcessResultResponse>, NotFound<ApiErrorResponse>, BadRequest<ApiErrorResponse>>> GetOnboardingResultAsync(
        Guid id,        [AsParameters] AccountsServices services)
    {
        var result = await services.Mediator.Send(new GetOnboardingProcessResultQuery(id));

        if (result.IsFailure)
        {
            if (result.Error.Code == Error.NotFound.Code)
                return TypedResults.NotFound(new ApiErrorResponse { Code = "NotFound", Message = "Onboarding process not found.", TraceId = services.UserHttpContext.TraceId });

            return TypedResults.BadRequest(ApiErrorResponse.From(result.Error, services.UserHttpContext.TraceId));
        }

        return TypedResults.Ok(result.Value);
    }
}

public record StartOnboardingProcessRequest(
    CompanyInputDto Company,
    SubscriptionInputDto Subscription,
    AdminUserInputDto AdminUser);

public record CompanyInputDto(
    string LegalName,
    string TaxId,
    string CountryCode,
    string? Country = null,
    string? TradeName = null,
    TaxNumberType? TaxNumberType = null);

public record SubscriptionInputDto(
    string SubscriptionPlanCode);

public record AdminUserInputDto(
    string FirstName,
    string LastName,
    string Email,
    string? PreferredLanguage = null);

public record RetryOnboardingProcessRequest(
    string? Reason = null);
