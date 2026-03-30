using FluentValidation;

using MediatR;

using Qlt.Platform.Activities;

using System.Diagnostics;

namespace Qlt.Domain.Behaviors;

/// <summary>
/// MediatR pipeline behavior that validates commands/queries using FluentValidation validators.
/// Throws ValidationException if validation fails.
/// </summary>
/// <typeparam name="TRequest">The request type (Command or Query)</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
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
            // No validators registered for this request type, skip validation
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

            throw new Exceptions.DomainValidationException(failures);
        }

        BehaviorHelper.RecordValidationSuccess(activity, commandName, validatorCount, elapsedMs);

        return await next(cancellationToken);
    }

}
