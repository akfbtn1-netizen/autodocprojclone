
using MediatR;
using FluentValidation;

namespace Enterprise.Documentation.Core.Application.Behaviors;

/// <summary>
/// Pipeline behavior that validates all commands and queries using FluentValidation.
/// Ensures all requests are validated before reaching handlers.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Any())
        {
            throw new ApplicationValidationException(failures);
        }

        return await next();
    }
}

/// <summary>
/// Custom validation exception that aggregates FluentValidation errors.
/// </summary>
public class ApplicationValidationException : Exception
{
    public IReadOnlyList<ValidationError> Errors { get; }

    public ApplicationValidationException(IList<FluentValidation.Results.ValidationFailure> failures)
        : base("One or more validation failures have occurred.")
    {
        Errors = failures
            .Select(f => new ValidationError(f.PropertyName, f.ErrorMessage, f.ErrorCode))
            .ToList();
    }
}

/// <summary>
/// Represents a validation error with property name, message, and error code.
/// </summary>
public record ValidationError(string PropertyName, string ErrorMessage, string? ErrorCode);