namespace CoppAddresd.Domain.Exceptions;

/// <summary>
/// Payload válido sintácticamente pero semánticamente inaceptable
/// (p. ej. referencia a un catálogo inexistente). Se traduce a
/// <c>422 Unprocessable Entity</c> por el middleware global de excepciones.
/// </summary>
/// <remarks>
/// NO sellada (D3, SPEC nutrition-intake-adherence): deriva
/// <see cref="NutritionEvidenceRequiredException"/> para que el middleware
/// pueda añadir <c>errors.missingMealCodes</c> al body RFC 7807 sin cambiar
/// el comportamiento del resto de mapeos.
/// </remarks>
public class UnprocessableEntityException(string message) : Exception(message);
