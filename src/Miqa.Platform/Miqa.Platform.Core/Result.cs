using System;
using System.Collections.Generic;

namespace Miqa.Platform;

public class Result
{
    protected internal Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException();
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException();
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    public static Result<TValue> Create<TValue>(TValue? value) => value is not null ? Success(value) : Failure<TValue>(Error.NullValue);

    public static Result ValidationFailure(IDictionary<string, string[]> errors) =>
        new(false, new ValidationError(errors));

    public static Result<TValue> ValidationFailure<TValue>(IDictionary<string, string[]> errors) =>
        new(default, false, new ValidationError(errors));

    public static Result ValidationFailure(string propertyName, string errorMessage) =>
        new(false, new ValidationError(propertyName, errorMessage));

    public static Result<TValue> ValidationFailure<TValue>(string propertyName, string errorMessage) =>
        new(default, false, new ValidationError(propertyName, errorMessage));

    /// <summary>
    /// Converts the result into a unified return type based on success or failure.
    /// </summary>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess() : onFailure(Error);

    /// <summary>
    /// Chains a Result-producing operation that executes only on success.
    /// </summary>
    public Result<TOut> Bind<TOut>(Func<Result<TOut>> binder) =>
        IsSuccess ? binder() : Failure<TOut>(Error);

    /// <summary>
    /// Chains a Result-producing operation that executes only on success.
    /// </summary>
    public Result Bind(Func<Result> binder) =>
        IsSuccess ? binder() : this;
}


public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error) =>
        _value = value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failure result can not be accessed.");

    public static implicit operator Result<TValue>(TValue? value) => Create(value);

    /// <summary>
    /// Transforms the value if success, propagates the error if failure.
    /// </summary>
    public Result<TOut> Map<TOut>(Func<TValue, TOut> mapper) =>
        IsSuccess ? Result.Success(mapper(Value)) : Result.Failure<TOut>(Error);

    /// <summary>
    /// Chains a Result-producing operation that executes only on success.
    /// </summary>
    public Result<TOut> Bind<TOut>(Func<TValue, Result<TOut>> binder) =>
        IsSuccess ? binder(Value) : Result.Failure<TOut>(Error);

    /// <summary>
    /// Converts the result into a unified return type based on success or failure.
    /// </summary>
    public TOut Match<TOut>(Func<TValue, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(Error);
}
