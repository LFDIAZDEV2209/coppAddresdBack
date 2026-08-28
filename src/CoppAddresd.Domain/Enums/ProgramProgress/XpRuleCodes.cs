namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Códigos canónicos de las reglas del catálogo de XP (SPEC §14.2). Viven en
/// Domain porque los comparten el seeder (proyecto Api: UPSERT idempotente por
/// <c>code</c>) y el motor de otorgamiento (repositorio Infrastructure:
/// resolución por código antes de escribir <c>app.xp_ledger</c>).
/// </summary>
public static class XpRuleCodes
{
    public const string TaskPodcast = "TASK_PODCAST";
    public const string TaskVitals = "TASK_VITALS";
    public const string TaskNut = "TASK_NUT";
    public const string TaskEjercicio = "TASK_EJERCICIO";
    public const string TaskNutribiotico = "TASK_NUTRIBIOTICO";
    public const string TaskEmocional = "TASK_EMOCIONAL";
    public const string DayBonus = "DAY_BONUS";
    public const string Streak7 = "STREAK_7";
    public const string Streak11 = "STREAK_11";
    public const string Streak22 = "STREAK_22";
    public const string Streak50 = "STREAK_50";

    // --- XP clínica (SPEC §15): otorgamientos basados en la evolución de las
    // métricas clínicas frente a la línea base (se disparan SOLO en
    // POST /scores/calculate). CLINICAL_SIGNIFICANT requiere validación de un
    // clínico (se crea una revisión pending); el resto se auto-otorgan.
    public const string ClinicalImprove = "CLINICAL_IMPROVE";
    public const string ClinicalSignificant = "CLINICAL_SIGNIFICANT";
    public const string ClinicalStable = "CLINICAL_STABLE";
    public const string ClinicalWeeklyAllUp = "CLINICAL_WEEKLY_ALL_UP";

    // --- Nutrición granular (SPEC §18): XP por log de comida/hidratación
    // (B, POST /program/nutrition/log) y por adherencia semanal (C, SOLO en
    // POST /scores/calculate). Los nombres coinciden con los miembros de
    // <see cref="XpReason"/> (dedupe parcial por reason), precedente CLINICAL_*.
    public const string NutritionMealComplete = "NUTRITION_MEAL_COMPLETE";
    public const string NutritionHydration = "NUTRITION_HYDRATION";
    public const string NutritionWeek85 = "NUTRITION_WEEK_85";
    public const string NutritionRecovery = "NUTRITION_RECOVERY";

    // --- Racha propia del nutribiótico (SPEC §19, B, "Paso 7a"): hitos de la
    // racha CONSECUTIVA de la tarea nutribiotico (independiente de la racha
    // general y de los congelamientos). Los nombres coinciden con los
    // miembros de <see cref="XpReason"/> (dedupe parcial por reason),
    // precedente CLINICAL_*/NUTRITION_*. Cada corrida de 7/14/... días
    // re-otorga su hito al alcanzarlo (AC-39).
    public const string NbStreak7 = "NB_STREAK_7";
    public const string NbStreak14 = "NB_STREAK_14";
    public const string NbStreak30 = "NB_STREAK_30";
    public const string NbStreak60 = "NB_STREAK_60";
    public const string NbStreak90 = "NB_STREAK_90";

    // --- Intervenciones (SPEC §22, "Paso 7d"): XP por eventos del ciclo de
    // vida de las intervenciones derivadas de debilidades. Se otorgan por el
    // camino del catálogo (§14.3) con dedupe parcial del libro mayor.
    // WEAKNESS_ASSESS se otorga al crear la intervención desde una debilidad;
    // INTERV_ACCEPT se otorga cuando el paciente acepta;
    // TELE_SCHEDULE/TELE_ATTEND/TELE_COMPLY se otorgan en los hooks de
    // telemedicina; INTERV_COMPLETE se otorga al completar la intervención
    // con resultado positivo; RECOVERY_MISSION se otorga al aceptar una
    // intervención de tipo recovery_mission.
    public const string WeaknessAssess = "WEAKNESS_ASSESS";
    public const string IntervAccept = "INTERV_ACCEPT";
    public const string TeleSchedule = "TELE_SCHEDULE";
    public const string TeleAttend = "TELE_ATTEND";
    public const string TeleComply = "TELE_COMPLY";
    public const string IntervComplete = "INTERV_COMPLETE";
    public const string RecoveryMission = "RECOVERY_MISSION";

    /// <summary>
    /// Código de regla de una tarea: <c>TASK_&lt;TASKCODE&gt;</c> (ej:
    /// <c>TASK_PODCAST</c>). Los nombres del enum <see cref="TaskCode"/> son
    /// minúsculas (contrato de la API); los códigos del catálogo son
    /// mayúsculas (SPEC §14.2), por lo que se normalizan con
    /// <c>ToUpperInvariant</c>.
    /// </summary>
    public static string ForTask(TaskCode taskCode) => $"TASK_{taskCode}".ToUpperInvariant();
}