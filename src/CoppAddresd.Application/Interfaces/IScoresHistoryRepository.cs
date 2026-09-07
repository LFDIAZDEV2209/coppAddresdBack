using CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Lectura del historial de puntajes del paciente (scores-history): contexto
/// de la inscripción activa + filas persistidas de <c>app.health_scores</c> y
/// <c>app.transformation_scores</c>. Clase enfocada de SOLO LECTURA — no crece
/// <see cref="IProgramRepository"/> (~9k líneas; precedente:
/// <see cref="ILeagueRepository"/>). NUNCA dispara recálculo (sin
/// compute-on-read, sin ScoreTrigger): la frescura de la semana actual es
/// trabajo de GET /scores.
/// </summary>
public interface IScoresHistoryRepository
{
    /// <summary>
    /// Contexto del historial del paciente: semanas materializadas de su
    /// inscripción ACTIVA + filas persistidas de ambas tablas (ordenadas ASC
    /// por su eje). Null si el paciente no tiene inscripción activa (el
    /// handler responde 404 <c>NO_ACTIVE_ENROLLMENT</c>, anti-IDOR AC-11).
    /// </summary>
    Task<ScoresHistoryContext?> GetScoresHistoryContextAsync(
        Guid patientId,
        CancellationToken ct = default);
}