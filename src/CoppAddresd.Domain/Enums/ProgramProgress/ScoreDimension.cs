namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Dimensiones del Índice de Salud (SPEC §13.4). Los nombres del enum son
/// exactamente los valores almacenados (<c>varchar(20)</c> en
/// <c>app.health_score_weights.dimension</c>) y los que la fórmula espera:
/// <c>adherence</c>, <c>clinical</c>, <c>nutrition</c>, <c>psychology</c>,
/// <c>exercise</c>. Convención de nombres en minúscula igual que
/// <see cref="TaskCode"/>.
/// </summary>
public enum ScoreDimension
{
    adherence = 1,
    clinical = 2,
    nutrition = 3,
    psychology = 4,
    exercise = 5,
}