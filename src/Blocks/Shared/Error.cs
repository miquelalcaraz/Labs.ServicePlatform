namespace QltS.Shared;

public record Error(string Code, string Message, string PropertyName = null!)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "The specified result value is null.");
    public static readonly Error ConditionNotMet = new("Error.ConditionNotMet", "The specified condition was not met.");
    public static readonly Error NotFound = new("Error.NotFound", "Resource not found.");
    public static readonly Error Conflict = new("Error.Conflict", "Request could not be completed due to a conflict with the current state of the resource.");
    public static readonly Error BadRequest = new("Error.BadRequest", "The request could not be understood by the server due to invalid syntax or missing parameters.");
    public static readonly Error Unauthorized = new("Error.Unauthorized", "The request requires user authentication.");
}

public static class ErrorExtensions
{
    public static Error WithMessage(this Error error, string message)
    {
        return error with { Message = message };
    }
    public static Error WithPropertyName(this Error error, string propertyName)
    {
        return error with { PropertyName = propertyName };
    }
}