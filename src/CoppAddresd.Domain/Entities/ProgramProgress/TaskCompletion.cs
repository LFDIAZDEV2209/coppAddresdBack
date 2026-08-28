using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Una fila por tarea programada completada. Es la entidad que lleva la clave
/// de idempotencia <see cref="ClientRequestId"/> y las FKs de contenido
/// resuelto en runtime (una por código de tarea; solo se fija la que corresponde).
/// </summary>
public sealed class TaskCompletion
{
    public Guid Id { get; set; }

    public Guid EnrollmentId { get; set; }

    public Guid ProgramWeekId { get; set; }

    public Guid DailyCheckinId { get; set; }

    public DateOnly LocalDate { get; set; }

    /// <summary>Día de la semana (1..7) en que se completó.</summary>
    public short Weekday { get; set; }

    public TaskCode TaskCode { get; set; }

    /// <summary>Snapshot de <c>weekly_day_templates.points</c> al momento de completar; nunca se actualiza.</summary>
    public int PointsAwarded { get; set; }

    /// <summary>Clave de reintento del móvil (UUID/ULID).</summary>
    public string? ClientRequestId { get; set; }

    /// <summary>Reloj del servidor.</summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp del cliente, para auditoría de escrituras offline.</summary>
    public DateTime? ClientCompletedAt { get; set; }

    /// <summary>Origen: manual, auto_vitals, auto_media, auto_nutrition, auto_exercise.</summary>
    public string SourceRefType { get; set; } = default!;

    /// <summary>hash(plan_day_id|routine_id|media_id|vitals_id|nutribiotic_id|emotional_id) para detectar contenido obsoleto.</summary>
    public string? ContentFingerprint { get; set; }

    // FKs de contenido (nullable, una por código de tarea; solo la que
    // corresponde se fija en runtime. Sin FKs polimórficas).

    /// <summary>Contenido del task <c>nut</c>.</summary>
    public Guid? NutritionPlanId { get; set; }

    /// <summary>Número de día del plan de nutrición (1..7) usado para el task <c>nut</c>.</summary>
    public short? NutritionPlanDayNumber { get; set; }

    /// <summary>Contenido del task <c>ejercicio</c>.</summary>
    public Guid? ExerciseRoutineId { get; set; }

    /// <summary>Contenido del task <c>podcast</c>.</summary>
    public Guid? MediaId { get; set; }

    /// <summary>Contenido del task <c>vitals</c>.</summary>
    public Guid? VitalSignsBatchId { get; set; }

    /// <summary>Contenido del task <c>nutribiotico</c>.</summary>
    public Guid? NutribioticProductId { get; set; }

    /// <summary>Contenido del task <c>emocional</c>.</summary>
    public Guid? EmotionalRecordId { get; set; }

    public ProgramEnrollment? Enrollment { get; set; }

    public ProgramWeek? Week { get; set; }

    public DailyCheckIn? DailyCheckIn { get; set; }

    public NutritionPlan? NutritionPlan { get; set; }

    public ExerciseRoutine? ExerciseRoutine { get; set; }

    public MediaItem? Media { get; set; }

    public VitalSign? VitalSignsBatch { get; set; }

    public Product? NutribioticProduct { get; set; }

    public EmotionalRecord? EmotionalRecord { get; set; }
}