using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using Qlt.Platform;
using Qlt.Services.Accounts.Application.Commands;
using Qlt.Services.Accounts.Application.Queries;
using Qlt.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Qlt.Services.Accounts.Api;

public static class BackOfficeOnboardingApi
{
    private const string BasePath = "/api/backoffice/onboarding-processes";

    public static RouteGroupBuilder MapBackOfficeOnboardingApiV1(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("api/backoffice/onboarding-processes").HasApiVersion(1.0);

        api.MapPost("/", StartOnboardingAsync);
        //.RequireAuthorization(BackOfficeAuthorizationPolicies.OnboardingWrite);

        api.MapGet("/{id:guid}", GetOnboardingDetailAsync);
        //.RequireAuthorization(BackOfficeAuthorizationPolicies.OnboardingRead);

        api.MapGet("/", GetOnboardingListAsync);
        //.RequireAuthorization(BackOfficeAuthorizationPolicies.OnboardingRead);

        api.MapPost("/{id:guid}/retry", RetryOnboardingAsync);
        //.RequireAuthorization(BackOfficeAuthorizationPolicies.OnboardingRetry);

        api.MapGet("/{id:guid}/result", GetOnboardingResultAsync);
        //.RequireAuthorization(BackOfficeAuthorizationPolicies.OnboardingRead);

        return api;
    }

    public static async Task<Results<Accepted<OnboardingProcessStartedResponse>, ValidationProblem, BadRequest<string>>> StartOnboardingAsync(
        [FromHeader(Name = "x-requestid")] Guid requestId,
        StartOnboardingProcessRequest request,
        [AsParameters] AccountsServices services)
    {
        services.Logger.LogInformation(
            "Starting onboarding process - Company: {CompanyLegalName}, Plan: {PlanCode}",
            request.Company.LegalName,
            request.Subscription.SubscriptionPlanCode);

        if (requestId == Guid.Empty)
        {
            services.Logger.LogWarning("Invalid request - RequestId is missing - {@Request}", request);
            return TypedResults.BadRequest("Empty GUID is not valid for request ID");
        }

        using (services.Logger.BeginScope(new List<KeyValuePair<string, object>> { new("IdentifiedCommandId", requestId) }))
        {
            if (!Guid.TryParse(services.IdentityService.GetUserIdentity(), out var internalUserId))
            {
                return TypedResults.BadRequest("Unable to resolve the current user identity.");
            }

            var command = new StartCustomerOnboardingCommand(
                planCode: request.Subscription.SubscriptionPlanCode,
                companyLegalName: request.Company.LegalName,
                companyTradeName: request.Company.TradeName ?? request.Company.LegalName,
                taxId: request.Company.TaxId,
                taxNumberType: request.Company.TaxNumberType == TaxNumberType.Unknown ? TaxNumberType.Vat : request.Company.TaxNumberType,
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
                services.Logger.LogWarning("StartOnboarding failed - RequestId: {RequestId}", requestId);
                return TypedResults.BadRequest(result.Error.Message);
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

    public static async Task<Results<Ok<OnboardingProcessDetailResponse>, NotFound>> GetOnboardingDetailAsync(
        Guid id,
        [AsParameters] AccountsServices services)
    {
        var result = await services.Mediator.Send(new GetOnboardingProcessByIdQuery(id));

        if (result.IsFailure)
            return TypedResults.NotFound();

        return TypedResults.Ok(result.Value);
    }

    public static async Task<Results<Ok<OnboardingProcessListResponse>, ValidationProblem>> GetOnboardingListAsync(
        [FromQuery] string? status,
        [FromQuery] string? companyLegalName,
        [FromQuery] string? adminEmail,
        [FromQuery] string? subscriptionPlanCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "startedAtUtc",
        [FromQuery] string sortDirection = "desc",
        [AsParameters] AccountsServices services = default!)
    {
        if (pageSize > 100)
        {
            var errors = new Dictionary<string, string[]>
            {
                { "pageSize", ["Page size cannot exceed 100."] }
            };
            return TypedResults.ValidationProblem(errors);
        }

        var result = await services.Mediator.Send(new GetOnboardingProcessListQuery(
            status, companyLegalName, adminEmail, subscriptionPlanCode,
            page, pageSize, sortBy, sortDirection));

        return TypedResults.Ok(result.Value);
    }

    public static async Task<Results<Accepted<OnboardingProcessRetryResponse>, NotFound, BadRequest<string>>> RetryOnboardingAsync(
        Guid id,
        [FromHeader(Name = "x-requestid")] Guid requestId,
        RetryOnboardingProcessRequest? request,
        [AsParameters] AccountsServices services)
    {
        services.Logger.LogInformation(
            "Retrying onboarding process {OnboardingProcessId}",
            id);

        if (requestId == Guid.Empty)
        {
            services.Logger.LogWarning("Invalid request - RequestId is missing for retry of {OnboardingProcessId}", id);
            return TypedResults.BadRequest("Empty GUID is not valid for request ID");
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
                    return TypedResults.NotFound();

                return TypedResults.BadRequest(result.Error.Message);
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

    public static async Task<Results<Ok<OnboardingProcessResultResponse>, NotFound, BadRequest<string>>> GetOnboardingResultAsync(
        Guid id,
        [AsParameters] AccountsServices services)
    {
        var result = await services.Mediator.Send(new GetOnboardingProcessResultQuery(id));

        if (result.IsFailure)
        {
            if (result.Error.Code == Error.NotFound.Code)
                return TypedResults.NotFound();

            return TypedResults.BadRequest(result.Error.Message);
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
    TaxNumberType TaxNumberType,
    string Country,
    string CountryCode,
    string? TradeName = null);

public record SubscriptionInputDto(
    string SubscriptionPlanCode);

public record AdminUserInputDto(
    string FirstName,
    string LastName,
    string Email,
    string? PreferredLanguage = null);

public record RetryOnboardingProcessRequest(
    string? Reason = null);
