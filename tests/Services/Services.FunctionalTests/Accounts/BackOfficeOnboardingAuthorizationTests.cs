using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Asp.Versioning;
using Asp.Versioning.Http;

using Sirius.Services.Accounts.APIs;
using Sirius.Services.Accounts.Domain.Models.CompanyAggregate;

using static Sirius.Services.Accounts.Authorization.AccountsAuthorizationPolicies;

namespace Services.FunctionalTests.Accounts;

/// <summary>
/// Validates the onboarding authorization matrix:
/// OnboardingWrite  → ProjectManager, BusinessAdmin, PlatformAdmin (NOT SupportAgent)
/// OnboardingRead   → all four roles
/// OnboardingRetry  → ProjectManager, SupportAgent, PlatformAdmin  (NOT BusinessAdmin)
/// </summary>
public sealed class OnboardingAuthorizationTests : IClassFixture<AccountsFixture>
{
    private const string BasePath = "api/accounts/customer-onboarding";

    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OnboardingAuthorizationTests(AccountsFixture fixture)
    {
        var handler = new ApiVersionHandler(new QueryStringApiVersionWriter(), new ApiVersion(1.0));
        _httpClient = fixture.CreateDefaultClient(handler);
    }

    // ── Negative: roles that must be DENIED ──────────────────────────────────

    [Fact]
    public async Task StartOnboarding_AsSupportAgent_Returns403()
    {
        var response = await SendAsRole(HttpMethod.Post, BasePath, Roles.SupportAgent,
            BuildStartContent(FakeStartRequest(), Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RetryOnboarding_AsBusinessAdmin_Returns403()
    {
        var response = await SendAsRole(HttpMethod.Post, $"{BasePath}/{Guid.NewGuid()}/retry",
            Roles.BusinessAdmin, BuildRetryContent(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Positive: Start — OnboardingWrite ────────────────────────────────────

    [Fact]
    public async Task StartOnboarding_AsProjectManager_Returns202()
    {
        var response = await SendAsRole(HttpMethod.Post, BasePath, Roles.ProjectManager,
            BuildStartContent(FakeStartRequest(), Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task StartOnboarding_AsBusinessAdmin_Returns202()
    {
        var response = await SendAsRole(HttpMethod.Post, BasePath, Roles.BusinessAdmin,
            BuildStartContent(FakeStartRequest(), Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    // ── Positive: Detail — OnboardingRead ────────────────────────────────────

    [Fact]
    public async Task GetOnboardingDetail_AsSupportAgent_PassesAuthorization()
    {
        var response = await SendAsRole(HttpMethod.Get, $"{BasePath}/{Guid.NewGuid()}", Roles.SupportAgent);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOnboardingDetail_AsBusinessAdmin_PassesAuthorization()
    {
        var response = await SendAsRole(HttpMethod.Get, $"{BasePath}/{Guid.NewGuid()}", Roles.BusinessAdmin);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Positive: List — OnboardingRead ──────────────────────────────────────

    [Fact]
    public async Task GetOnboardingList_AsSupportAgent_Returns200()
    {
        var response = await SendAsRole(HttpMethod.Get, BasePath, Roles.SupportAgent);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOnboardingList_AsBusinessAdmin_Returns200()
    {
        var response = await SendAsRole(HttpMethod.Get, BasePath, Roles.BusinessAdmin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Positive: Result — OnboardingRead ────────────────────────────────────

    [Fact]
    public async Task GetOnboardingResult_AsSupportAgent_PassesAuthorization()
    {
        var response = await SendAsRole(HttpMethod.Get, $"{BasePath}/{Guid.NewGuid()}/result", Roles.SupportAgent);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOnboardingResult_AsBusinessAdmin_PassesAuthorization()
    {
        var response = await SendAsRole(HttpMethod.Get, $"{BasePath}/{Guid.NewGuid()}/result", Roles.BusinessAdmin);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Positive: Retry — OnboardingRetry ────────────────────────────────────

    [Fact]
    public async Task RetryOnboarding_AsSupportAgent_PassesAuthorization()
    {
        var response = await SendAsRole(HttpMethod.Post, $"{BasePath}/{Guid.NewGuid()}/retry",
            Roles.SupportAgent, BuildRetryContent(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RetryOnboarding_AsProjectManager_PassesAuthorization()
    {
        var response = await SendAsRole(HttpMethod.Post, $"{BasePath}/{Guid.NewGuid()}/retry",
            Roles.ProjectManager, BuildRetryContent(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> SendAsRole(HttpMethod method, string url, string role, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Test-Role", role);
        request.Content = content;
        return _httpClient.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static StringContent BuildStartContent(StartOnboardingProcessRequest request, Guid requestId)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(request, JsonOptions),
            Encoding.UTF8,
            "application/json");
        content.Headers.Add("x-requestid", requestId.ToString());
        return content;
    }

    private static StringContent BuildRetryContent(Guid requestId, string? reason = null)
    {
        var payload = new RetryOnboardingProcessRequest(reason);
        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");
        content.Headers.Add("x-requestid", requestId.ToString());
        return content;
    }

    private static StartOnboardingProcessRequest FakeStartRequest()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        return new StartOnboardingProcessRequest(
            Company: new CompanyInputDto(
                LegalName: $"Auth Test Corp {id} S.L.",
                TaxId: $"B{id}",
                TaxNumberType: TaxNumberType.Cif,
                Country: "Spain",
                CountryCode: "ES",
                TradeName: $"Auth Test {id}"),
            Subscription: new SubscriptionInputDto(
                SubscriptionPlanCode: "PROFESSIONAL"),
            AdminUser: new AdminUserInputDto(
                FirstName: "Auth",
                LastName: "Test",
                Email: $"auth-{id}@test.com"));
    }
}
