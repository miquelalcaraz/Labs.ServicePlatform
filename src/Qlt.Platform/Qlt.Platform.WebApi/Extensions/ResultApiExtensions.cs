using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Qlt.Platform.Extensions;

/// <summary>
/// Extension methods for converting Result types into typed HTTP responses with structured error contract support.
/// </summary>
public static class ResultApiExtensions
{
    /// <summary>
    /// Converts a Result&lt;T&gt; into an HTTP response:
    /// Success → 200 OK, any failure → 400 BadRequest with a structured <see cref="ApiErrorResponse"/> body.
    /// </summary>
    public static Results<Ok<T>, BadRequest<ApiErrorResponse>> ToHttpResult<T>(this Result<T> result, string? traceId = null)
    {
        if (result.IsSuccess)
            return TypedResults.Ok(result.Value);

        return TypedResults.BadRequest(ApiErrorResponse.From(result.Error, traceId));
    }
}
