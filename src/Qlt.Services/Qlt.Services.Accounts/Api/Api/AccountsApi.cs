using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using Qlt.Platform;
using Qlt.Platform.Extensions;
using Qlt.Services.Accounts.Application.Commands;
using Qlt.Services.Accounts.Application.Queries;
using Qlt.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Qlt.Services.Accounts.Api
{
    public static class AccountsApi
    {
        public static RouteGroupBuilder MapAccountsApiV1(this IEndpointRouteBuilder app)
        {
            var api = app.MapGroup("api/accounts/companies").HasApiVersion(1.0);
            api.MapPut("/", CreateCompanyAsync);
            api.MapGet("/{id:guid}", GetCompanyByIdAsync);
            api.MapGet("/", GetAllCompaniesAsync);
            return api;
        }

        public static async Task<Results<Ok<bool>, BadRequest<ApiErrorResponse>>> CreateCompanyAsync(
            [FromHeader(Name = "x-requestid")] Guid requestId,
            CreateCompanyRequest request,
            HttpContext context,
            [AsParameters] AccountsServices services)
        {
            services.Logger.LogInformation(
                "Sending command: {CommandName} - {IdProperty}: {CommandId}",
                request.GetGenericTypeName(),
                nameof(request.TaxId),
                request.TaxId);

            if (requestId == Guid.Empty)
            {
                services.Logger.LogWarning("Invalid request - RequestId is missing - {@Request}", request);
                return TypedResults.BadRequest(new ApiErrorResponse { Code = "ValidationError", Message = "Empty GUID is not valid for request ID", TraceId = context.TraceIdentifier });
            }

            using (services.Logger.BeginScope(new List<KeyValuePair<string, object>> { new("IdentifiedCommandId", requestId) }))
            {
                var command = new CreateCompanyCommand(
                    request.TenantId,
                    request.LegalName,
                    request.TradeName,
                    request.TaxId,
                    request.Country,
                    request.CountryCode,
                    request.CompanyType,
                    request.TaxNumberType,
                    request.State,
                    request.PostalCode,
                    request.City,
                    request.Street,
                    requestId: requestId);

                var result = await services.Mediator.Send(command);

                if (result.IsFailure)
                    services.Logger.LogWarning("CreateCompanyCommand failed - RequestId: {RequestId}", requestId);
                else
                    services.Logger.LogInformation("CreateCompanyCommand succeeded - RequestId: {RequestId}", requestId);

                return result.ToHttpResult(context.TraceIdentifier);
            }
        }

        public static async Task<Results<Ok<CompanyDetail>, NotFound<ApiErrorResponse>>> GetCompanyByIdAsync(
            Guid id,
            HttpContext context,
            [AsParameters] AccountsServices services)
        {
            var result = await services.Mediator.Send(new GetCompanyByIdQuery(id));

            if (result.IsFailure)
                return TypedResults.NotFound(new ApiErrorResponse { Code = "NotFound", Message = "Company not found.", TraceId = context.TraceIdentifier });

            return TypedResults.Ok(result.Value);
        }

        public static async Task<Ok<IEnumerable<CompanySummary>>> GetAllCompaniesAsync(
            [AsParameters] AccountsServices services)
        {
            var result = await services.Mediator.Send(new GetAllCompaniesQuery());
            return TypedResults.Ok(result.Value);
        }
    }

    public record CreateCompanyRequest(
        Guid TenantId,
        string LegalName,
        string TradeName,
        string TaxId,
        TaxNumberType TaxNumberType,
        string Country,
        string CountryCode,
        CompanyType CompanyType = CompanyType.Owner,
        string? State = null,
        string? PostalCode = null,
        string? City = null,
        string? Street = null);
}
