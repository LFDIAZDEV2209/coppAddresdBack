namespace CoppAddresd.Domain.Exceptions;

/// <summary>
/// Acceso prohibido por regla de dominio (p. ej. AC-22: un paciente intenta
/// auto-asignarse una línea base clínica). Se traduce a <c>403 Forbidden</c>
/// por el middleware global de excepciones.
/// </summary>
public sealed class ForbiddenException(string message) : Exception(message);