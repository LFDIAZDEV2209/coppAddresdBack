using FluentValidation.Results;

namespace CoppAddresd.Telemedicine.Application.Exceptions;

/// <summary>
/// Errores de validación de FluentValidation agrupados (HTTP 400). La lista de
/// <c>{ property, error }</c> se expone en la respuesta ProblemDetails.
/// </summary>
public sealed class RequestValidationException : Exception
{
    public RequestValidationException(IReadOnlyList<ValidationFailure> failures)
        : base("Uno o más campos no pasaron la validación.")
    {
        Failures = failures;
    }

    public IReadOnlyList<ValidationFailure> Failures { get; }
}
