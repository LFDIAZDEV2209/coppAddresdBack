using FluentValidation;
using MediatR;
using CoppAddresd.Telemedicine.Application.Exceptions;

namespace CoppAddresd.Telemedicine.Application.Behaviors;

/// <summary>
/// Pipeline behavior de MediatR: valida los command/query con FluentValidation
/// antes de que lleguen al handler. Los errores se agrupan en una única
/// <see cref="RequestValidationException"/> que el middleware traduce a HTTP 400.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            var results = await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = results
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .ToList();

            if (failures.Count > 0)
            {
                throw new RequestValidationException(failures);
            }
        }

        return await next();
    }
}
