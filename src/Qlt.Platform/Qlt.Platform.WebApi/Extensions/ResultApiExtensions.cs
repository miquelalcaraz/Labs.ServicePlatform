using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Qlt.Platform.Extensions;

/// <summary>
/// Extension methods for converting Result types into typed HTTP responses with structured validation error support.
/// </summary>
public static class ResultApiExtensions
{
    /// <summary>
    /// Converts a Result&lt;T&gt; into an HTTP response:
    /// Success → 200 OK, ValidationError → 400 ValidationProblem (structured), other errors → 400 BadRequest.
    /// </summary>
    public static Results<Ok<T>, ValidationProblem, BadRequest<string>> ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return TypedResults.Ok(result.Value);

        if (result.Error is ValidationError ve)
            return TypedResults.ValidationProblem(ve.Errors);

        return TypedResults.BadRequest(result.Error.Message);
    }
}
