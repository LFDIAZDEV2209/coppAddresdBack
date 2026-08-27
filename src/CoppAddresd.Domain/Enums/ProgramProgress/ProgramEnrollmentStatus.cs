namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Estado de una inscripción de un paciente al programa.
/// Active = en curso, Paused = pausada (bloquea completar tareas),
/// Completed = terminal al llegar a la semana final, Withdrawn = terminal (retirada).
/// </summary>
public enum ProgramEnrollmentStatus
{
    Active = 1,
    Paused = 2,
    Completed = 3,
    Withdrawn = 4,
}