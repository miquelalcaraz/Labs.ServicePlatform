using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using Sirius.Platform;
using Sirius.Services.Accounts.Application.Commands;
using Sirius.Services.Accounts.Application.Queries;
using Sirius.Services.Accounts.Authorization;

namespace Sirius.Services.Accounts.APIs;

public static class ActivationApi
{
    private const string BasePath = "/api/accounts/customer-activation";

    public static RouteGroupBuilder MapActivationApiV1(this IEndpointRouteBuilder app)
    {
        var api = app.NewVersionedApi("customer-activation")
            .MapGroup("api/accounts/customer-activation")
            .HasApiVersion(1.0);

        api.MapGet("/", GetActivationListAsync)
            .RequireAuthorization(AccountsAuthorizationPolicies.ActivationRead);

        api.MapGet("/{id:guid}", GetActivationDetailAsync)
            .RequireAuthorization(AccountsAuthorizationPolicies.ActivationRead);

        api.MapPost("/{id:guid}/retry", RetryActivationAsync)
            .RequireAuthorization(AccountsAuthorizationPolicies.ActivationRetry);

        api.MapPost("/{id:guid}/resend", ResendActivationAsync)
            .RequireAuthorization(AccountsAuthorizationPolicies.ActivationResend);

        return api;
    }

    public static async Task<Results<Ok<CustomerActivationProcessListResponse>, BadRequest<ApiErrorResponse>>> GetActivationListAsync(
        [FromQuery] string? status,
        [FromQuery] string? companyLegalName,
        [FromQuery] string? adminEmail,
        [FromQuery] int page = 1,
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
                TraceId = services.CurrentUser.TraceId,
                Errors = new Dictionary<string, string[]> { { "pageSize", ["Page size cannot exceed 100."] } }
            });

        var result = await services.Mediator.Send(new GetCustomerActivationProcessListQuery(
            status, companyLegalName, adminEmail, page, pageSize, sortBy, sortDirection));

        return TypedResults.Ok(result.Value);
    }

    public static async Task<Results<Ok<CustomerActivationProcessDetailResponse>, NotFound<ApiErrorResponse>>> GetActivationDetailAsync(
        Guid id,
        [AsParameters] AccountsServices services)
    {
        var result = await services.Mediator.Send(new GetCustomerActivationProcessByIdQuery(id));

        if (result.IsFailure)
            return TypedResults.NotFound(new ApiErrorResponse
            {
                Code = "NotFound",
                Message = result.Error.Message,
                TraceId = services.CurrentUser.TraceId
            });

        return TypedResults.Ok(result.Value);
    }

    public static async Task<Results<Accepted<CustomerActivationProcessRetryResponse>, NotFound<ApiErrorResponse>, Conflict<ApiErrorResponse>, BadRequest<ApiErrorResponse>>> RetryActivationAsync(
        Guid id,
        [FromHeader(Name = "x-requestid")] Guid requestId,
        RetryActivationProcessRequest? request,
        [AsParameters] AccountsServices services)
    {
        if (requestId == Guid.Empty)
            return TypedResults.BadRequest(new ApiErrorResponse
            {
                Code = "ValidationError",
                Message = "Empty GUID is not valid for request ID",
                TraceId = services.CurrentUser.TraceId
            });

        using (services.Logger.BeginScope(new List<KeyValuePair<string, object>> { new("IdentifiedCommandId", requestId) }))
        {
            var command = new RetryCustomerActivationProcessCommand(
                activationProcessId: id,
                reason: request?.Reason,
                requestId: requestId);

            var result = await services.Mediator.Send(command);

            if (result.IsFailure)
            {
                if (result.Error.Code == Error.NotFound.Code)
                {
                    services.Logger.LogWarning("RetryActivation - NotFound: {ActivationProcessId}", id);
                    return TypedResults.NotFound(new ApiErrorResponse { Code = "NotFound", Message = result.Error.Message, TraceId = services.CurrentUser.TraceId });
                }
                if (result.Error.Code == Error.Conflict.Code)
                {
                    services.Logger.LogWarning("RetryActivation - NotAllowed: {ActivationProcessId}, Reason: {ErrorMessage}", id, result.Error.Message);
                    return TypedResults.Conflict(new ApiErrorResponse { Code = "RetryNotAllowed", Message = result.Error.Message, TraceId = services.CurrentUser.TraceId });
                }
                services.Logger.LogWarning("RetryActivation failed - {ActivationProcessId}, ErrorCode: {ErrorCode}", id, result.Error.Code);
                return TypedResults.BadRequest(ApiErrorResponse.From(result.Error, services.CurrentUser.TraceId));
            }

            var detailResult = await services.Mediator.Send(new GetCustomerActivationProcessByIdQuery(id));
            if (detailResult.IsSuccess)
            {
                var detail = detailResult.Value;
                return TypedResults.Accepted($"{BasePath}/{detail.ActivationProcessId}", new CustomerActivationProcessRetryResponse
                {
                    ActivationProcessId = detail.ActivationProcessId,
                    Status = detail.Status,
                    StatusDisplayName = detail.StatusDisplayName,
                    CurrentStep = detail.CurrentStep,
                    CurrentStepDisplayName = detail.CurrentStepDisplayName,
                    RetryCount = detail.RetryCount,
                    Links = new ActivationLinksResponse { Self = $"{BasePath}/{detail.ActivationProcessId}" }
                });
            }

            return TypedResults.Accepted<CustomerActivationProcessRetryResponse>((string?)null, null);
        }
    }

    public static async Task<Results<Accepted, NotFound<ApiErrorResponse>, Conflict<ApiErrorResponse>, BadRequest<ApiErrorResponse>>> ResendActivationAsync(
        Guid id,
        [FromHeader(Name = "x-requestid")] Guid requestId,
        [AsParameters] AccountsServices services)
    {
        if (requestId == Guid.Empty)
            return TypedResults.BadRequest(new ApiErrorResponse
            {
                Code = "ValidationError",
                Message = "Empty GUID is not valid for request ID",
                TraceId = services.CurrentUser.TraceId
            });

        using (services.Logger.BeginScope(new List<KeyValuePair<string, object>> { new("IdentifiedCommandId", requestId) }))
        {
            var command = new ResendActivationEmailCommand(activationProcessId: id, requestId: requestId);
            var result = await services.Mediator.Send(command);

            if (result.IsFailure)
            {
                if (result.Error.Code == Error.NotFound.Code)
                {
                    services.Logger.LogWarning("ResendActivation - NotFound: {ActivationProcessId}", id);
                    return TypedResults.NotFound(new ApiErrorResponse { Code = "NotFound", Message = result.Error.Message, TraceId = services.CurrentUser.TraceId });
                }
                if (result.Error.Code == Error.Conflict.Code)
                {
                    services.Logger.LogWarning("ResendActivation - NotAllowed: {ActivationProcessId}, Reason: {ErrorMessage}", id, result.Error.Message);
                    return TypedResults.Conflict(new ApiErrorResponse { Code = "ResendNotAllowed", Message = result.Error.Message, TraceId = services.CurrentUser.TraceId });
                }
                return TypedResults.BadRequest(ApiErrorResponse.From(result.Error, services.CurrentUser.TraceId));
            }

            return TypedResults.Accepted($"{BasePath}/{id}");
        }
    }
}

public record RetryActivationProcessRequest(string? Reason = null);
