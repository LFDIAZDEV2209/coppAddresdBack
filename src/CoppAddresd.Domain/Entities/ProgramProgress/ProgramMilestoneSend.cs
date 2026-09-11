using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Registro de envío de un recordatorio de hito del programa: una fila por par
/// (inscripción, día de hito) — el índice único
/// <c>ix_program_milestone_sends_enrollment_day</c> garantiza la idempotencia
/// (cada hito se notifica como máximo una vez). El job de recordatorios
/// (<c>ProgramMilestoneSenderJob</c>) reclama la fila como Pending antes de
/// notificar y la pasa a Sent/Failed/Skipped según el resultado.
/// </summary>
public sealed class ProgramMilestoneSend
{
    public Guid Id { get; set; }

    /// <summary>Inscripción del programa que cumple el hito.</summary>
    public Guid EnrollmentId { get; set; }

    /// <summary>Día del programa del hito (7, 14, 21, 45, 60, 90).</summary>
    public int MilestoneDay { get; set; }

    /// <summary>Estado del envío (Pending por defecto).</summary>
    public ProgramMilestoneSendStatus Status { get; set; } = ProgramMilestoneSendStatus.Pending;

    /// <summary>Intentos de notificación acumulados (fallos transitorios).</summary>
    public int Attempts { get; set; }

    /// <summary>Thread de chat donde se inyectó el mensaje proactivo (si aplica).</summary>
    public string? ThreadId { get; set; }

    /// <summary>Momento del envío confirmado (null hasta pasar a Sent).</summary>
    public DateTime? SentAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ProgramEnrollment? Enrollment { get; set; }
}
