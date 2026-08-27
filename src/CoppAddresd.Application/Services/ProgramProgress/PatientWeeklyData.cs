namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Paquete de datos semanales del paciente (SPEC §21, "Paso 7c") que el
/// <see cref="WeaknessRulesEngine"/> evalúa: el repositorio lo reúne sobre el
/// período actual (misma ventana que el Índice de Salud, SPEC §13.2) con
/// queries set-based, y el motor — función pura, determinista (ADRED-inspired)
/// — decide qué reglas de debilidad disparar.
///
/// Fuentes (todo desde tablas existentes del módulo, sin duplicar lógica):
/// - <c>NutritionAdherencePct</c>: <c>app.habit_checks</c> categoría
///   <c>alimentacion</c> (MISMA fuente que la dimensión <c>nutrition</c> del
///   Índice de Salud y que los premios semanales de SPEC §18 — reuso de
///   <c>GetNutritionLogAsync</c>).
/// - <c>GlucoseCurrent</c>/<c>GlucoseTrend</c>/<c>BodyFatDelta</c> (+ sus
///   <c>GlucoseMetricId</c>/<c>BodyFatMetricId</c>): <c>app.clinical_baselines</c>
///   + <c>app.clinical_measurements</c> del período (misma data que los
///   calculadores de puntaje clínico).
/// - <c>MotivationScore</c>: registro emocional más reciente del período.
///   El módulo solo persiste <c>mood_score</c> 1..5, por lo que la motivación
///   en escala 1..10 se deriva como <c>mood × 2</c> (proxy documentado, ADRED
///   adaptada; ver SPEC §21, B.5).
/// - <c>StressScore</c>/<c>AvgSleepHours</c>: null — no existen columnas
///   físicas de estrés/sueño todavía; las reglas que los usan son latentes y
///   solo disparan cuando una fuente futura las alimente (SPEC §21, B.6/B.7).
/// - <c>WeeklyAdherencePct</c>: dimensión <c>adherence</c> de la fila
///   <c>app.health_scores</c> del período (persistida por
///   <c>POST /scores/calculate</c> justo antes de la detección).
/// - <c>NbAdherence7dPct</c>: días con <c>task_completions</c> de
///   <c>nutribiotico</c> en los últimos 7 días / 7.
/// - <c>ExerciseCompletionPct</c>: días con <c>task_completions</c> de
///   <c>ejercicio</c> / días del período.
///
/// Convención "sin datos → null → la regla NO dispara" (nunca penaliza por
/// ausencia de datos, mismo principio que SPEC §13.4 y §18).
/// </summary>
public sealed record PatientWeeklyData(
    Guid PatientId,
    decimal? NutritionAdherencePct,
    decimal? GlucoseCurrent,
    string? GlucoseTrend,
    Guid? GlucoseMetricId,
    decimal? BodyFatDelta,
    Guid? BodyFatMetricId,
    decimal? MotivationScore,
    decimal? StressScore,
    decimal? AvgSleepHours,
    decimal? WeeklyAdherencePct,
    decimal? NbAdherence7dPct,
    decimal? ExerciseCompletionPct);