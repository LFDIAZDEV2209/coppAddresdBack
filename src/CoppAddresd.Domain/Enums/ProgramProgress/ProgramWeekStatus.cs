namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Estado de una semana concreta dentro de una inscripción.
/// Locked = bloqueada (futura), Active = vigente (se pueden completar tareas),
/// Completed = finalizada (perfecta o no).
/// </summary>
public enum ProgramWeekStatus
{
    Locked = 1,
    Active = 2,
    Completed = 3,
}