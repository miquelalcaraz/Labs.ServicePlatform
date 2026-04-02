namespace Qlt.Domain.Exceptions;

/// <summary>
/// Exception thrown when validation of a command/query fails.
/// Contains structured validation errors.
/// </summary>
public class DomainValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public DomainValidationException()
        : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public DomainValidationException(IEnumerable<FluentValidation.Results.ValidationFailure> failures)
        : this()
    {
        Errors = failures
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray());
    }
}
