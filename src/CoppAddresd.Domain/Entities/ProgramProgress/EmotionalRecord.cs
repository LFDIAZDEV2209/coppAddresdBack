namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>
/// Registro de la tarea emocional: el ánimo (1..5) del paciente en una fecha
/// local, persistido como fila de primera clase para que el clínico revise el
/// historial emocional independiente de la XP.
/// </summary>
public sealed class EmotionalRecord
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    /// <summary>Inscripción relacionada; null si el registro no está ligado a un programa.</summary>
    public Guid? ProgramEnrollmentId { get; set; }

    public DateOnly RecordedLocalDate { get; set; }

    /// <summary>Ánimo reportado (1..5).</summary>
    public short MoodScore { get; set; }

    /// <summary>Barrera reportada (id de <c>WEEK_BARRIERS</c> del móvil); opcional.</summary>
    public string? Barriers { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PatientProfile? Patient { get; set; }

    public ProgramEnrollment? Enrollment { get; set; }
}