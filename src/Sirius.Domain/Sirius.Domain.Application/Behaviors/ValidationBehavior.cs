using System.Diagnostics;

using FluentValidation;

using MediatR;

using Sirius.Platform;
using Sirius.Platform.Activities;

namespace Sirius.Domain.Behaviors;

/// <summary>
/// MediatR pipeline behavior that validates commands/queries using FluentValidation validators.
/// Returns Result.ValidationFailure for Result-based responses; throws DomainValidationException for non-Result types.
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly Func<ValidationError, TResponse>? _failureFactory = CreateFailureFactory();

    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next(cancellationToken);
        }

        var commandName = typeof(TRequest).Name;
        var validatorCount = _validators.Count();

        using var activity = BehaviorHelper.StartValidationActivity(commandName, validatorCount);

        var stopwatch = Stopwatch.StartNew();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        stopwatch.Stop();

        var failures = validationResults
            .Where(r => r.Errors.Any())
            .SelectMany(r => r.Errors)
            .ToList();

        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        if (failures.Any())
        {
            var errorCount = failures.Count;
            var errorMessages = string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));

            BehaviorHelper.RecordValidationFailure(
                activity,
                commandName,
                validatorCount,
                errorCount,
                elapsedMs,
                errorMessages);

            var errors = failures
                .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
                .ToDictionary(g => g.Key, g => g.ToArray());

            if (_failureFactory is not null)
                return _failureFactory(new ValidationError(errors));

            throw new Exceptions.DomainValidationException(failures);
        }

        BehaviorHelper.RecordValidationSuccess(activity, commandName, validatorCount, elapsedMs);

        return await next(cancellationToken);
    }

    private static Func<ValidationError, TResponse>? CreateFailureFactory()
    {
        var responseType = typeof(TResponse);

        if (responseType == typeof(Result))
            return error => (TResponse)(object)Result.Failure(error);

        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var innerType = responseType.GetGenericArguments()[0];
            var method = typeof(Result)
                .GetMethod(nameof(Result.Failure), 1, [typeof(Error)])!
                .MakeGenericMethod(innerType);

            return error => (TResponse)method.Invoke(null, [error])!;
        }

        return null;
    }
}
