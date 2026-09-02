namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Códigos canónicos de las reglas de detección de debilidades (SPEC §21,
/// "Paso 7c"): identifican la regla que disparó cada fila de
/// <c>app.weaknesses</c> y son la llave del dedupe de detección (AC-43: no se
/// duplica una debilidad <c>open</c>/<c>acknowledged</c>/<c>in_intervention</c>
/// con el mismo código). Viven en Domain porque los comparten el motor de
/// reglas (Application) y la persistencia (Infrastructure).
/// </summary>
public static class WeaknessCodes
{
    /// <summary>Adherencia nutricional &lt; 70% (nutritional, medium).</summary>
    public const string NutLowAdherence = "WK_NUT_LOW_ADHERENCE";

    /// <summary>Adherencia nutricional &lt; 50% (nutritional, high).</summary>
    public const string NutCritical = "WK_NUT_CRITICAL";

    /// <summary>Glucosa en tendencia al alza y &gt; 125 mg/dL (clinical, high; REQUIRES_CLINICAL_VALIDATION).</summary>
    public const string ClinGlucoseHigh = "WK_CLIN_GLUCOSE_HIGH";

    /// <summary>Delta de % grasa corporal &gt; 0.3 puntos en la semana (clinical, medium).</summary>
    public const string ClinBodyFatUp = "WK_CLIN_BODYFAT_UP";

    /// <summary>Motivación &lt; 5/10 (psychological, medium).</summary>
    public const string PsyLowMotivation = "WK_PSY_LOW_MOTIVATION";

    /// <summary>Estrés &gt; 7/10 (psychological, medium; requiere fuente de estrés, latente).</summary>
    public const string PsyHighStress = "WK_PSY_HIGH_STRESS";

    /// <summary>Sueño promedio &lt; 6 h (sleep, low; requiere fuente de sueño, latente).</summary>
    public const string PsySleepPoor = "WK_PSY_SLEEP_POOR";

    /// <summary>Adherencia semanal &lt; 50% (adherence, medium).</summary>
    public const string AdhLowStreak = "WK_ADH_LOW_STREAK";

    /// <summary>Adherencia del nutracéutico a 7 días &lt; 70% (supplement, low).</summary>
    public const string AdhNbMissed = "WK_ADH_NB_MISSED";

    /// <summary>Cumplimiento de ejercicio &lt; 60% (exercise, low).</summary>
    public const string AdhExerciseLow = "WK_ADH_EXERCISE_LOW";
}