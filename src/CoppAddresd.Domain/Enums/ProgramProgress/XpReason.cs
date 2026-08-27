namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Motivo de un movimiento de XP en el libro mayor. MVP solo otorga XP
/// (nunca revoca), por lo que <see cref="AdaptationCorrection"/> queda
/// reservado para correcciones futuras de la adaptación.
///
/// Los miembros clínicos (<c>CLINICAL_*</c>) se nombran IGUAL que su código de
/// regla en <c>app.xp_rules</c> (SPEC §15): el dedupe parcial
/// <c>(source_ref_type, source_ref_id, reason)</c> de <c>xp_ledger</c> usa el
/// <c>reason</c> para garantizar un único otorgamiento por regla y período
/// clínico (el <c>HasConversion&lt;string&gt;</c> persiste el nombre del miembro).
/// </summary>
public enum XpReason
{
    TaskCompletion = 1,
    DailyBonus = 2,
    AdaptationCorrection = 3,

    /// <summary>Mejoría favorable de 1%..umbral de significancia (auto, sin validación).</summary>
    CLINICAL_IMPROVE = 4,

    /// <summary>Mejoría significativa aprobada por un clínico (requiere validación).</summary>
    CLINICAL_SIGNIFICANT = 5,

    /// <summary>Métrica estable (|Δ%| &lt; 1%, auto, sin validación).</summary>
    CLINICAL_STABLE = 6,

    /// <summary>Todas las métricas con medición del período favorables (auto, sin validación).</summary>
    CLINICAL_WEEKLY_ALL_UP = 7,

    /// <summary>
    /// Hito de racha de 7 días (SPEC §16): se otorga UNA vez por inscripción
    /// (dedupe parcial <c>(source_ref_type, source_ref_id, reason)</c> con
    /// <c>source_ref_type = 'streak_milestone'</c> y <c>source_ref_id =
    /// streak_states.enrollment_id</c>). Sin multiplicador (hito 7 no activa x2).
    /// </summary>
    STREAK_7 = 8,

    /// <summary>
    /// Hito de racha de 11 días (SPEC §16): activa el multiplicador x2 por 24h
    /// y otorga <c>STREAK_11</c> una única vez por inscripción.
    /// </summary>
    STREAK_11 = 9,

    /// <summary>
    /// Hito de racha de 22 días (SPEC §16): activa el multiplicador x2 por 48h
    /// y otorga <c>STREAK_22</c> una única vez por inscripción.
    /// </summary>
    STREAK_22 = 10,

    /// <summary>
    /// Hito de racha de 50 días (SPEC §16): activa el multiplicador x2 por 72h
    /// y otorga <c>STREAK_50</c> una única vez por inscripción.
    /// </summary>
    STREAK_50 = 11,

    /// <summary>
    /// Comida registrada (SPEC §18, B): XP granular por cada log de comida
    /// (<c>NUTRITION_MEAL_COMPLETE</c>, 10 base). Se otorga por el camino del
    /// catálogo (tope 4/día) con <c>source_ref_type = 'habit_log'</c> y
    /// <c>source_ref_id = habit_check.id</c> (dedupe parcial).
    /// </summary>
    NUTRITION_MEAL_COMPLETE = 12,

    /// <summary>
    /// Hidratación registrada (SPEC §18, B): XP granular por el log de agua
    /// (<c>NUTRITION_HYDRATION</c>, 5 base), tope 1/día, mismo dedupe
    /// <c>'habit_log'</c>.
    /// </summary>
    NUTRITION_HYDRATION = 13,

    /// <summary>
    /// Adherencia semanal de nutrición ≥ 85% (SPEC §18, C): <c>NUTRITION_WEEK_85</c>
    /// (75 base) una vez por período, evaluada SOLO en
    /// <c>POST /scores/calculate</c> con <c>source_ref_type =
    /// 'nutrition_period'</c> y <c>source_ref_id = health_scores.id</c>.
    /// </summary>
    NUTRITION_WEEK_85 = 14,

    /// <summary>
    /// Recuperación nutricional (SPEC §18, C): la adherencia del período subió
    /// ≥ 20 puntos vs el período anterior (<c>NUTRITION_RECOVERY</c>, 50 base),
    /// una vez por período, mismo dedupe <c>'nutrition_period'</c>.
    /// </summary>
    NUTRITION_RECOVERY = 15,
}