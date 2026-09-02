using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Motor determinista de detección de debilidades (SPEC §21, B — "Paso 7c",
/// ADRED-inspired): función pura que evalúa un <see cref="PatientWeeklyData"/>
/// contra un catálogo fijo de reglas y devuelve los descriptores de debilidad
/// detectados. Sin I/O, sin aleatoriedad, sin estado: mismo patrón de los
/// calculadores de puntaje (SPEC §13, T-37) — testable con datos planos.
///
/// Semántica de datos ausentes: un indicador <c>null</c> NO dispara su regla
/// (principio "sin datos → sin hallazgo", nunca penalizar por ausencia —
/// SPEC §13.4/§18). Las reglas clínicas marcadas
/// <c>REQUIRES_CLINICAL_VALIDATION</c> tienen umbrales propuestos por el equipo
/// clínico de ADRED adaptado y deben confirmarse con el comité clínico antes de
/// operar como referencia.
///
/// Reglas (código, categoría, severidad, condición, acción sugerida):
/// 1. WK_NUT_LOW_ADHERENCE (nutritional, medium): adherencia nutricional
///    &lt; 70% → <c>create_intervention</c>.
/// 2. WK_NUT_CRITICAL (nutritional, high): adherencia &lt; 50% →
///    <c>telehealth_referral</c> (nutricionista).
/// 3. WK_CLIN_GLUCOSE_HIGH (clinical, high, VALIDATION): glucosa en tendencia
///    al alza (vs línea base) y actual &gt; 125 mg/dL → <c>referral_doctor</c>.
/// 4. WK_CLIN_BODYFAT_UP (clinical, medium): delta de % grasa &gt; 0.3 puntos
///    en la semana → <c>referral_nutritionist</c>.
/// 5. WK_PSY_LOW_MOTIVATION (psychological, medium): motivación &lt; 5/10 →
///    <c>referral_psychologist</c>.
/// 6. WK_PSY_HIGH_STRESS (psychological, medium): estrés &gt; 7/10 →
///    <c>referral_psychologist</c> (latente: requiere fuente de estrés).
/// 7. WK_PSY_SLEEP_POOR (sleep, low): sueño promedio &lt; 6 h →
///    <c>ai_recommendation</c> (latente: requiere fuente de sueño).
/// 8. WK_ADH_LOW_STREAK (adherence, medium): adherencia semanal &lt; 50% →
///    <c>recovery_mode</c>.
/// 9. WK_ADH_NB_MISSED (supplement, low): adherencia del nutracéutico a 7 días
///    &lt; 70% → <c>ai_recommendation</c>.
/// 10. WK_ADH_EXERCISE_LOW (exercise, low): cumplimiento de ejercicio &lt; 60%
///     → <c>reto_adjustment</c>.
/// </summary>
public static class WeaknessRulesEngine
{
    // --- Umbrales (documentados como validados por el equipo clínico; los
    // clínicos llevan la marca REQUIRES_CLINICAL_VALIDATION, SPEC §21, B) ---

    /// <summary>Adherencia nutricional mínima aceptable (WK_NUT_LOW_ADHERENCE).</summary>
    public const decimal NutritionAdherenceWarningPct = 70m;

    /// <summary>Adherencia nutricional crítica (WK_NUT_CRITICAL).</summary>
    public const decimal NutritionAdherenceCriticalPct = 50m;

    /// <summary>Umbral de glucosa en ayunas / aleatoria (mg/dL) para WK_CLIN_GLUCOSE_HIGH. REQUIRES_CLINICAL_VALIDATION.</summary>
    public const decimal GlucoseHighThresholdMgDl = 125m;

    /// <summary>Subida máxima aceptable de % grasa corporal en la semana (puntos) para WK_CLIN_BODYFAT_UP.</summary>
    public const decimal BodyFatMaxWeeklyDelta = 0.3m;

    /// <summary>Motivación mínima (escala 1..10) para WK_PSY_LOW_MOTIVATION.</summary>
    public const decimal MotivationMinScore = 5m;

    /// <summary>Estrés máximo aceptable (escala 1..10) para WK_PSY_HIGH_STRESS.</summary>
    public const decimal StressMaxScore = 7m;

    /// <summary>Sueño mínimo promedio (horas) para WK_PSY_SLEEP_POOR.</summary>
    public const decimal SleepMinHours = 6m;

    /// <summary>Adherencia semanal mínima del programa para WK_ADH_LOW_STREAK.</summary>
    public const decimal WeeklyAdherenceMinPct = 50m;

    /// <summary>Adherencia mínima del nutracéutico a 7 días para WK_ADH_NB_MISSED.</summary>
    public const decimal NbAdherenceMin7dPct = 70m;

    /// <summary>Cumplimiento mínimo de ejercicio para WK_ADH_EXERCISE_LOW.</summary>
    public const decimal ExerciseCompletionMinPct = 60m;

    /// <summary>Evaluar las reglas contra el paquete semanal del paciente.</summary>
    public static IReadOnlyList<WeaknessDescriptor> Evaluate(PatientWeeklyData data)
    {
        var detected = new List<WeaknessDescriptor>(4);

        // 1-2. Adherencia nutricional (nutritional): misma fuente que la
        // dimensión nutrition del Health Score (SPEC §18/§13.4.3).
        if (data.NutritionAdherencePct is { } nutritionAdherence)
        {
            if (nutritionAdherence < NutritionAdherenceCriticalPct)
            {
                detected.Add(NewDescriptor(
                    WeaknessCodes.NutCritical, WeaknessCategory.nutritional, WeaknessSeverity.high,
                    "Adherencia nutricional crítica",
                    $"Adherencia nutricional del período {nutritionAdherence:0}% (< {NutritionAdherenceCriticalPct:0}%). " +
                    "Acción sugerida: referir a telemedicina con nutricionista.",
                    metricId: null, nutritionAdherence, "telehealth_referral"));
            }
            else if (nutritionAdherence < NutritionAdherenceWarningPct)
            {
                detected.Add(NewDescriptor(
                    WeaknessCodes.NutLowAdherence, WeaknessCategory.nutritional, WeaknessSeverity.medium,
                    "Adherencia nutricional baja",
                    $"Adherencia nutricional del período {nutritionAdherence:0}% (< {NutritionAdherenceWarningPct:0}%). " +
                    "Acción sugerida: crear intervención de seguimiento nutricional.",
                    metricId: null, nutritionAdherence, "create_intervention"));
            }
        }

        // 3. Glucosa elevada (clinical): tendencia al alza vs línea base y
        // valor actual por encima del umbral. REQUIRES_CLINICAL_VALIDATION.
        if (data.GlucoseTrend == "up" && data.GlucoseCurrent is { } glucose)
        {
            if (glucose > GlucoseHighThresholdMgDl)
            {
                detected.Add(NewDescriptor(
                    WeaknessCodes.ClinGlucoseHigh, WeaknessCategory.clinical, WeaknessSeverity.high,
                    "Glucosa elevada",
                    $"Glucosa actual {glucose:0.#} mg/dL en tendencia al alza (> {GlucoseHighThresholdMgDl:0} mg/dL). " +
                    "Acción sugerida: referir al médico. Umbral REQUIRES_CLINICAL_VALIDATION.",
                    metricId: data.GlucoseMetricId, glucose, "referral_doctor"));
            }
        }

        // 4. Subida de % grasa corporal en la semana (clinical).
        if (data.BodyFatDelta is { } bodyFatDelta && bodyFatDelta > BodyFatMaxWeeklyDelta)
        {
            detected.Add(NewDescriptor(
                WeaknessCodes.ClinBodyFatUp, WeaknessCategory.clinical, WeaknessSeverity.medium,
                "Subida de porcentaje de grasa corporal",
                $"Delta semanal de % grasa corporal {bodyFatDelta:+0.0;-0.0} puntos (> {BodyFatMaxWeeklyDelta:0.#}). " +
                "Acción sugerida: referir a nutricionista.",
                metricId: data.BodyFatMetricId, bodyFatDelta, "referral_nutritionist"));
        }

        // 5. Motivación baja (psychological): proxy mood × 2 sobre el último
        // registro emocional (el módulo solo persiste mood 1..5).
        if (data.MotivationScore is { } motivation && motivation < MotivationMinScore)
        {
            detected.Add(NewDescriptor(
                WeaknessCodes.PsyLowMotivation, WeaknessCategory.psychological, WeaknessSeverity.medium,
                "Motivación baja",
                $"Motivación {motivation:0.#}/10 (< {MotivationMinScore:0}). " +
                "Acción sugerida: referir a psicólogo.",
                metricId: null, motivation, "referral_psychologist"));
        }

        // 6. Estrés alto (psychological): LATENTE — no existe columna física de
        // estrés; la regla dispara cuando una fuente futura alimente el dato.
        if (data.StressScore is { } stress && stress > StressMaxScore)
        {
            detected.Add(NewDescriptor(
                WeaknessCodes.PsyHighStress, WeaknessCategory.psychological, WeaknessSeverity.medium,
                "Estrés alto",
                $"Estrés {stress:0.#}/10 (> {StressMaxScore:0}). " +
                "Acción sugerida: referir a psicólogo.",
                metricId: null, stress, "referral_psychologist"));
        }

        // 7. Sueño pobre (sleep): LATENTE — no existe fuente de sueño; la regla
        // dispara cuando una fuente futura alimente el dato.
        if (data.AvgSleepHours is { } sleepHours && sleepHours < SleepMinHours)
        {
            detected.Add(NewDescriptor(
                WeaknessCodes.PsySleepPoor, WeaknessCategory.sleep, WeaknessSeverity.low,
                "Sueño insuficiente",
                $"Sueño promedio {sleepHours:0.#} h (< {SleepMinHours:0.#} h). " +
                "Acción sugerida: recomendación de higiene del sueño.",
                metricId: null, sleepHours, "ai_recommendation"));
        }

        // 8. Adherencia semanal baja (adherence): dimensión adherence del
        // health_scores del período (SPEC §13.4.1).
        if (data.WeeklyAdherencePct is { } weeklyAdherence && weeklyAdherence < WeeklyAdherenceMinPct)
        {
            detected.Add(NewDescriptor(
                WeaknessCodes.AdhLowStreak, WeaknessCategory.adherence, WeaknessSeverity.medium,
                "Adherencia semanal baja",
                $"Adherencia semanal {weeklyAdherence:0}% (< {WeeklyAdherenceMinPct:0}%). " +
                "Acción sugerida: activar modo de recuperación.",
                metricId: null, weeklyAdherence, "recovery_mode"));
        }

        // 9. Nutracéutico faltante (supplement): constancia de la toma a 7 días.
        if (data.NbAdherence7dPct is { } nbAdherence && nbAdherence < NbAdherenceMin7dPct)
        {
            detected.Add(NewDescriptor(
                WeaknessCodes.AdhNbMissed, WeaknessCategory.supplement, WeaknessSeverity.low,
                "Constancia baja del nutracéutico",
                $"Adherencia del nutracéutico a 7 días {nbAdherence:0}% (< {NbAdherenceMin7dPct:0}%). " +
                "Acción sugerida: recomendación de constancia de la toma.",
                metricId: null, nbAdherence, "ai_recommendation"));
        }

        // 10. Cumplimiento de ejercicio bajo (exercise).
        if (data.ExerciseCompletionPct is { } exercisePct && exercisePct < ExerciseCompletionMinPct)
        {
            detected.Add(NewDescriptor(
                WeaknessCodes.AdhExerciseLow, WeaknessCategory.exercise, WeaknessSeverity.low,
                "Cumplimiento de ejercicio bajo",
                $"Cumplimiento de ejercicio {exercisePct:0}% (< {ExerciseCompletionMinPct:0}%). " +
                "Acción sugerida: ajustar el reto de actividad física.",
                metricId: null, exercisePct, "reto_adjustment"));
        }

        return detected;
    }

    private static WeaknessDescriptor NewDescriptor(
        string code, WeaknessCategory category, WeaknessSeverity severity,
        string title, string description, Guid? metricId, decimal? indicatorValue, string action)
        => new(code, category, severity, title, description, metricId, indicatorValue, action);
}