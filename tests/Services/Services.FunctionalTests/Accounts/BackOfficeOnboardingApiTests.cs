using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Asp.Versioning;
using Asp.Versioning.Http;

using MediatR;

using Microsoft.AspNetCore.Mvc.Testing;

using Sirius.Contracts.Identity.IntegrationEvents;
using Sirius.Platform.Messaging.IntegrationEvents;
using Sirius.Services.Accounts.APIs;
using Sirius.Services.Accounts.Application.Queries;
using Sirius.Services.Accounts.Domain.Models.CompanyAggregate;

namespace Services.FunctionalTests.Accounts;

public sealed class OnboardingApiTests : IClassFixture<AccountsFixture>
{
    private const string BasePath = "api/accounts/customer-onboarding";

    private readonly WebApplicationFactory<Program> _webApplicationFactory;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OnboardingApiTests(AccountsFixture fixture)
    {
        var handler = new ApiVersionHandler(new QueryStringApiVersionWriter(), new ApiVersion(1.0));
        _webApplicationFactory = fixture;
        _httpClient = _webApplicationFactory.CreateDefaultClient(handler);
    }

    [Fact]
    public async Task StartOnboarding_WithValidRequest_Returns202WithStartedResponse()
    {
        var content = BuildStartContent(FakeStartRequest(), Guid.NewGuid());

        var response = await _httpClient.PostAsync(BasePath, content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var started = JsonSerializer.Deserialize<OnboardingProcessStartedResponse>(body, JsonOptions);

        Assert.NotNull(started);
        Assert.NotEqual(Guid.Empty, started.OnboardingProcessId);
        Assert.False(string.IsNullOrEmpty(started.Status));
        Assert.False(string.IsNullOrEmpty(started.CurrentStep));
        Assert.NotNull(started.Links);
        Assert.Contains(started.OnboardingProcessId.ToString(), started.Links.Self);
    }

    [Fact]
    public async Task StartOnboarding_WithEmptyRequestId_ReturnsBadRequest()
    {
        var content = BuildStartContent(FakeStartRequest(), Guid.Empty);

        var response = await _httpClient.PostAsync(BasePath, content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StartOnboarding_WithSameRequestId_IsIdempotent()
    {
        var requestId = Guid.NewGuid();
        var request = FakeStartRequest();

        var content1 = BuildStartContent(request, requestId);
        var response1 = await _httpClient.PostAsync(BasePath, content1, TestContext.Current.CancellationToken);

        var content2 = BuildStartContent(request, requestId);
        var response2 = await _httpClient.PostAsync(BasePath, content2, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response1.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, response2.StatusCode);
    }

    [Fact]
    public async Task GetOnboardingDetail_AfterStart_Returns200WithCompleteStructure()
    {
        var onboardingId = await StartOnboardingAndGetId();

        var response = await _httpClient.GetAsync($"{BasePath}/{onboardingId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var detail = JsonSerializer.Deserialize<OnboardingProcessDetailResponse>(body, JsonOptions);

        Assert.NotNull(detail);
        Assert.Equal(onboardingId, detail.OnboardingProcessId);
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
    public async Task GetOnboardingDetail_AfterStart_ContainsSnapshotData()
    {
        var request = FakeStartRequest();
        var onboardingId = await StartOnboardingAndGetId(request);

        var response = await _httpClient.GetAsync($"{BasePath}/{onboardingId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var detail = JsonSerializer.Deserialize<OnboardingProcessDetailResponse>(body, JsonOptions);

        Assert.NotNull(detail);
        Assert.Equal(request.Company.LegalName, detail.Summary.CompanyLegalName);
        Assert.Equal(request.Subscription.SubscriptionPlanCode, detail.Summary.SubscriptionPlanCode);
        Assert.Equal(request.AdminUser.Email, detail.Summary.AdminEmail);
    }

    [Fact]
    public async Task GetOnboardingDetail_WithNonExistentId_Returns404()
    {
        var response = await _httpClient.GetAsync($"{BasePath}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOnboardingList_ReturnsPagedResults()
    {
        await StartOnboardingAndGetId();

        var response = await _httpClient.GetAsync($"{BasePath}?page=1&pageSize=10", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var list = JsonSerializer.Deserialize<OnboardingProcessListResponse>(body, JsonOptions);

        Assert.NotNull(list);
        Assert.True(list.TotalCount > 0);
        Assert.NotEmpty(list.Items);
        Assert.Equal(1, list.Page);
        Assert.Equal(10, list.PageSize);
    }

    [Fact]
    public async Task GetOnboardingList_WithPageSizeExceeding100_ReturnsValidationProblem()
    {
        var response = await _httpClient.GetAsync($"{BasePath}?page=1&pageSize=200", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOnboardingList_WithCompanyNameFilter_FiltersCorrectly()
    {
        var request = FakeStartRequest();
        await StartOnboardingAndGetId(request);

        var companyName = Uri.EscapeDataString(request.Company.LegalName);
        var response = await _httpClient.GetAsync($"{BasePath}?companyLegalName={companyName}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var list = JsonSerializer.Deserialize<OnboardingProcessListResponse>(body, JsonOptions);

        Assert.NotNull(list);
        Assert.True(list.TotalCount >= 1);
        Assert.All(list.Items, item =>
            Assert.Contains(request.Company.LegalName, item.CompanyLegalName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RetryOnboarding_OnNonFailedProcess_ReturnsConflict()
    {
        var onboardingId = await StartOnboardingAndGetId();

        var content = BuildRetryContent(Guid.NewGuid());
        var response = await _httpClient.PostAsync($"{BasePath}/{onboardingId}/retry", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RetryOnboarding_WithEmptyRequestId_ReturnsBadRequest()
    {
        var onboardingId = await StartOnboardingAndGetId();

        var content = BuildRetryContent(Guid.Empty);
        var response = await _httpClient.PostAsync($"{BasePath}/{onboardingId}/retry", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RetryOnboarding_WithNonExistentId_Returns404()
    {
        var content = BuildRetryContent(Guid.NewGuid());
        var response = await _httpClient.PostAsync($"{BasePath}/{Guid.NewGuid()}/retry", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOnboardingResult_OnInProgressProcess_ReturnsBadRequest()
    {
        var onboardingId = await StartOnboardingAndGetId();

        var response = await _httpClient.GetAsync($"{BasePath}/{onboardingId}/result", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOnboardingResult_WithNonExistentId_Returns404()
    {
        var response = await _httpClient.GetAsync($"{BasePath}/{Guid.NewGuid()}/result", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── E2E: Full Onboarding Flow ─────────────────────────────────────────────

    [Fact]
    public async Task OnboardingFlow_WhenExternalUserProvisioned_CompletesAndStartsActivation()
    {
        var request = FakeStartRequest();
        var onboardingId = await StartOnboardingAndGetId(request);

        var detailResponse = await _httpClient.GetAsync($"{BasePath}/{onboardingId}", TestContext.Current.CancellationToken);
        var detailBody = await detailResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var detail = JsonSerializer.Deserialize<OnboardingProcessDetailResponse>(detailBody, JsonOptions)!;

        Assert.Equal("WaitingForExternalProvisioning", detail.Status);
        Assert.Equal("AdminProvisioningRequested", detail.CurrentStep);

        var adminUserId = Guid.NewGuid();
        using (var scope = _webApplicationFactory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<ExternalUserProvisionedIntegrationEvent>>();

            await handler.Handle(new ExternalUserProvisionedIntegrationEvent(
                UserId: adminUserId,
                TenantId: detail.References.TenantId,
                CompanyId: detail.References.OwnerCompanyId!.Value,
                SubscriptionId: detail.References.SubscriptionId,
                OnboardingProcessId: onboardingId,
                Email: request.AdminUser.Email,
                FirstName: request.AdminUser.FirstName,
                LastName: request.AdminUser.LastName));
        }

        var completedResponse = await _httpClient.GetAsync($"{BasePath}/{onboardingId}", TestContext.Current.CancellationToken);
        var completedBody = await completedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var completedDetail = JsonSerializer.Deserialize<OnboardingProcessDetailResponse>(completedBody, JsonOptions)!;

        Assert.Equal("Completed", completedDetail.Status);
        Assert.Equal("Completed", completedDetail.CurrentStep);
        Assert.NotNull(completedDetail.CompletedAtUtc);
        Assert.Equal(adminUserId, completedDetail.References.AdminUserId);
        Assert.NotNull(completedDetail.References.AdminMembershipId);
        Assert.Equal(100, completedDetail.ProgressPercent);

        var resultResponse = await _httpClient.GetAsync($"{BasePath}/{onboardingId}/result", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);

        const string activationBasePath = "api/accounts/customer-activation";
        var activationListResponse = await _httpClient.GetAsync(
            $"{activationBasePath}?adminEmail={Uri.EscapeDataString(request.AdminUser.Email)}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, activationListResponse.StatusCode);

        var activationBody = await activationListResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var activationList = JsonSerializer.Deserialize<CustomerActivationProcessListResponse>(activationBody, JsonOptions)!;

        Assert.True(activationList.TotalCount >= 1);
        Assert.Contains(activationList.Items, i => i.AdminEmail == request.AdminUser.Email);
    }

    private async Task<Guid> StartOnboardingAndGetId(StartOnboardingProcessRequest? request = null)
    {
        request ??= FakeStartRequest();
        var content = BuildStartContent(request, Guid.NewGuid());

        var response = await _httpClient.PostAsync(BasePath, content, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var started = JsonSerializer.Deserialize<OnboardingProcessStartedResponse>(body, JsonOptions);

        return started!.OnboardingProcessId;
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
                LegalName: $"BackOffice Test Corp {id} S.L.",
                TaxId: $"B{id}",
                TaxNumberType: TaxNumberType.Cif,
                Country: "Spain",
                CountryCode: "ES",
                TradeName: $"BackOffice Test {id}"),
            Subscription: new SubscriptionInputDto(
                SubscriptionPlanCode: "PROFESSIONAL"),
            AdminUser: new AdminUserInputDto(
                FirstName: "Jane",
                LastName: "Smith",
                Email: $"admin-{id}@backofficetest.com"));
    }
}
