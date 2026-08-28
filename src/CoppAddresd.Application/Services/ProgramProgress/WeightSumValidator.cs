namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>Resultado de la validación de la suma de pesos (AC-19).</summary>
public sealed record WeightValidationResult(bool IsValid, decimal Sum)
{
    public static WeightValidationResult Valid(decimal sum) => new(true, sum);
    public static WeightValidationResult Invalid(decimal sum) => new(false, sum);
}

/// <summary>
/// Validador de la suma de pesos del Índice de Salud (SPEC §13.1.1 + AC-19):
/// antes de cualquier escritura sobre <c>app.health_score_weights</c> se exige
/// <c>SUM(weight) = 1.0000</c> para que el ponderado viva en 0..100. Defensa
/// contra una mala configuración clínica (riesgo documentado en PLAN §7).
/// No hay endpoint de escritura de pesos en este batch (T-35..T-41); el
/// validador se implementa y prueba igualmente (AC-19 defensivo).
/// </summary>
public static class WeightSumValidator
{
    /// <summary>Suma objetivo: 1.0000 (100%).</summary>
    public const decimal TargetSum = 1.0000m;

    /// <summary>
    /// Valida que la suma de pesos sea exactamente 1.0000. Una lista vacía o
    /// con pesos negativos también se rechaza (no puede ponderar nada).
    /// </summary>
    public static WeightValidationResult Validate(IReadOnlyList<ScoreWeight> weights)
    {
        if (weights.Count == 0 || weights.Any(w => w.Weight < 0m))
        {
            return WeightValidationResult.Invalid(weights.Sum(w => w.Weight));
        }

        var sum = weights.Sum(w => w.Weight);
        return sum == TargetSum
            ? WeightValidationResult.Valid(sum)
            : WeightValidationResult.Invalid(sum);
    }
}