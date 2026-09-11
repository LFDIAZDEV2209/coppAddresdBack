using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Control conversacional del programa ("Controles", extensión de UC-001): una
/// fila por par (inscripción, día de hito) que modela el ciclo de vida de un
/// recordatorio proactivo (hito del programa) y su seguimiento — el índice
/// único <c>ix_program_controls_enrollment_day</c> garantiza la idempotencia
/// (cada hito se notifica como máximo una vez). El job de recordatorios
/// (<c>ProgramControlJob</c>) reclama la fila como Pending antes de notificar y
/// la pasa a Sent/Failed/Skipped según el resultado; el flujo conversacional
/// (fase 2) agrega los estados de respuesta, seguimiento y cierre sobre las
/// columnas de ciclo de vida (responded_at, followup_sent_at, completed_at,
/// closed_reason, exam_batch_id).
/// </summary>
public sealed class ProgramControl
{
    public Guid Id { get; set; }

    /// <summary>Inscripción del programa que cumple el hito.</summary>
    public Guid EnrollmentId { get; set; }

    /// <summary>Día del programa del hito (7, 14, 21, 45, 60, 90).</summary>
    public int MilestoneDay { get; set; }

    /// <summary>Estado del control (Pending por defecto).</summary>
    public ProgramControlStatus Status { get; set; } = ProgramControlStatus.Pending;

    /// <summary>Intentos de notificación acumulados (fallos transitorios).</summary>
    public int Attempts { get; set; }

    /// <summary>Thread de chat donde se inyectó el mensaje proactivo (si aplica).</summary>
    public string? ThreadId { get; set; }

    /// <summary>Momento del envío confirmado (null hasta pasar a Sent).</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>Momento en que el paciente respondió en el control abierto (null hasta Responded).</summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>Momento del follow-up de la fase 2 (null hasta FollowedUp).</summary>
    public DateTime? FollowupSentAt { get; set; }

    /// <summary>Momento de completación (subida de examen o cierre positivo; null hasta Completed).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Motivo de cierre sin examen cuando el control se cierra negativo:
    /// <c>declined</c> (rechazo explícito del paciente) o
    /// <c>no_upload_timeout</c> (venció la ventana de subida). Validado por
    /// CHECK en la base de datos.
    /// </summary>
    public string? ClosedReason { get; set; }

    /// <summary>
    /// Id del lote de examen de laboratorio asociado al control completado
    /// (sin FK: el lote pertenece al módulo de exámenes; la referencia es
    /// best-effort desde <c>exam_batch_id</c>).
    /// </summary>
    public Guid? ExamBatchId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ProgramEnrollment? Enrollment { get; set; }
}