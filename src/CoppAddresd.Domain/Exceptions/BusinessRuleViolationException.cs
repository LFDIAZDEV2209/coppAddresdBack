namespace CoppAddresd.Domain.Exceptions;

/// <summary>
/// Violación de una regla de negocio con payload sintácticamente válido
/// (p. ej. duplicado de número de historia clínica). Se traduce a
/// <c>409 Conflict</c> por el middleware global de excepciones.
/// </summary>
public sealed class BusinessRuleViolationException(string message) : Exception(message);
