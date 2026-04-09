
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Qlt.Domain.Exceptions;

using System.Net;
using System.Text.Json;



namespace Qlt.Platform.Middleware;

/// <summary>
/// Global exception handling middleware that catches all unhandled exceptions and returns appropriate HTTP responses.
/// - ValidationException: 400 Bad Request with structured validation errors
/// - Other exceptions: 500 Internal Server Error with RFC 9110 format
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainValidationException ex)
        {
            _logger.LogWarning(ex, "Validation failed for {RequestMethod} {RequestPath}",
                context.Request.Method, context.Request.Path);
            await HandleValidationExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {RequestMethod} {RequestPath}: {ExceptionType}",
                context.Request.Method, context.Request.Path, ex.GetType().FullName);
            await HandleGenericExceptionAsync(context, ex);
        }
    }

    private static async Task HandleValidationExceptionAsync(HttpContext context, DomainValidationException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

        var response = new ApiErrorResponse
        {
            Code = "ValidationError",
            Message = "One or more validation errors occurred.",
            TraceId = context.TraceIdentifier,
            Errors = exception.Errors as Dictionary<string, string[]>
                     ?? new Dictionary<string, string[]>(exception.Errors)
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }

    private async Task HandleGenericExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new ApiErrorResponse
        {
            Code = "UnexpectedError",
            Message = "An error occurred while processing your request.",
            TraceId = context.TraceIdentifier,
            Details = _environment.IsDevelopment()
                ? new { detail = exception.Message, stackTrace = exception.StackTrace }
                : null
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
