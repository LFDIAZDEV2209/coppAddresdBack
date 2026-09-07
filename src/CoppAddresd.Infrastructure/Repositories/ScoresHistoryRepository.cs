using CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Repositorio del historial de puntajes del paciente (scores-history):
/// semanas de la inscripción activa + proyecciones mínimas de
/// <c>app.health_scores</c> (índice patient_id + period_end DESC) y
/// <c>app.transformation_scores</c> (índice patient_id + week_number). Clase
/// enfocada de SOLO LECTURA: no crece <see cref="ProgramRepository"/> (~9k
/// líneas; precedente: <see cref="LeagueRepository"/>). SOLO filas
/// persistidas — nunca dispara el motor de puntajes (sin compute-on-read, sin
/// ScoreTrigger; la frescura de la semana actual es trabajo de GET /scores).
///
/// Alcance temporal acotado a la corrida actual: [inicio de la semana 1 de la
/// inscripción, fin de la semana actual local] — las filas de la corrida
/// anterior fueron purgadas al re-inscribir (SPEC §13.7.3) y el fetch nunca
/// recorre historia ilimitada del paciente.
/// </summary>
public sealed class ScoresHistoryRepository(AppDbContext dbContext) : IScoresHistoryRepository
{
    /// <inheritdoc />
    public async Task<ScoresHistoryContext?> GetScoresHistoryContextAsync(
        Guid patientId,
        CancellationToken ct = default
    )
    {
        var enrollment = await dbContext
            .ProgramEnrollments.AsNoTracking()
            .Where(e => e.PatientId == patientId && e.Status == ProgramEnrollmentStatus.Active)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new { e.Id, e.Timezone })
            .FirstOrDefaultAsync(ct);
        if (enrollment is null)
        {
            return null;
        }

        // Semanas materializadas de la inscripción (1..TotalWeeks): el eje del
        // alineamiento de períodos de salud.
        var weeks = await dbContext
            .ProgramWeeks.AsNoTracking()
            .Where(w => w.EnrollmentId == enrollment.Id)
            .OrderBy(w => w.WeekNumber)
            .Select(w => new ScoreWeekRow(w.WeekNumber, w.WeekStartDateLocal, w.WeekEndDateLocal))
            .ToListAsync(ct);

        // Límites de la ventana (corrida actual): desde el inicio de la semana
        // 1 hasta el fin de la semana que contiene el hoy local del paciente
        // (defensivo: la última semana materializada si el hoy cae fuera — no
        // debería ocurrir con una inscripción activa). Documentado: el
        // historial pertenece al programa en curso (SPEC §13.7.3).
        var today = PatientLocalToday(enrollment.Timezone);
        var firstWeek = weeks[0];
        var currentWeek = weeks.FirstOrDefault(
                w => today >= w.WeekStartDateLocal && today <= w.WeekEndDateLocal)
            ?? weeks[^1];
        var windowStart = firstWeek.WeekStartDateLocal;
        var windowEnd = currentWeek.WeekEndDateLocal;
        var currentWeekNumber = currentWeek.WeekNumber;

        // Filas de salud: ASC por period_end (el índice patient_id + period_end
        // DESC sirve en ambas direcciones); desempate final por Id para orden
        // totalmente determinista bajo (period_end, calculated_at) idénticos.
        // La más reciente del grupo gana en el alineamiento del handler.
        var healthRows = await dbContext
            .HealthScores.AsNoTracking()
            .Where(
                h =>
                    h.PatientId == patientId
                    && h.PeriodEnd >= windowStart
                    && h.PeriodEnd <= windowEnd
            )
            .OrderBy(h => h.PeriodEnd)
            .ThenBy(h => h.CalculatedAt)
            .ThenBy(h => h.Id)
            .Select(h => new HealthScoreHistoryRow(h.PeriodStart, h.PeriodEnd, h.Score, h.ScorePrevious))
            .ToListAsync(ct);

        // Filas de transformación: ASC por weekNumber; CalculatedAt DESC dentro
        // de la semana para el dedupe del handler (sin restricción única);
        // desempate final por Id (determinismo bajo empate exacto).
        var transformationRows = await dbContext
            .TransformationScores.AsNoTracking()
            .Where(
                t =>
                    t.PatientId == patientId
                    && t.WeekNumber >= firstWeek.WeekNumber
                    && t.WeekNumber <= currentWeekNumber
            )
            .OrderBy(t => t.WeekNumber)
            .ThenByDescending(t => t.CalculatedAt)
            .ThenByDescending(t => t.Id)
            .Select(t => new TransformationScoreHistoryRow(t.WeekNumber, t.Score))
            .ToListAsync(ct);

        return new ScoresHistoryContext(weeks, healthRows, transformationRows);
    }

    /// <summary>Hoy en zona local del paciente (espejo del helper del ProgramRepository).</summary>
    private static DateOnly PatientLocalToday(string timezone)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow.Date);
        }
    }
}