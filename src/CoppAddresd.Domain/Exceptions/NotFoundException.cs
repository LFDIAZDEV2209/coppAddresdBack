namespace CoppAddresd.Domain.Exceptions;

/// <summary>
/// Recurso solicitado no existe. Se traduce a <c>404 Not Found</c> por el
/// middleware global de excepciones.
/// </summary>
public sealed class NotFoundException(string message) : Exception(message);
