using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Asp.Versioning;
using Asp.Versioning.Http;

using MediatR;

using Microsoft.AspNetCore.Mvc.Testing;

using Sirius.Services.Accounts.APIs;
using Sirius.Services.Accounts.Application.Commands;
using Sirius.Services.Accounts.Application.Queries;

namespace Services.FunctionalTests.Accounts;

public sealed class ActivationApiTests : IClassFixture<AccountsFixture>
{
    private const string BasePath = "api/accounts/customer-activation";

    private readonly WebApplicationFactory<Program> _webApplicationFactory;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ActivationApiTests(AccountsFixture fixture)
    {
        var handler = new ApiVersionHandler(new QueryStringApiVersionWriter(), new ApiVersion(1.0));
        _webApplicationFactory = fixture;
        _httpClient = _webApplicationFactory.CreateDefaultClient(handler);
    }

    // ── List ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetActivationList_ReturnsPagedResults()
    {
        await StartActivationAndGetId();

        var response = await _httpClient.GetAsync($"{BasePath}?page=1&pageSize=10", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var list = JsonSerializer.Deserialize<CustomerActivationProcessListResponse>(body, JsonOptions);

        Assert.NotNull(list);
        Assert.True(list.TotalCount > 0);
        Assert.NotEmpty(list.Items);
        Assert.Equal(1, list.Page);
        Assert.Equal(10, list.PageSize);
    }

    [Fact]
    public async Task GetActivationList_WithPageSizeExceeding100_ReturnsValidationProblem()
    {
        var response = await _httpClient.GetAsync($"{BasePath}?page=1&pageSize=200", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetActivationList_WithAdminEmailFilter_FiltersCorrectly()
    {
        var adminEmail = $"filter-{Guid.NewGuid():N}@test.com";
        await StartActivationAndGetId(adminEmail: adminEmail);

        var encodedEmail = Uri.EscapeDataString(adminEmail);
        var response = await _httpClient.GetAsync($"{BasePath}?adminEmail={encodedEmail}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var list = JsonSerializer.Deserialize<CustomerActivationProcessListResponse>(body, JsonOptions);

        Assert.NotNull(list);
        Assert.True(list.TotalCount >= 1);
        Assert.All(list.Items, item =>
            Assert.Contains(adminEmail, item.AdminEmail, StringComparison.OrdinalIgnoreCase));
    }

    // ── Detail ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetActivationDetail_AfterStart_Returns200WithCompleteStructure()
    {
        var activationId = await StartActivationAndGetId();

        var response = await _httpClient.GetAsync($"{BasePath}/{activationId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var detail = JsonSerializer.Deserialize<CustomerActivationProcessDetailResponse>(body, JsonOptions);

        Assert.NotNull(detail);
        Assert.Equal(activationId, detail.ActivationProcessId);
        Assert.False(string.IsNullOrEmpty(detail.Status));
        Assert.False(string.IsNullOrEmpty(detail.StatusDisplayName));
        Assert.False(string.IsNullOrEmpty(detail.CurrentStep));
        Assert.False(string.IsNullOrEmpty(detail.CurrentStepDisplayName));
        Assert.NotNull(detail.Summary);
        Assert.NotNull(detail.References);
        Assert.NotEqual(Guid.Empty, detail.References.SubscriptionId);
        Assert.NotEqual(Guid.Empty, detail.References.TenantId);
        Assert.NotNull(detail.Steps);
        Assert.NotEmpty(detail.Steps);
        Assert.NotNull(detail.Actions);
        Assert.True(detail.ProgressPercent >= 0 && detail.ProgressPercent <= 100);
    }

    [Fact]
    public async Task GetActivationDetail_AfterStart_ContainsSnapshotData()
    {
        var adminEmail = $"snapshot-{Guid.NewGuid():N}@test.com";
        var companyLegalName = $"Snapshot Corp {Guid.NewGuid().ToString("N")[..8]} S.L.";
        var activationId = await StartActivationAndGetId(adminEmail: adminEmail, companyLegalName: companyLegalName);

        var response = await _httpClient.GetAsync($"{BasePath}/{activationId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var detail = JsonSerializer.Deserialize<CustomerActivationProcessDetailResponse>(body, JsonOptions);

        Assert.NotNull(detail);
        Assert.Equal(adminEmail, detail.Summary.AdminEmail);
        Assert.Equal(companyLegalName, detail.Summary.CompanyLegalName);
        Assert.Equal("PROFESSIONAL", detail.Summary.SubscriptionPlanCode);
    }

    [Fact]
    public async Task GetActivationDetail_WithNonExistentId_Returns404()
    {
        var response = await _httpClient.GetAsync($"{BasePath}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Retry ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RetryActivation_OnNonFailedProcess_ReturnsConflict()
    {
        var activationId = await StartActivationAndGetId();

        var content = BuildRetryContent(Guid.NewGuid());
        var response = await _httpClient.PostAsync($"{BasePath}/{activationId}/retry", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RetryActivation_WithEmptyRequestId_ReturnsBadRequest()
    {
        var activationId = await StartActivationAndGetId();

        var content = BuildRetryContent(Guid.Empty);
        var response = await _httpClient.PostAsync($"{BasePath}/{activationId}/retry", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RetryActivation_WithNonExistentId_Returns404()
    {
        var content = BuildRetryContent(Guid.NewGuid());
        var response = await _httpClient.PostAsync($"{BasePath}/{Guid.NewGuid()}/retry", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RetryActivation_WithSameRequestId_BothReturnSameStatus()
    {
        var activationId = await StartActivationAndGetId();
        var requestId = Guid.NewGuid();

        var response1 = await _httpClient.PostAsync($"{BasePath}/{activationId}/retry", BuildRetryContent(requestId), TestContext.Current.CancellationToken);
        var response2 = await _httpClient.PostAsync($"{BasePath}/{activationId}/retry", BuildRetryContent(requestId), TestContext.Current.CancellationToken);

        Assert.Equal(response1.StatusCode, response2.StatusCode);
    }

    // ── Resend ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResendActivation_WithEmptyRequestId_ReturnsBadRequest()
    {
        var activationId = await StartActivationAndGetId();

        var response = await _httpClient.PostAsync(
            $"{BasePath}/{activationId}/resend",
            BuildResendContent(Guid.Empty),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResendActivation_WithNonExistentId_Returns404()
    {
        var response = await _httpClient.PostAsync(
            $"{BasePath}/{Guid.NewGuid()}/resend",
            BuildResendContent(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResendActivation_WithSameRequestId_BothReturnSameStatus()
    {
        var activationId = await StartActivationAndGetId();
        var requestId = Guid.NewGuid();

        var response1 = await _httpClient.PostAsync($"{BasePath}/{activationId}/resend", BuildResendContent(requestId), TestContext.Current.CancellationToken);
        var response2 = await _httpClient.PostAsync($"{BasePath}/{activationId}/resend", BuildResendContent(requestId), TestContext.Current.CancellationToken);

        Assert.Equal(response1.StatusCode, response2.StatusCode);
    }

    // ── E2E ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ActivationProcess_WhenUserActivatesAccount_ProcessCompletes()
    {
        var adminUserId = Guid.NewGuid();
        var adminEmail = $"e2e-{Guid.NewGuid():N}@test.com";
        var activationId = await StartActivationAndGetId(adminEmail: adminEmail, adminUserId: adminUserId);

        using (var deliveryScope = _webApplicationFactory.Services.CreateScope())
        {
            var mediator = deliveryScope.ServiceProvider.GetRequiredService<IMediator>();

            await mediator.Send(new ConfirmActivationDeliveryCommand(activationId));
        }

        using (var activationScope = _webApplicationFactory.Services.CreateScope())
        {
            var mediator = activationScope.ServiceProvider.GetRequiredService<IMediator>();

            await mediator.Send(new CompleteActivationOnUserActivatedCommand(adminUserId));
        }

        var response = await _httpClient.GetAsync($"{BasePath}/{activationId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var detail = JsonSerializer.Deserialize<CustomerActivationProcessDetailResponse>(body, JsonOptions);

        Assert.NotNull(detail);
        Assert.Equal("Completed", detail.Status);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> StartActivationAndGetId(string? adminEmail = null, string? companyLegalName = null, Guid? adminUserId = null)
    {
        adminEmail ??= $"activation-{Guid.NewGuid():N}@test.com";
        companyLegalName ??= $"Activation Test Corp {Guid.NewGuid().ToString("N")[..8]} S.L.";
        var resolvedAdminUserId = adminUserId ?? Guid.NewGuid();

        using var scope = _webApplicationFactory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(
            new StartCustomerActivationProcessCommand(
                subscriptionId: Guid.NewGuid(),
                tenantId: Guid.NewGuid(),
                ownerCompanyId: Guid.NewGuid(),
                adminUserId: resolvedAdminUserId,
                adminMembershipId: Guid.NewGuid(),
                companyLegalName: companyLegalName,
                subscriptionPlanCode: "PROFESSIONAL",
                adminEmail: adminEmail,
                requestId: Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        var listResult = await mediator.Send(
            new GetCustomerActivationProcessListQuery(
                Status: null,
                CompanyLegalName: null,
                AdminEmail: adminEmail,
                Page: 1,
                PageSize: 10),
            TestContext.Current.CancellationToken);

        return listResult.Value.Items[0].ActivationProcessId;
    }

    private static StringContent BuildRetryContent(Guid requestId, string? reason = null)
    {
        var payload = new RetryActivationProcessRequest(reason);
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
