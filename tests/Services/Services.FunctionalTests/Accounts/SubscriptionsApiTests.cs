using System.Net;
using System.Text;
using System.Text.Json;

using Asp.Versioning;
using Asp.Versioning.Http;

using Microsoft.AspNetCore.Mvc.Testing;

using Qlt.Services.Accounts.Api;
using Qlt.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Services.FunctionalTests.Accounts;

public sealed class SubscriptionsApiTests : IClassFixture<AccountsFixture>
{
    private readonly WebApplicationFactory<Program> _webApplicationFactory;
    private readonly HttpClient _httpClient;

    public SubscriptionsApiTests(AccountsFixture fixture)
    {
        var handler = new ApiVersionHandler(new QueryStringApiVersionWriter(), new ApiVersion(1.0));
        _webApplicationFactory = fixture;
        _httpClient = _webApplicationFactory.CreateDefaultClient(handler);
    }

    [Fact]
    public async Task InitializeSubscription_WithValidRequest_ReturnsOkTrue()
    {
        var content = BuildContent(FakeInitializeSubscription(), Guid.NewGuid());

        var response = await _httpClient.PutAsync("api/accounts/subscriptions/initialize", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal("true", body);
    }

    [Fact]
    public async Task InitializeSubscription_WithSameRequestId_IsIdempotent()
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

    [Fact]
    public async Task InitializeSubscription_WithEmptyGuidRequestId_ReturnsBadRequest()
    {
        var content = BuildContent(FakeInitializeSubscription(), Guid.Empty);

        var response = await _httpClient.PutAsync("api/accounts/subscriptions/initialize", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InitializeSubscription_WithMissingPlanCode_ReturnsValidationProblem()
    {
        var request = FakeInitializeSubscription() with { PlanCode = string.Empty };
        var content = BuildContent(request, Guid.NewGuid());

        var response = await _httpClient.PutAsync("api/accounts/subscriptions/initialize", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InitializeSubscription_WithInvalidAdminEmail_ReturnsValidationProblem()
    {
        var request = FakeInitializeSubscription() with { AdminEmail = "not-a-valid-email" };
        var content = BuildContent(request, Guid.NewGuid());

        var response = await _httpClient.PutAsync("api/accounts/subscriptions/initialize", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InitializeSubscription_WithDifferentRequestIds_CreatesTwoIndependentSubscriptions()
    {
        var payload = FakeInitializeSubscription();
        var payload2 = FakeInitializeSubscription();

        var response1 = await _httpClient.PutAsync("api/accounts/subscriptions/initialize", BuildContent(payload, Guid.NewGuid()), TestContext.Current.CancellationToken);
        var response2 = await _httpClient.PutAsync("api/accounts/subscriptions/initialize", BuildContent(payload2, Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
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
            CompanyLegalName: $"Test Corp {id} S.L.",
            CompanyTradeName: $"Test Corp {id}",
            TaxId: $"B{id}",
            TaxNumberType: TaxNumberType.Cif,
            Country: "Spain",
            CountryCode: "ES",
            AdminEmail: $"admin-{id}@functionaltest.com",
            AdminFirstName: "John",
            AdminLastName: "Doe",
            CreatedByInternalUserId: Guid.Parse("9e3163b9-1ae6-4652-9dc6-7898ab7b7a00"),
            TenantName: $"Test Tenant {id}");
    }
}