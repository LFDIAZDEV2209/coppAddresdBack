using CoppAddresd.Application.Features.ProgramProgress.DTOs.League;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetLeague;

/// <summary>
/// Liga del paciente autenticado (LEAGUE v1): cohorte (estado o nacional por
/// la k-rule) con las 4 categorías (racha/evo/adh/clin). Privacidad por
/// diseño: opt-in default OFF, seudonimización server-side (la respuesta
/// nunca contiene id/nombre/ciudad de otro participante) y k-rule
/// (estado &lt;10 opt-in → nacional).
///
/// El <c>patientId</c> SIEMPRE llega resuelto de la identidad del JWT por la
/// capa API (nunca del body, anti-IDOR AC-11): sin perfil o sin inscripción
/// activa → 404 <c>NO_ACTIVE_ENROLLMENT</c>.
///
/// Cache: el cohorte se cachea por alcance (<c>league:{state|ALL}:v1</c>,
/// TTL 5 min) y NO contiene datos por-petición: el bloque <c>me</c> y los
/// flags <c>isMe</c> se resuelven AQUÍ, por request, contra el patientId
/// fresco del JWT. Logging sin PHI (solo alcance, tamaño y computedAt,
/// espejo de la regla §13.6 del motor de puntajes).
/// </summary>
public sealed record GetLeagueQuery(Guid PatientId) : IRequest<LeagueResponseDto>;

public sealed class GetLeagueQueryHandler(
    ILeagueRepository repository,
    ICacheService cache,
    ILogger<GetLeagueQueryHandler> logger) : IRequestHandler<GetLeagueQuery, LeagueResponseDto>
{
    public async Task<LeagueResponseDto> Handle(GetLeagueQuery request, CancellationToken ct)
    {
        var context = await repository.GetLeagueContextAsync(request.PatientId, ct)
            ?? throw new NotFoundException(
                $"NO_ACTIVE_ENROLLMENT: no existe una inscripción activa para el paciente {request.PatientId}.");

        var key = CacheKeys.League(context.StateCode);
        var cohort = await cache.GetOrCreateAsync(
            key,
            CacheKeys.LeagueTtl,
            async token =>
            {
                // k-rule (LEAGUE v1): el estado se usa solo si tiene suficientes
                // opt-in; si no, el cohorte cae a nacional (scope refleja cuál
                // se usó). Sin estado resoluble → SIEMPRE nacional (las filas
                // consultadas ya son nacionales; no se re-consulta). Los
                // valores se leen PERSISTIDOS únicamente: nunca se dispara
                // recálculo de puntajes por participante.
                var stateRows = await repository.GetLeagueCohortRowsAsync(context.StateCode, token);
                var scope =
                    context.StateCode is null
                        ? "national"
                        : LeagueCohortBuilder.ResolveScope(stateRows.Count);
                var rows =
                    scope == "national" && context.StateCode is not null
                        ? await repository.GetLeagueCohortRowsAsync(null, token)
                        : stateRows;
                return LeagueCohortBuilder.Build(rows, scope, context.StateCode, DateTime.UtcNow);
            },
            ct);

        // Log estructurado SIN PHI (espejo §13.6): solo métricas del cohorte.
        logger.LogInformation(
            "Program.League: scope={Scope} stateCode={StateCode} participants={Participants} computedAt={ComputedAt:O}",
            cohort.Scope, cohort.StateCode, cohort.Participants, cohort.ComputedAt);

        // Merge por-request (nunca cacheado): me + isMe desde el JWT.
        var me = new LeagueMeDto(context.OptedIn, context.Nickname);
        var categories = new LeagueCategoriesDto(
            MergeCategory(cohort.Categories.Racha, request.PatientId, context.OptedIn),
            MergeCategory(cohort.Categories.Evo, request.PatientId, context.OptedIn),
            MergeCategory(cohort.Categories.Adh, request.PatientId, context.OptedIn),
            MergeCategory(cohort.Categories.Clin, request.PatientId, context.OptedIn));

        return new LeagueResponseDto(
            new LeagueCohortDto(cohort.Scope, cohort.StateCode, cohort.Participants, cohort.ComputedAt),
            me,
            categories);
    }

    /// <summary>
    /// Recorta a top 10, marca isMe (comparación en memoria contra el
    /// patientId del JWT) y anexa la fila propia REAL (posición exacta)
    /// cuando el paciente está opt-in y fuera del top 10. Sin opt-in o sin
    /// valor en la categoría → myRank/myValue null, sin fila anexada.
    /// </summary>
    private static LeagueCategoryDto MergeCategory(
        LeagueCategoryCacheDto category,
        Guid myPatientId,
        bool optedIn
    )
    {
        var myEntry = category.Entries.FirstOrDefault(e => e.PatientId == myPatientId);
        var top = category.Entries
            .Take(LeagueCohortBuilder.TopEntries)
            .Select(e => new LeagueEntryDto(e.Position, e.Display, e.PatientId == myPatientId, e.Value))
            .ToList();

        int? myRank = null;
        int? myValue = null;
        LeagueEntryDto? appended = null;
        if (optedIn && myEntry is not null)
        {
            myRank = myEntry.Position;
            myValue = myEntry.Value;
            if (myEntry.Position > LeagueCohortBuilder.TopEntries)
            {
                appended = new LeagueEntryDto(myEntry.Position, myEntry.Display, true, myEntry.Value);
            }
        }

        return new LeagueCategoryDto(
            appended is null ? top : [.. top, appended],
            myRank,
            myValue,
            category.TotalParticipants);
    }
}