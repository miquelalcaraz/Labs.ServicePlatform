using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Asp.Versioning;
using Asp.Versioning.Http;

using static Sirius.Services.Accounts.Authorization.AccountsAuthorizationPolicies;

namespace Services.FunctionalTests.Accounts;

/// <summary>
/// Validates the activation authorization matrix:
/// ActivationRead   → ProjectManager, BusinessAdmin, SupportAgent, PlatformAdmin (all 4)
/// ActivationRetry  → ProjectManager, SupportAgent, PlatformAdmin (NOT BusinessAdmin)
/// ActivationResend → ProjectManager, SupportAgent, PlatformAdmin (NOT BusinessAdmin)
/// </summary>
public sealed class ActivationAuthorizationTests : IClassFixture<AccountsFixture>
{
    private const string BasePath = "api/accounts/customer-activation";

    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ActivationAuthorizationTests(AccountsFixture fixture)
    {
        var handler = new ApiVersionHandler(new QueryStringApiVersionWriter(), new ApiVersion(1.0));
        _httpClient = fixture.CreateDefaultClient(handler);
    }

    // ── Negative: roles that must be DENIED ──────────────────────────────────

    [Fact]
    public async Task RetryActivation_AsBusinessAdmin_Returns403()
    {
        var response = await SendAsRole(
            HttpMethod.Post, $"{BasePath}/{Guid.NewGuid()}/retry",
            Roles.BusinessAdmin, BuildRetryContent(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ResendActivation_AsBusinessAdmin_Returns403()
    {
        var response = await SendAsRole(
            HttpMethod.Post, $"{BasePath}/{Guid.NewGuid()}/resend",
            Roles.BusinessAdmin, BuildResendContent(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Positive: List — ActivationRead ──────────────────────────────────────

    [Fact]
    public async Task GetActivationList_AsSupportAgent_Returns200()
    {
        var response = await SendAsRole(HttpMethod.Get, BasePath, Roles.SupportAgent);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetActivationList_AsBusinessAdmin_Returns200()
    {
        var response = await SendAsRole(HttpMethod.Get, BasePath, Roles.BusinessAdmin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Positive: Detail — ActivationRead ────────────────────────────────────

    [Fact]
    public async Task GetActivationDetail_AsSupportAgent_PassesAuthorization()
    {
        var response = await SendAsRole(HttpMethod.Get, $"{BasePath}/{Guid.NewGuid()}", Roles.SupportAgent);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetActivationDetail_AsBusinessAdmin_PassesAuthorization()
    {
        var response = await SendAsRole(HttpMethod.Get, $"{BasePath}/{Guid.NewGuid()}", Roles.BusinessAdmin);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Positive: Retry — ActivationRetry ────────────────────────────────────

    [Fact]
    public async Task RetryActivation_AsSupportAgent_PassesAuthorization()
    {
        var response = await SendAsRole(
            HttpMethod.Post, $"{BasePath}/{Guid.NewGuid()}/retry",
            Roles.SupportAgent, BuildRetryContent(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RetryActivation_AsProjectManager_PassesAuthorization()
    {
        var response = await SendAsRole(
            HttpMethod.Post, $"{BasePath}/{Guid.NewGuid()}/retry",
            Roles.ProjectManager, BuildRetryContent(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Positive: Resend — ActivationResend ──────────────────────────────────

    [Fact]
    public async Task ResendActivation_AsSupportAgent_PassesAuthorization()
    {
        var response = await SendAsRole(
            HttpMethod.Post, $"{BasePath}/{Guid.NewGuid()}/resend",
            Roles.SupportAgent, BuildResendContent(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResendActivation_AsProjectManager_PassesAuthorization()
    {
        var response = await SendAsRole(
            HttpMethod.Post, $"{BasePath}/{Guid.NewGuid()}/resend",
            Roles.ProjectManager, BuildResendContent(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> SendAsRole(HttpMethod method, string url, string role, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Test-Role", role);
        request.Content = content;
        return _httpClient.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static StringContent BuildRetryContent(Guid requestId, string? reason = null)
    {
        var payload = new { Reason = reason };
        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");
        content.Headers.Add("x-requestid", requestId.ToString());
        return content;
    }

    private static StringContent BuildResendContent(Guid requestId)
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        content.Headers.Add("x-requestid", requestId.ToString());
        return content;
    }
}
