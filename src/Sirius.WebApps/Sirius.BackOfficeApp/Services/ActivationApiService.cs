using System.Web;

using Sirius.BackOfficeApp.Models;

namespace Sirius.BackOfficeApp.Services;

/// <summary>
/// Typed HTTP client service for communicating with the Activation API.
/// </summary>
public class ActivationApiService(HttpClient httpClient, ILogger<ActivationApiService> logger)
{
    private const string BasePath = "/api/accounts/customer-activation";

    public async Task<ApiResult<ActivationProcessDetailResponse>> GetActivationDetailAsync(
        Guid activationProcessId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"{BasePath}/{activationProcessId}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ActivationProcessDetailResponse>(cancellationToken);
                return ApiResult<ActivationProcessDetailResponse>.Success(result!);
            }

            return ApiResult<ActivationProcessDetailResponse>.Failure((int)response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling GetActivationDetail API for {Id}", activationProcessId);
            return ApiResult<ActivationProcessDetailResponse>.Failure(0, ex.Message);
        }
    }

    public async Task<ApiResult<ActivationProcessListResponse>> GetActivationListAsync(
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
                var result = await response.Content.ReadFromJsonAsync<ActivationProcessListResponse>(cancellationToken);
                return ApiResult<ActivationProcessListResponse>.Success(result!);
            }

            return ApiResult<ActivationProcessListResponse>.Failure((int)response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling GetActivationList API");
            return ApiResult<ActivationProcessListResponse>.Failure(0, ex.Message);
        }
    }

    public async Task<ApiResult<ActivationProcessRetryResponse>> RetryActivationAsync(
        Guid activationProcessId, RetryActivationRequest? request = null, CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BasePath}/{activationProcessId}/retry");
        httpRequest.Headers.Add("x-requestid", requestId.ToString());
        httpRequest.Content = JsonContent.Create(request ?? new RetryActivationRequest());

        try
        {
            var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                // The API may answer 202 Accepted without body when the detail cannot be read back
                var result = response.Content.Headers.ContentLength is 0
                    ? null
                    : await response.Content.ReadFromJsonAsync<ActivationProcessRetryResponse>(cancellationToken);
                return ApiResult<ActivationProcessRetryResponse>.Success(result!);
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("RetryActivation failed with {StatusCode}: {Body}", response.StatusCode, errorBody);
            return ApiResult<ActivationProcessRetryResponse>.Failure((int)response.StatusCode, errorBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling RetryActivation API for {Id}", activationProcessId);
            return ApiResult<ActivationProcessRetryResponse>.Failure(0, ex.Message);
        }
    }

    public async Task<ApiResult<bool>> ResendActivationAsync(
        Guid activationProcessId, CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BasePath}/{activationProcessId}/resend");
        httpRequest.Headers.Add("x-requestid", requestId.ToString());

        try
        {
            var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            // The API answers 202 Accepted with no body
            if (response.IsSuccessStatusCode)
            {
                return ApiResult<bool>.Success(true);
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("ResendActivation failed with {StatusCode}: {Body}", response.StatusCode, errorBody);
            return ApiResult<bool>.Failure((int)response.StatusCode, errorBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling ResendActivation API for {Id}", activationProcessId);
            return ApiResult<bool>.Failure(0, ex.Message);
        }
    }
}
