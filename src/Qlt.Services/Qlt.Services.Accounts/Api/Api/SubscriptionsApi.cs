using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using Qlt.Platform;
using Qlt.Platform.Extensions;
using Qlt.Services.Accounts.Application.Commands;
using Qlt.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Qlt.Services.Accounts.Api
{
    public static class SubscriptionsApi
    {
        public static RouteGroupBuilder MapSubscriptionsApiV1(this IEndpointRouteBuilder app)
        {
            var api = app.MapGroup("api/accounts/subscriptions").HasApiVersion(1.0);
            api.MapPut("/initialize", StartCustomerOnboardingAsync);
            return api;
        }

        public static async Task<Results<Ok<bool>, ValidationProblem, BadRequest<string>>> StartCustomerOnboardingAsync(
            [FromHeader(Name = "x-requestid")] Guid requestId,
            StartCustomerOnboardingRequest request,
            [AsParameters] AccountsServices services)
        {
            services.Logger.LogInformation(
                "Sending command: StartCustomerOnboarding - PlanCode: {PlanCode}, Company: {CompanyName}",
                request.PlanCode,
                request.CompanyLegalName);

            if (requestId == Guid.Empty)
            {
                services.Logger.LogWarning("Invalid request - RequestId is missing - {@Request}", request);
                return TypedResults.BadRequest("Empty GUID is not valid for request ID");
            }

            using (services.Logger.BeginScope(new List<KeyValuePair<string, object>> { new("IdentifiedCommandId", requestId) }))
            {
                var command = new StartCustomerOnboardingCommand(
                    request.PlanCode,
                    request.CompanyLegalName,
                    request.CompanyTradeName,
                    request.TaxId,
                    request.TaxNumberType,
                    request.Country,
                    request.CountryCode,
                    request.AdminEmail,
                    request.AdminFirstName,
                    request.AdminLastName,
                    request.CreatedByInternalUserId,
                    request.TenantName,
                    request.State,
                    request.PostalCode,
                    request.City,
                    request.Street,
                    requestId: requestId);

                var result = await services.Mediator.Send(command);

                if (result.IsFailure)
                    services.Logger.LogWarning("StartCustomerOnboardingCommand failed - RequestId: {RequestId}", requestId);
                else
                    services.Logger.LogInformation("StartCustomerOnboardingCommand succeeded - RequestId: {RequestId}", requestId);

                return result.ToHttpResult();
            }
        }
    }

    public record StartCustomerOnboardingRequest(
        string PlanCode,
        string CompanyLegalName,
        string CompanyTradeName,
        string TaxId,
        TaxNumberType TaxNumberType,
        string Country,
        string CountryCode,
        string AdminEmail,
        string AdminFirstName,
        string AdminLastName,
        Guid CreatedByInternalUserId,
        string? TenantName = null,
        string? State = null,
        string? PostalCode = null,
        string? City = null,
        string? Street = null);
}
