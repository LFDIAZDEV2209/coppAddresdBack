namespace CoppAddresd.Telemedicine.Domain.Exceptions;

/// <summary>
/// Recurso del dominio de telemedicina no encontrado. Traducido a HTTP 404 por
/// el middleware global.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string resourceName, Guid id)
        : base($"{resourceName} con id '{id}' no fue encontrado.")
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Violación de una regla de negocio del dominio de telemedicina (agendamiento,
/// ventana de sala, estados, concurrencia). Traducida a HTTP 409 por el
/// middleware global.
/// </summary>
public sealed class BusinessRuleViolationException : Exception
{
    public BusinessRuleViolationException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Entrada inválida detectada en el dominio (no sustituye a FluentValidation,
/// que se aplica antes en la capa de aplicación). Traducida a HTTP 400.
/// </summary>
public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message)
        : base(message)
    {
    }
}
