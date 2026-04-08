using Asp.Versioning;
using Asp.Versioning.Http;

using Microsoft.AspNetCore.Mvc.Testing;

using Qlt.Services.Accounts.Api;
using Qlt.Services.Accounts.Domain.Models.CompanyAggregate;

using System.Net;
using System.Text;
using System.Text.Json;

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
    private readonly WebApplicationFactory<Program> _webApplicationFactory;
    private readonly HttpClient _httpClient;

    public IdentityApiTests(IdentityFixture fixture)
    {
        var handler = new ApiVersionHandler(new QueryStringApiVersionWriter(), new ApiVersion(1.0));
        _webApplicationFactory = fixture;
        _httpClient = _webApplicationFactory.CreateDefaultClient(handler);
    }

    [Fact]
    public async Task InitializeSubscription_WithFullInfrastructure_ReturnsOkTrue()
    {
        var content = BuildContent(FakeInitializeSubscription(), Guid.NewGuid());

        var response = await _httpClient.PutAsync("api/accounts/subscriptions/initialize", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal("true", body);
    }

    [Fact]
    public async Task InitializeSubscription_WithSameRequestId_IdempotencyHoldsAcrossFullInfrastructure()
    {
        var requestId = Guid.NewGuid();
        var payload = FakeInitializeSubscription();

        var content1 = BuildContent(payload, requestId);
        var response1 = await _httpClient.PutAsync("api/accounts/subscriptions/initialize", content1, TestContext.Current.CancellationToken);

        var content2 = BuildContent(payload, requestId);
        var response2 = await _httpClient.PutAsync("api/accounts/subscriptions/initialize", content2, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        var body1 = await response1.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var body2 = await response2.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal("true", body1);
        Assert.Equal("true", body2);
    }

    private static StringContent BuildContent(StartCustomerOnboardingRequest request, Guid requestId)
    {
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        content.Headers.Add("x-requestid", requestId.ToString());
        return content;
    }

    private static StartCustomerOnboardingRequest FakeInitializeSubscription()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        return new(
            PlanCode: "STANDARD",
            CompanyLegalName: $"Identity Flow Test {id} S.L.",
            CompanyTradeName: $"Identity Flow Test {id}",
            TaxId: $"B{id}",
            TaxNumberType: TaxNumberType.Cif,
            Country: "Spain",
            CountryCode: "ES",
            AdminEmail: $"admin-{id}@identityflow.com",
            AdminFirstName: "Jane",
            AdminLastName: "Smith",
            CreatedByInternalUserId: Guid.Parse("9e3163b9-1ae6-4652-9dc6-7898ab7b7a00"),
            TenantName: $"Identity Flow Tenant {id}");
    }
}
