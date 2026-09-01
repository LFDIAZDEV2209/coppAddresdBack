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

    /// <summary>
    /// Hito de la racha propia del nutribiótico de 7 días (SPEC §19, B):
    /// <c>NB_STREAK_7</c> (50 base) se otorga cuando la racha CONSECUTIVA de la
    /// tarea <c>nutribiotico</c> llega exactamente a 7. A diferencia de los
    /// hitos de la racha general (SPEC §16, una vez por inscripción), cada
    /// corrida de 7 días re-otorga su hito al alcanzarlo (AC-39): el dedupe
    /// parcial usa <c>source_ref_type = 'nb_milestone'</c> con
    /// <c>source_ref_id = task_completions.id</c> (la completación que disparó
    /// el hito — única por corrida), por lo que el re-otorgamiento en una nueva
    /// corrida NO colisiona con el de la corrida anterior. Los congelamientos
    /// NO protegen esta racha (AC-38).
    /// </summary>
    NB_STREAK_7 = 16,

    /// <summary>Hito de la racha del nutribiótico de 14 días (<c>NB_STREAK_14</c>, 100 base, SPEC §19).</summary>
    NB_STREAK_14 = 17,

    /// <summary>Hito de la racha del nutribiótico de 30 días (<c>NB_STREAK_30</c>, 250 base, SPEC §19).</summary>
    NB_STREAK_30 = 18,

    /// <summary>Hito de la racha del nutribiótico de 60 días (<c>NB_STREAK_60</c>, 500 base, SPEC §19).</summary>
    NB_STREAK_60 = 19,

    /// <summary>Hito de la racha del nutribiótico de 90 días (<c>NB_STREAK_90</c>, 1000 base, SPEC §19).</summary>
    NB_STREAK_90 = 20,

    /// <summary>
    /// Evaluación de debilidad (SPEC §22, "Paso 7d"): +20 XP al crear una
    /// intervención desde una debilidad detectada. Se otorga UNA vez por
    /// intervención (dedupe parcial <c>(source_ref_type='intervention',
    /// source_ref_id=intervention.id, reason='WEAKNESS_ASSESS')</c>).
    /// Sin validación clínica requerida.
    /// </summary>
    WEAKNESS_ASSESS = 21,

    /// <summary>
    /// Aceptación de intervención por el paciente (SPEC §22, D): +15 XP
    /// cuando el paciente acepta una intervención (<c>detected→accepted</c>).
    /// Dedupe por <c>(source_ref_type='intervention', source_ref_id,
    /// reason='INTERV_ACCEPT')</c>. Sin validación requerida.
    /// </summary>
    INTERV_ACCEPT = 22,

    /// <summary>
    /// Teleconsulta agendada (SPEC §22, D): +50 XP cuando se agenda una
    /// teleconsulta vinculada a una intervención. Sin validación requerida.
    /// </summary>
    TELE_SCHEDULE = 23,

    /// <summary>
    /// Teleconsulta asistida con confirmación del clínico (SPEC §22, D): +100 XP
    /// cuando el clínico confirma la asistencia a la teleconsulta.
    /// <c>requires_validation = true</c> (validated_by = clínico).
    /// </summary>
    TELE_ATTEND = 24,

    /// <summary>
    /// Cumplimiento de teleconsulta evaluado (SPEC §22, D): +50 XP cuando el
    /// clínico/IA evalúa el cumplimiento. Sin validación requerida.
    /// </summary>
    TELE_COMPLY = 25,

    /// <summary>
    /// Intervención completada (SPEC §22, D): +200 XP cuando la intervención
    /// se completa con resultado positivo. <c>requires_validation = true</c>
    /// (validated_by = clínico que confirma el resultado). Marcada la
    /// debilidad vinculada como <c>resolved</c>.
    /// </summary>
    INTERV_COMPLETE = 26,

    /// <summary>
    /// Misión de recuperación aceptada (SPEC §22, D): +50 XP adicional cuando
    /// el paciente acepta una intervención de tipo <c>recovery_mission</c>.
    /// Sin validación requerida.
    /// </summary>
    RECOVERY_MISSION = 27,

    // --- Hitos de racha del catálogo extendido (módulo "cofres"): misma
    // mecánica que STREAK_7/11/22/50 (SPEC §16, B — UNA vez por inscripción,
    // source_ref_type = 'streak_milestone', dedupe parcial por reason). Ninguno
    // activa multiplicador x2 (el multiplicador queda exclusivamente en
    // 11/22/50). Se persisten como string (HasConversion), sin migración.

    /// <summary>Hito de racha de 14 días (una vez por inscripción, sin multiplicador).</summary>
    STREAK_14 = 28,

    /// <summary>Hito de racha de 30 días (una vez por inscripción, sin multiplicador).</summary>
    STREAK_30 = 29,

    /// <summary>Hito de racha de 75 días (una vez por inscripción, sin multiplicador).</summary>
    STREAK_75 = 30,

    /// <summary>Hito de racha de 100 días (una vez por inscripción, sin multiplicador).</summary>
    STREAK_100 = 31,
}