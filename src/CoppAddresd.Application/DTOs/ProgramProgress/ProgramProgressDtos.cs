using System.Text.Json;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.DTOs.ProgramProgress;

/// <summary>
/// Resultado de una completación de tarea. Distingue entre escritura nueva,
/// replay idempotente (la misma tarea del mismo día ya existía) y clave de
/// idempotencia reutilizada con otra tarea/fecha (AC-04 → 409).
/// </summary>
public enum CompleteTaskOutcome
{
    Created,
    Replay,
    IdempotencyKeyReused,
}

/// <summary>
/// Entrada de la persistencia de una tarea completada. Los FKs de contenido
/// resueltos en runtime los calcula el handler (B4) y se persisten tal cual:
/// una por código de tarea, solo la que corresponde (SPEC §3.6, decisión 15).
/// </summary>
public sealed record CompleteTaskInput(
    Guid EnrollmentId,
    DateOnly LocalDate,
    TaskCode TaskCode,
    string? ClientRequestId,
    DateTime? ClientCompletedAt,
    short? MoodScore,
    string? Barriers,
    string? ContentFingerprint,
    string SourceRefType = "manual",
    Guid? NutritionPlanId = null,
    short? NutritionPlanDayNumber = null,
    Guid? ExerciseRoutineId = null,
    Guid? MediaId = null,
    Guid? VitalSignsBatchId = null,
    Guid? NutribioticProductId = null,
    Guid? EmotionalRecordId = null);

/// <summary>
/// Resultado tipado de <c>CompleteTaskAsync</c> (shape del response de
/// <c>POST /tasks/complete</c>, SPEC §7.2). En replay devuelve los datos de la
/// completación existente y el balance actual, sin otorgar XP dos veces.
/// </summary>
public sealed record CompleteTaskResult(
    CompleteTaskOutcome Outcome,
    Guid TaskCompletionId,
    int PointsAwarded,
    int XpBalanceAfter,
    bool IsPerfectDay,
    int DailyBonusAwarded,
    int StreakCurrent,
    int FreezesRemaining,
    int DayPoints,
    int DayPointsMax);

/// <summary>Snapshot del programa para la home del móvil (SPEC §7.1).</summary>
public sealed record ProgramSnapshotDto(
    Guid EnrollmentId,
    ProgramSnapshotTemplateDto Template,
    DateOnly TodayLocalDate,
    IReadOnlyList<TodayTaskDto> TodayTasks,
    int TodayPoints,
    bool TodayBonusAvailable,
    int TodayPointsMax,
    XpInfoDto Xp,
    StreakInfoDto Streak,
    int NextMilestoneDays,
    IReadOnlyList<CalendarDayDto> Calendar);

/// <summary>
/// Bloque de plantilla/semana actual del snapshot. <c>StreakMinTasks</c> y
/// <c>EssentialTaskCodes</c> vienen de la plantilla (SPEC §17, D): el móvil
/// puede renderizar "necesitas X tareas" y qué tareas rescatan con
/// congelamiento si se desea. Campos aditivos — no cambian los existentes.
/// </summary>
public sealed record ProgramSnapshotTemplateDto(
    Guid Id,
    string Code,
    string Name,
    int TotalWeeks,
    int CurrentWeekNumber,
    ProgramWeekStatus CurrentWeekStatus,
    DateOnly CurrentWeekStartDateLocal,
    DateOnly CurrentWeekEndDateLocal,
    int StreakMinTasks,
    IReadOnlyList<string> EssentialTaskCodes);

/// <summary>
/// Tarea de hoy del snapshot. <c>Content</c> trae el contenido multimedia
/// resuelto (título/duración/miniatura de <c>app.media_items</c>) para las
/// tareas <c>podcast</c>: las ya completadas usan su <c>mediaId</c> real y las
/// PENDIENTES el fallback template-level <c>weekly_day_templates.media_id</c>
/// del día (SPEC §4.4 y §7.1). P1:
/// <c>ThumbnailUrl</c> transporta la storage key de la miniatura; la forma
/// final de URL (proxy local / presign S3) se resuelve en la capa API (B5).
///
/// T-75: <c>ContentUnavailable</c> es true cuando la tarea es nut/ejercicio
/// y no hay asignación activa que cubra hoy (el móvil oculta el contenido y
/// muestra un fallback).
/// </summary>
public sealed record TodayTaskDto(
    TaskCode TaskCode,
    string Title,
    string Short,
    int Points,
    string Status,
    DateTime? CompletedAt,
    TodayTaskContentDto? Content,
    bool ContentUnavailable = false);

/// <summary>
/// Contenido multimedia de una tarea del día (podcast) más contenido resuelto
/// de nutrición y ejercicio (T-75/T-76). La resolución la hace el repositorio
/// en <c>GetSnapshotAsync</c> (una sola query set, sin N+1).
///
/// Campos de podcast: <c>MediaId</c>, <c>Title</c>, <c>DurationSecs</c>,
/// <c>ThumbnailUrl</c>.
/// Campos de nutrición: <c>NutritionPlanId</c>, <c>NutritionPlanName</c>,
/// <c>NutritionPlanDayNumber</c>.
/// Campos de ejercicio: <c>ExerciseRoutineId</c>, <c>ExerciseRoutineName</c>.
/// <c>ContentUnavailable</c>: true cuando una tarea tipo nut/ejercicio necesita
/// contenido pero no hay asignación activa que cubra hoy (SPEC §4.2/§4.3).
/// </summary>
public sealed record TodayTaskContentDto(
    Guid? MediaId,
    string? Title,
    int? DurationSecs,
    string? ThumbnailUrl,
    Guid? NutritionPlanId = null,
    string? NutritionPlanName = null,
    int? NutritionPlanDayNumber = null,
    Guid? ExerciseRoutineId = null,
    string? ExerciseRoutineName = null,
    bool ContentUnavailable = false);

/// <summary>XP + nivel de gamificación (nunca métrica clínica, SPEC §6.15).</summary>
public sealed record XpInfoDto(int Balance, string Level, int NextLevelAt);

/// <summary>
/// Estado de racha y congelamientos + multiplicador x2 activo (SPEC §16, D):
/// <c>MultiplierActive</c> es 1.0 sin multiplicador o 2.0 con x2 vigente;
/// <c>MultiplierEndsAt</c> es null cuando no hay multiplicador;
/// <c>MultiplierRemainingHours</c> son las horas restantes redondeadas hacia
/// abajo (0 sin multiplicador).
///
/// Campos aditivos de la racha propia del nutribiótico (SPEC §19, D — Paso 7a):
/// <c>NbStreak</c>/<c>NbLongestStreak</c> son la racha consecutiva de la tarea
/// <c>nutribiotico</c> y su máximo histórico; <c>NbNextMilestone</c> es el
/// próximo hito <c>{ days, xp, daysRemaining }</c> por encima de la racha
/// actual (null si ya llegó a 90). Con default para no romper los call sites
/// existentes — los campos previos no cambian.
/// </summary>
public sealed record StreakInfoDto(
    int Current,
    int Longest,
    int FreezesRemaining,
    decimal MultiplierActive,
    DateTime? MultiplierEndsAt,
    int MultiplierRemainingHours,
    int NbStreak = 0,
    int NbLongestStreak = 0,
    NbNextMilestoneDto? NbNextMilestone = null);

/// <summary>
/// Próximo hito de la racha propia del nutribiótico (SPEC §19, D): días del
/// hito, XP base del catálogo (<c>app.xp_rules</c>) y días restantes para
/// alcanzarlo. Null en el snapshot cuando la racha actual ya es ≥ 90.
/// </summary>
public sealed record NbNextMilestoneDto(int Days, int Xp, int DaysRemaining);

/// <summary>Día del mini calendario del snapshot (7 días).</summary>
public sealed record CalendarDayDto(
    DateOnly LocalDate,
    short Weekday,
    bool IsPerfectDay,
    int Points,
    string Status);

/// <summary>Calendario por día para una ventana (máx 92 días, SPEC §7.3).</summary>
public sealed record ProgramCalendarDto(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<CalendarDayDetailDto> Days,
    CalendarSummaryDto Summary);

/// <summary>Rollup diario del calendario.</summary>
public sealed record CalendarDayDetailDto(
    DateOnly LocalDate,
    short Weekday,
    int WeekNumber,
    bool IsPerfectDay,
    int Points,
    int BonusAwarded,
    IReadOnlyList<string> CompletedTaskCodes);

/// <summary>Resumen de la ventana del calendario.</summary>
public sealed record CalendarSummaryDto(int PerfectDays, int MissedDays, int TotalXp);

/// <summary>Sendero de 83 semanas (SPEC §7.4).</summary>
public sealed record ProgramPathDto(IReadOnlyList<ProgramPathWeekDto> Weeks);

/// <summary>
/// Semana del sendero. <c>IsPerfectWeek</c> es null para semanas Locked/Active
/// (solo las Completed lo definen, SPEC §7.4).
/// </summary>
public sealed record ProgramPathWeekDto(
    int WeekNumber,
    ProgramWeekStatus Status,
    bool? IsPerfectWeek,
    int Points,
    DateOnly WeekStartDateLocal,
    DateOnly WeekEndDateLocal);

/// <summary>Acción de decisión de una recomendación de adaptación (SPEC §5.6).</summary>
public enum AdaptationDecisionAction
{
    Approve,
    Reject,
    Apply,
}

/// <summary>
/// Catálogo de presentación de las 6 tareas del programa (espejo del mock
/// móvil <c>PROGRAM_TASKS</c> en antares-paciente). Solo títulos y subtítulos;
/// los puntos vienen del snapshot de la semana.
/// </summary>
public static class ProgramTaskCatalog
{
    public static (string Title, string Short) For(TaskCode code) => code switch
    {
        TaskCode.podcast => ("Escuchar podcast", "Biohacking y metabolismo · 8 min"),
        TaskCode.vitals => ("Medir signos vitales", "FC · SpO2 · Glucosa · Peso"),
        TaskCode.nut => ("Cumplir plan nutricional", "Mediterráneo · 1,800 kcal"),
        TaskCode.ejercicio => ("Hacer ejercicio del día", "Circuito 12 min · Semana 12"),
        TaskCode.nutribiotico => ("Tomar nutribiótico", "Dosis diaria matutina"),
        TaskCode.emocional => ("Evaluación emocional", "Estado psicológico · Semana 12"),
        _ => ("Tarea", ""),
    };
}

/// <summary>
/// Escalera de niveles de gamificación (espejo de <c>PROGRAM_LEVELS</c> del
/// mock móvil). El nivel es un derivado de presentación de la XP; nunca una
/// métrica clínica (SPEC §6.15).
/// </summary>
public static class XpLevels
{
    private static readonly (string Name, int Min, int Max)[] Ladder =
    [
        ("Explorador", 0, 499),
        ("Iniciado", 500, 1499),
        ("Constante", 1500, 2999),
        ("Disciplinado", 3000, 4999),
        ("Transformación", 5000, 7999),
        ("Bienestar", 8000, 11999),
        ("Maestro", 12000, 99999),
    ];

    /// <summary>
    /// Devuelve el nivel para un balance y el mínimo del siguiente nivel (para
    /// el nivel máximo devuelve su propio tope: no hay nivel superior).
    /// </summary>
    public static (string Level, int NextLevelAt) ForBalance(int balance)
    {
        for (var i = Ladder.Length - 1; i >= 0; i--)
        {
            if (balance >= Ladder[i].Min)
            {
                var next = i + 1 < Ladder.Length ? Ladder[i + 1].Min : Ladder[i].Max;
                return (Ladder[i].Name, next);
            }
        }

        return (Ladder[0].Name, Ladder[1].Min);
    }
}

/// <summary>
/// Respuesta de <c>POST /tasks/complete</c> (SPEC §7.2). El cuerpo del replay
/// idempotente es idéntico al de la primera escritura.
/// </summary>
public sealed record CompleteTaskResponseDto(
    Guid TaskCompletionId,
    int PointsAwarded,
    int XpBalanceAfter,
    bool IsPerfectDay,
    int DailyBonusAwarded,
    int StreakCurrent,
    int FreezesRemaining,
    int DayPoints,
    int DayPointsMax)
{
    public static CompleteTaskResponseDto FromResult(CompleteTaskResult r) => new(
        r.TaskCompletionId,
        r.PointsAwarded,
        r.XpBalanceAfter,
        r.IsPerfectDay,
        r.DailyBonusAwarded,
        r.StreakCurrent,
        r.FreezesRemaining,
        r.DayPoints,
        r.DayPointsMax);
}

/// <summary>
/// Fila por día de la semana de una plantilla (SPEC §7.6): qué tarea corre,
/// con qué puntos base y en qué orden. Es la misma forma para la respuesta y
/// para el payload de creación/edición (CRUD de plantillas).
/// </summary>
public sealed record WeeklyDayTemplateRequest(
    short Weekday,
    TaskCode TaskCode,
    int Points,
    int SortOrder = 0,
    Guid? MediaId = null,
    Guid? RoutineId = null,
    Guid? NutritionPlanId = null);

/// <summary>Fila por día de la semana de una plantilla (respuesta).</summary>
public sealed record WeeklyDayTemplateDto(
    Guid Id,
    short Weekday,
    TaskCode TaskCode,
    int Points,
    int SortOrder,
    Guid? MediaId,
    DateTime CreatedAt)
{
    public static WeeklyDayTemplateDto FromEntity(Domain.Entities.ProgramProgress.WeeklyDayTemplate d) => new(
        d.Id, d.Weekday, d.TaskCode, d.Points, d.SortOrder, d.MediaId, d.CreatedAt);
}

/// <summary>
/// Plantilla de programa con su definición semanal (respuesta de
/// <c>GET /templates/{id}</c> y de los comandos de plantilla, SPEC §7.6).
/// </summary>
public sealed record ProgramTemplateDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int TotalWeeks,
    TemplateStatus Status,
    int Version,
    Guid? CreatedBy,
    Guid? UpdatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PublishedAt,
    IReadOnlyList<WeeklyDayTemplateDto> Days)
{
    public static ProgramTemplateDto FromEntity(Domain.Entities.ProgramProgress.ProgramTemplate t) => new(
        t.Id, t.Code, t.Name, t.Description, t.TotalWeeks, t.Status, t.Version,
        t.CreatedBy, t.UpdatedBy, t.CreatedAt, t.UpdatedAt, t.PublishedAt,
        t.DayTemplates.Select(WeeklyDayTemplateDto.FromEntity).ToList());
}

/// <summary>Ítem del listado de plantillas (sin días; el detalle va a <c>GET /templates/{id}</c>).</summary>
public sealed record ProgramTemplateListItemDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int TotalWeeks,
    TemplateStatus Status,
    int Version,
    DateTime CreatedAt,
    DateTime? PublishedAt);

/// <summary>Resultado paginado del listado de plantillas (SPEC §7.6).</summary>
public sealed record PaginatedTemplatesResult(
    IReadOnlyList<ProgramTemplateListItemDto> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>
/// Inscripción de un paciente al programa con su estado de gamificación
/// (racha, congelamientos y balance XP). Se usa como respuesta de
/// enroll/pause/resume/withdraw y en el listado del ERP (SPEC §7.5).
/// </summary>
public sealed record ProgramEnrollmentDto(
    Guid Id,
    Guid PatientId,
    Guid TemplateId,
    string Timezone,
    ProgramEnrollmentStatus Status,
    DateTime StartedAt,
    DateOnly StartLocalDate,
    int CurrentWeekNumber,
    int TotalWeeks,
    int XpBalance,
    int StreakCurrent,
    int StreakLongest,
    int FreezesRemaining,
    DateTime? CompletedAt,
    DateTime? PausedAt,
    DateTime? WithdrawnAt,
    DateTime CreatedAt);

/// <summary>Resultado paginado del listado de inscripciones (SPEC §7.5).</summary>
public sealed record PaginatedEnrollmentsResult(
    IReadOnlyList<ProgramEnrollmentDto> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>
/// Recomendación de adaptación visible para el clínico (SPEC §7.7). El payload
/// es un <c>jsonb</c> opaco para la capa de aplicación: se serializa tal cual.
/// </summary>
public sealed record AdaptationRecommendationDto(
    Guid Id,
    Guid EnrollmentId,
    AdaptationKind Kind,
    AdaptationTargetEntityType TargetEntityType,
    Guid TargetEntityId,
    JsonElement Payload,
    string Reason,
    AdaptationStatus Status,
    bool RequiresApproval,
    Guid? RequestedBy,
    Guid? DecidedBy,
    DateTime? DecidedAt,
    DateTime? AppliedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static AdaptationRecommendationDto FromEntity(
        Domain.Entities.ProgramProgress.AdaptationRecommendation a) => new(
        a.Id, a.EnrollmentId, a.Kind, a.TargetEntityType, a.TargetEntityId,
        a.Payload, a.Reason, a.Status, a.RequiresApproval,
        a.RequestedBy, a.DecidedBy, a.DecidedAt, a.AppliedAt,
        a.CreatedAt, a.UpdatedAt);
}

/// <summary>Resultado paginado de recomendaciones de adaptación (SPEC §7.7).</summary>
public sealed record PaginatedAdaptationsResult(
    IReadOnlyList<AdaptationRecommendationDto> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>
/// Regla del catálogo de XP visible para el administrador (SPEC §14.5, ERP):
/// wire shape de <c>GET /api/v1/program/xp-rules</c> y de
/// <c>PUT /api/v1/program/xp-rules/{code}</c>.
/// </summary>
public sealed record XpRuleDto(
    Guid Id,
    string Code,
    string Name,
    string Category,
    int? BaseXp,
    decimal Multiplier,
    int? MaxPerDay,
    int? MaxPerWeek,
    bool RequiresValidation,
    bool Active,
    DateOnly ValidFrom,
    DateOnly? ValidUntil,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static XpRuleDto FromEntity(Domain.Entities.ProgramProgress.XpRule r) => new(
        r.Id, r.Code, r.Name, r.Category, r.BaseXp, r.Multiplier,
        r.MaxPerDay, r.MaxPerWeek, r.RequiresValidation, r.Active,
        r.ValidFrom, r.ValidUntil, r.CreatedAt, r.UpdatedAt);
}

// ===================== T-77: Endpoints del configurador =====================

/// <summary>
/// Respuesta de <c>GET /program/enrollments/{id}/content</c> (T-77): contenido
/// de nutrición y ejercicio configurado por semana para la inscripción.
/// </summary>
public sealed record ProgramContentResponse(
    Guid EnrollmentId,
    Guid PatientId,
    Guid TemplateId,
    int TotalWeeks,
    DateOnly StartLocalDate,
    IReadOnlyList<ProgramContentWeekDto> Weeks);

/// <summary>
/// Contenido configurado para una semana del programa (T-77): plan de
/// alimentación y rutina de ejercicio activos para la ventana de 7 días.
/// </summary>
public sealed record ProgramContentWeekDto(
    int WeekNumber,
    DateOnly WeekStartDateLocal,
    DateOnly WeekEndDateLocal,
    ProgramContentPlanRef? NutritionPlan,
    ProgramContentRoutineRef? ExerciseRoutine);

/// <summary>Referencia a un plan de alimentación (T-77).</summary>
public sealed record ProgramContentPlanRef(Guid Id, string Code, string Name);

/// <summary>Referencia a una rutina de ejercicio (T-77).</summary>
public sealed record ProgramContentRoutineRef(Guid Id, string Code, string Name);

/// <summary>
/// Payload de <c>PUT /program/enrollments/{id}/content/week/{weekNumber}</c>
/// (T-77): ambos campos opcionales; null o ausente = desasignar esa dimensión
/// para la semana.
/// </summary>
public sealed record SetWeekContentRequest(
    Guid? NutritionPlanId = null,
    Guid? ExerciseRoutineId = null);

// ===================== GET /enrollments/{id}/week/{weekNumber} =====================

/// <summary>
/// Respuesta de <c>GET /program/enrollments/{id}/week/{weekNumber}</c>:
/// detalle de una semana con tareas, completaciones y contenido activo.
/// </summary>
public sealed record EnrollmentWeekDetailDto(
    int WeekNumber,
    DateOnly WeekStartDateLocal,
    DateOnly WeekEndDateLocal,
    EnrollmentWeekContentRef? NutritionPlan,
    EnrollmentWeekContentRef? ExerciseRoutine,
    IReadOnlyList<EnrollmentWeekDayDto> Days);

/// <summary>Referencia a contenido activo de la semana (plan o rutina).</summary>
public sealed record EnrollmentWeekContentRef(Guid Id, string Name);

/// <summary>Día detallado dentro de una semana del programa.</summary>
public sealed record EnrollmentWeekDayDto(
    DateOnly LocalDate,
    short Weekday,
    string DayLabel,
    bool IsPerfectDay,
    int TotalPoints,
    int MaxPoints,
    int BonusAwarded,
    IReadOnlyList<EnrollmentWeekTaskDto> Tasks);

/// <summary>Tarea individual de un día (completada o pendiente) con metadatos de contenido resuelto.</summary>
public sealed record EnrollmentWeekTaskDto(
    string TaskCode,
    string TaskLabel,
    int Points,
    int ScheduledPoints,
    string Status,
    DateTime? CompletedAt,
    Guid? ContentRefId = null,
    string? ContentName = null,
    string? DetailText = null);

/// <summary>
/// Etiquetas en español neutro de las tareas del programa (SPEC §7.1).
/// Usado por el endpoint de detalle de semana.
/// </summary>
public static class EnrollmentWeekTaskLabels
{
    private static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["podcast"] = "Podcast",
            ["vitals"] = "Signos vitales",
            ["nut"] = "Plan nutricional",
            ["ejercicio"] = "Ejercicio",
            ["nutribiotico"] = "Nutribiótico",
            ["emocional"] = "Evaluación emocional",
        };

    private static readonly IReadOnlyDictionary<short, string> DayLabels =
        new Dictionary<short, string>
        {
            [1] = "Lunes",
            [2] = "Martes",
            [3] = "Miércoles",
            [4] = "Jueves",
            [5] = "Viernes",
            [6] = "Sábado",
            [7] = "Domingo",
        };

    public static string TaskLabel(string taskCode)
        => Labels.TryGetValue(taskCode, out var label) ? label : taskCode;

    public static string DayLabel(short weekday)
        => DayLabels.TryGetValue(weekday, out var label) ? label : $"Día {weekday}";
}