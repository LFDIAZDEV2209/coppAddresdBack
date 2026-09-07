namespace CoppAddresd.Domain.Exceptions;

/// <summary>
/// Gate de adherencia nutricional (SPEC nutrition-intake-adherence, D3): la
/// completación de la tarea <c>nut</c> exige evidencia (intake logs) de TODAS
/// las comidas del plan-day activo. Precedente de mensaje:
/// <c>MOOD_SCORE_REQUIRED</c>. Se traduce a <c>422 Unprocessable Entity</c>
/// por el middleware global, que escribe los códigos faltantes en
/// <c>errors.missingMealCodes</c> (RFC 7807) gracias al catch dedicado ANTES
/// del catch base de <see cref="UnprocessableEntityException"/>.
/// </summary>
public sealed class NutritionEvidenceRequiredException(IReadOnlyList<string> missingMealCodes)
    : UnprocessableEntityException(
        $"NUTRITION_EVIDENCE_REQUIRED: faltan comidas del plan: {string.Join(", ", missingMealCodes)}."
    )
{
    /// <summary>
    /// Códigos de comida (des/alm/mer/cen, D4) del plan-day sin evidencia de
    /// log en la fecha de la completación. Orden estable (alfabético).
    /// </summary>
    public IReadOnlyList<string> MissingMealCodes { get; } = missingMealCodes;
}