using Qlt.WebApps.BackOffice.Models;

using System.Web;

namespace Qlt.WebApps.BackOffice.Services;

/// <summary>
/// Typed HTTP client service for communicating with the Onboarding API.
/// </summary>
public class OnboardingApiService(HttpClient httpClient, ILogger<OnboardingApiService> logger)
{
    private const string BasePath = "/api/accounts/customer-onboarding";

    public async Task<ApiResult<OnboardingProcessStartedResponse>> StartOnboardingAsync(
        StartOnboardingRequest request, CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BasePath);
        httpRequest.Headers.Add("x-requestid", requestId.ToString());
        httpRequest.Content = JsonContent.Create(request);

        try
        {
            var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<OnboardingProcessStartedResponse>(cancellationToken);
                return ApiResult<OnboardingProcessStartedResponse>.Success(result!);
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("StartOnboarding failed with {StatusCode}: {Body}", response.StatusCode, errorBody);
            return ApiResult<OnboardingProcessStartedResponse>.Failure((int)response.StatusCode, errorBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling StartOnboarding API");
            return ApiResult<OnboardingProcessStartedResponse>.Failure(0, ex.Message);
        }
    }

    public async Task<ApiResult<OnboardingProcessDetailResponse>> GetOnboardingDetailAsync(
        Guid onboardingProcessId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"{BasePath}/{onboardingProcessId}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<OnboardingProcessDetailResponse>(cancellationToken);
                return ApiResult<OnboardingProcessDetailResponse>.Success(result!);
            }

            return ApiResult<OnboardingProcessDetailResponse>.Failure((int)response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling GetOnboardingDetail API for {Id}", onboardingProcessId);
            return ApiResult<OnboardingProcessDetailResponse>.Failure(0, ex.Message);
        }
    }

    public async Task<ApiResult<OnboardingProcessListResponse>> GetOnboardingListAsync(
        string? status = null,
        string? companyLegalName = null,
        string? adminEmail = null,
        string? subscriptionPlanCode = null,
        int page = 1,
        int pageSize = 20,
        string sortBy = "startedAtUtc",
        string sortDirection = "desc",
        CancellationToken cancellationToken = default)
    {
        var queryParams = HttpUtility.ParseQueryString(string.Empty);
        if (!string.IsNullOrWhiteSpace(status)) queryParams["status"] = status;
        if (!string.IsNullOrWhiteSpace(companyLegalName)) queryParams["companyLegalName"] = companyLegalName;
        if (!string.IsNullOrWhiteSpace(adminEmail)) queryParams["adminEmail"] = adminEmail;
        if (!string.IsNullOrWhiteSpace(subscriptionPlanCode)) queryParams["subscriptionPlanCode"] = subscriptionPlanCode;
        queryParams["page"] = page.ToString();
        queryParams["pageSize"] = pageSize.ToString();
        queryParams["sortBy"] = sortBy;
        queryParams["sortDirection"] = sortDirection;

        var url = $"{BasePath}?{queryParams}";

        try
        {
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<OnboardingProcessListResponse>(cancellationToken);
                return ApiResult<OnboardingProcessListResponse>.Success(result!);
            }

            return ApiResult<OnboardingProcessListResponse>.Failure((int)response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling GetOnboardingList API");
            return ApiResult<OnboardingProcessListResponse>.Failure(0, ex.Message);
        }
    }

    public async Task<ApiResult<OnboardingProcessRetryResponse>> RetryOnboardingAsync(
        Guid onboardingProcessId, RetryOnboardingRequest? request = null, CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BasePath}/{onboardingProcessId}/retry");
        httpRequest.Headers.Add("x-requestid", requestId.ToString());
        httpRequest.Content = JsonContent.Create(request ?? new RetryOnboardingRequest());

        try
        {
            var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<OnboardingProcessRetryResponse>(cancellationToken);
                return ApiResult<OnboardingProcessRetryResponse>.Success(result!);
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("RetryOnboarding failed with {StatusCode}: {Body}", response.StatusCode, errorBody);
            return ApiResult<OnboardingProcessRetryResponse>.Failure((int)response.StatusCode, errorBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling RetryOnboarding API for {Id}", onboardingProcessId);
            return ApiResult<OnboardingProcessRetryResponse>.Failure(0, ex.Message);
        }
    }

    public async Task<ApiResult<OnboardingProcessResultResponse>> GetOnboardingResultAsync(
        Guid onboardingProcessId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"{BasePath}/{onboardingProcessId}/result", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<OnboardingProcessResultResponse>(cancellationToken);
                return ApiResult<OnboardingProcessResultResponse>.Success(result!);
            }

            return ApiResult<OnboardingProcessResultResponse>.Failure((int)response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling GetOnboardingResult API for {Id}", onboardingProcessId);
            return ApiResult<OnboardingProcessResultResponse>.Failure(0, ex.Message);
        }
    }
}

/// <summary>
/// Represents the outcome of an API call with success/failure semantics.
/// </summary>
public class ApiResult<T>
{
    public bool IsSuccess { get; private init; }
    public T? Value { get; private init; }
    public int StatusCode { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static ApiResult<T> Success(T value) => new()
    {
        IsSuccess = true,
        Value = value,
        StatusCode = 200
    };

    public static ApiResult<T> Failure(int statusCode, string? errorMessage = null) => new()
    {
        IsSuccess = false,
        StatusCode = statusCode,
        ErrorMessage = errorMessage
    };
}
