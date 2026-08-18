namespace CoppAddresd.Domain.Exceptions;

/// <summary>
/// Payload válido sintácticamente pero semánticamente inaceptable
/// (p. ej. referencia a un catálogo inexistente). Se traduce a
/// <c>422 Unprocessable Entity</c> por el middleware global de excepciones.
/// </summary>
public sealed class UnprocessableEntityException(string message) : Exception(message);
