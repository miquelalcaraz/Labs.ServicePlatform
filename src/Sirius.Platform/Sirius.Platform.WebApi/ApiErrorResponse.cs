using System.Text.Json.Serialization;

namespace Sirius.Platform;

/// <summary>
/// Structured API error response aligned with the platform error contract: code, message, traceId, details, errors.
/// </summary>
public sealed record ApiErrorResponse
{
    public required string Code { get; init; }
    public required string Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Details { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string[]>? Errors { get; init; }

    public static ApiErrorResponse From(Error error, string? traceId = null)
    {
        if (error is ValidationError ve)
            return new ApiErrorResponse
            {
                Code = "ValidationError",
                Message = error.Message,
                TraceId = traceId,
                Errors = ve.Errors as Dictionary<string, string[]>
                         ?? new Dictionary<string, string[]>(ve.Errors)
            };

        var code = error.Code switch
        {
            "Error.NotFound" => "NotFound",
            "Error.Conflict" => "Conflict",
            "Error.Unauthorized" => "Unauthorized",
            "Error.ValidationFailed" => "ValidationError",
            _ => "Error"
        };

        return new ApiErrorResponse
        {
            Code = code,
            Message = error.Message,
            TraceId = traceId
        };
    }
}
