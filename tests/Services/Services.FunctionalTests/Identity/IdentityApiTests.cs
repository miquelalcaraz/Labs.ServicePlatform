using System.Net;
using System.Text;
using System.Text.Json;

using Asp.Versioning;
using Asp.Versioning.Http;

using Microsoft.AspNetCore.Mvc.Testing;

using Miqa.Services.Accounts.APIs;
using Miqa.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Services.FunctionalTests.Identity;

/// <summary>
/// Functional tests for the Accounts → Identity cross-service integration flow (Epic 01).
/// These tests verify that the complete onboarding pipeline executes correctly with the
/// full Aspire infrastructure: Accounts service, Identity service, RabbitMQ and SQL Server.
/// </summary>
/// <remarks>
/// Deep Identity-side verification (e.g. user creation) requires query endpoints in the
/// Identity API that are not yet implemented. Those assertions should be added once the
/// Identity query API is available.
/// </remarks>
public sealed class IdentityApiTests : IClassFixture<IdentityFixture>
{
    private const string OnboardingEndpoint = "api/accounts/customer-onboarding";

    private readonly WebApplicationFactory<Program> _webApplicationFactory;
    private readonly HttpClient _httpClient;

    public IdentityApiTests(IdentityFixture fixture)
    {
        var handler = new ApiVersionHandler(new QueryStringApiVersionWriter(), new ApiVersion(1.0));
        _webApplicationFactory = fixture;
        _httpClient = _webApplicationFactory.CreateDefaultClient(handler);
    }

    [Fact]
    public async Task StartOnboarding_WithFullInfrastructure_ReturnsAccepted()
    {
        var content = BuildContent(FakeOnboardingRequest(), Guid.NewGuid());

        var response = await _httpClient.PostAsync(OnboardingEndpoint, content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task StartOnboarding_WithSameRequestId_IdempotencyHoldsAcrossFullInfrastructure()
    {
        var requestId = Guid.NewGuid();
        var payload = FakeOnboardingRequest();

        var content1 = BuildContent(payload, requestId);
        var response1 = await _httpClient.PostAsync(OnboardingEndpoint, content1, TestContext.Current.CancellationToken);

        var content2 = BuildContent(payload, requestId);
        var response2 = await _httpClient.PostAsync(OnboardingEndpoint, content2, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response1.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, response2.StatusCode);
    }

    private static StringContent BuildContent(StartOnboardingProcessRequest request, Guid requestId)
    {
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        content.Headers.Add("x-requestid", requestId.ToString());
        return content;
    }

    private static StartOnboardingProcessRequest FakeOnboardingRequest()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        return new(
            Company: new CompanyInputDto(
                LegalName: $"Identity Flow Test {id} S.L.",
                TaxId: $"B{id}",
                CountryCode: "ES",
                Country: "Spain",
                TradeName: $"Identity Flow Test {id}",
                TaxNumberType: TaxNumberType.Cif),
            Subscription: new SubscriptionInputDto(
                SubscriptionPlanCode: "STANDARD"),
            AdminUser: new AdminUserInputDto(
                FirstName: "Jane",
                LastName: "Smith",
                Email: $"admin-{id}@identityflow.com"));
    }
}
