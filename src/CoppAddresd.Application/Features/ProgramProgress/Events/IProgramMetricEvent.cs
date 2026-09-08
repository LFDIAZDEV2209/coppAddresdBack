namespace CoppAddresd.Application.Features.ProgramProgress.Events;

/// <summary>
/// Contrato marcador para eventos de métricas del programa ANTARES procesados en background.
/// </summary>
public interface IProgramMetricEvent;

/// <summary>Evento emitido al completar una tarea de programa.</summary>
public sealed record TaskCompletedMetricEvent(
    DateOnly LocalDate,
    string TaskCode,
    int PointsAwarded
) : IProgramMetricEvent;

/// <summary>Evento emitido al otorgar XP (por tarea, comida, hidratación o bonus).</summary>
public sealed record XpAwardedMetricEvent(
    DateOnly AwardDate,
    string Reason,
    int Amount
) : IProgramMetricEvent;

/// <summary>Evento emitido al actualizar la racha de un paciente.</summary>
public sealed record StreakUpdatedMetricEvent(
    DateOnly LocalDate,
    int CurrentStreak
) : IProgramMetricEvent;
