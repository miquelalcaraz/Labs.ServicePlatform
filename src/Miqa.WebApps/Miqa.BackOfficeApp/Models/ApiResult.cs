namespace Miqa.BackOfficeApp.Models
{
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
}
