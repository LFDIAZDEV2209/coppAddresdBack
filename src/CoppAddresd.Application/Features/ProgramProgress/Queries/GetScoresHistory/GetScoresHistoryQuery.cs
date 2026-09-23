using CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetScoresHistory;

/// <summary>
/// Historial de puntajes del paciente autenticado (pestaña Evolución del
/// móvil): serie semanal ASCENDENTE de Índice de Salud + Índice de
/// Transformación. SOLO filas PERSISTIDAS — nunca dispara compute-on-read ni
/// al motor de puntajes (la frescura de la semana actual sigue siendo trabajo
/// de <c>GET /scores</c>, SPEC §13.3).
///
/// El <c>patientId</c> SIEMPRE llega resuelto de la identidad del JWT por la
/// capa API (nunca del body, anti-IDOR AC-11): sin inscripción activa → 404
/// <c>NO_ACTIVE_ENROLLMENT</c>.
///
/// Cache: clave por paciente <c>scores-history:{{patientId}}:v1</c>, TTL
/// 5 min, fail-open (la abstracción lo garantiza). El payload cacheado es la
/// serie COMPLETA del paciente; el recorte a las últimas N semanas se aplica
/// por request (nunca se cachea por-petición).
/// </summary>
public sealed record GetScoresHistoryQuery(Guid PatientId, int Weeks)
    : IRequest<ScoresHistoryResponseDto>
{
    /// <summary>Valor por defecto de <c>weeks</c> (12 semanas ≈ un trimestre).</summary>
    public const int DefaultWeeks = 12;

    /// <summary>Tope duro de la ventana: la plantilla del programa es de 83 semanas.</summary>
    public const int MaxWeeks = 83;
}

/// <summary>
/// Orquesta la lectura del historial: contexto del repositorio (dos queries
/// indexadas: health_scores por patient_id+period_end, transformation_scores
/// por patient_id+week_number) + alineamiento de períodos contra las semanas
/// de la inscripción, cacheado 5 min por paciente con recorte por-request.
/// </summary>
public sealed class GetScoresHistoryQueryHandler(
    IScoresHistoryRepository repository,
    ICacheService cache,
    ILogger<GetScoresHistoryQueryHandler> logger
) : IRequestHandler<GetScoresHistoryQuery, ScoresHistoryResponseDto>
{
    public async Task<ScoresHistoryResponseDto> Handle(
        GetScoresHistoryQuery request,
        CancellationToken ct
    )
    {
        var weeks = Math.Clamp(request.Weeks, 1, GetScoresHistoryQuery.MaxWeeks);

        var points = await cache.GetOrCreateAsync(
            CacheKeys.ScoresHistory(request.PatientId),
            CacheKeys.ScoresHistoryTtl,
            async token =>
            {
                var context =
                    await repository.GetScoresHistoryContextAsync(request.PatientId, token)
                    ?? throw new NotFoundException(
                        $"NO_ACTIVE_ENROLLMENT: no existe una inscripción activa para el paciente {request.PatientId}."
                    );
                return BuildPoints(context);
            },
            ct
        );

        // Recorte por-request (nunca cacheado): las últimas N semanas.
        IReadOnlyList<ScoresHistoryPointDto> trimmed =
            points.Count <= weeks ? points : points.Skip(points.Count - weeks).ToList();

        // Log estructurado sin PHI (espejo de la regla §13.6 del motor de puntajes).
        logger.LogInformation(
            "Program.ScoresHistory: semanas={Weeks} puntos={Count}",
            weeks,
            trimmed.Count
        );

        return new ScoresHistoryResponseDto(trimmed);
    }

    /// <summary>
    /// Construye la serie: un punto por semana PERSISTIDA (nunca semanas
    /// vacías), ASC por <c>weekNumber</c>. Cada fila de transformación ancla
    /// un punto (período = rango de la semana del programa); las filas de
    /// salud se asignan a la semana cuyo rango contiene su <c>period_end</c>
    /// y se fusionan con el punto de transformación de esa semana; si la
    /// semana no tiene transformación, la fila de salud emite su propio punto
    /// con el período PERSISTIDO de la fila. Cuando hay varias filas de salud
    /// en la misma semana gana la de <c>period_end</c> más reciente (la
    /// fresca). Filas de salud sin semana contenedora en la inscripción → no
    /// emitibles (defensivo: el motor solo persiste períodos dentro del
    /// programa).
    /// </summary>
    private static IReadOnlyList<ScoresHistoryPointDto> BuildPoints(ScoresHistoryContext context)
    {
        var weeksByNumber = context.Weeks.ToDictionary(w => w.WeekNumber);

        // Alineamiento: healthRows llega ASC por period_end → la última fila
        // del grupo gana (period_end más reciente = la fresca del período).
        var healthByWeek = new Dictionary<int, HealthScoreHistoryRow>();
        foreach (var health in context.HealthRows)
        {
            var week = context.Weeks.FirstOrDefault(w =>
                health.PeriodEnd >= w.WeekStartDateLocal && health.PeriodEnd <= w.WeekEndDateLocal
            );
            if (week is not null)
            {
                healthByWeek[week.WeekNumber] = health;
            }
        }

        var points = new List<ScoresHistoryPointDto>(
            context.TransformationRows.Count + healthByWeek.Count
        );
        var seen = new HashSet<int>();
        foreach (var transformation in context.TransformationRows)
        {
            // Sin restricción única por semana: gana la fila más reciente
            // (el repositorio ordena CalculatedAt DESC dentro de la semana).
            if (
                !seen.Add(transformation.WeekNumber)
                || !weeksByNumber.TryGetValue(transformation.WeekNumber, out var week)
            )
            {
                continue;
            }

            var health = healthByWeek.TryGetValue(transformation.WeekNumber, out var h) ? h : null;
            points.Add(
                new ScoresHistoryPointDto(
                    transformation.WeekNumber,
                    week.WeekStartDateLocal,
                    week.WeekEndDateLocal,
                    health?.Score,
                    health?.ScorePrevious,
                    transformation.Score,
                    DimensionsOf(health)
                )
            );
            healthByWeek.Remove(transformation.WeekNumber);
        }

        // Filas de salud sin transformación en su semana: punto propio con el
        // período PERSISTIDO de la fila (periodStart/periodEnd del registro).
        foreach (var (weekNumber, health) in healthByWeek.OrderBy(kv => kv.Key))
        {
            points.Add(
                new ScoresHistoryPointDto(
                    weekNumber,
                    health.PeriodStart,
                    health.PeriodEnd,
                    health.Score,
                    health.ScorePrevious,
                    null,
                    DimensionsOf(health)
                )
            );
        }

        points.Sort((a, b) => a.WeekNumber.CompareTo(b.WeekNumber));
        return points;
    }

    /// <summary>
    /// Dimensiones del punto desde la fila de salud; null sin fila (semana
    /// solo con transformación). Alimenta la tarjeta Adherencia del Home
    /// móvil (<c>dimensions.adherence</c>).
    /// </summary>
    private static HealthScoreDimensionsDto? DimensionsOf(HealthScoreHistoryRow? health) =>
        health is null
            ? null
            : new HealthScoreDimensionsDto(
                health.ScoreAdherence,
                health.ScoreClinical,
                health.ScoreNutrition,
                health.ScorePsychology,
                health.ScoreExercise
            );
}
