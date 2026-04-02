using System.Collections.Generic;
using System.Linq;

namespace Qlt.Platform;

/// <summary>
/// Specialized error type for validation failures.
/// Carries structured per-property error messages compatible with RFC 9110 § 15.5.1 responses.
/// </summary>
public sealed record ValidationError : Error
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationError(IDictionary<string, string[]> errors)
        : base("Error.ValidationFailed", "One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationError(string propertyName, string errorMessage)
        : base("Error.ValidationFailed", errorMessage, propertyName)
    {
        Errors = new Dictionary<string, string[]>
        {
            [propertyName] = [errorMessage]
        };
    }

    public ValidationError(IEnumerable<(string PropertyName, string ErrorMessage)> failures)
        : base("Error.ValidationFailed", "One or more validation errors occurred.")
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }
}
