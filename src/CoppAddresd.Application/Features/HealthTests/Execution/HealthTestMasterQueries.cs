using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using MediatR;

namespace CoppAddresd.Application.Features.HealthTests.Execution;

/// <summary>Identidad del paciente para la fila de la tabla maestra (ERP).</summary>
public record MasterPatientIdentityDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? DocumentNumber,
    string? Gender,
    DateTime? DateOfBirth,
    string? ClinicName,
    string? InsurerName,
    string? ProfessionalName,
    string Status
)
{
    public static MasterPatientIdentityDto FromEntity(
        PatientProfile p,
        string? clinicName,
        string? insurerName,
        string? professionalName
    ) =>
        new(
            p.Id,
            p.FirstName,
            p.LastName,
            p.DocumentNumber,
            p.Gender,
            p.DateOfBirth,
            clinicName,
            insurerName,
            professionalName,
            p.Status
        );
}

/// <summary>Resumen por test del paciente para la tabla maestra (estado y último score).</summary>
public record MasterPatientResultDto(
    Guid VersionId,
    string? TestCode,
    string? TestName,
    string? TestCategory,
    string State,
    decimal? Score,
    decimal? ScorePercentage,
    string? Qualifier,
    string? Severity,
    DateTime? CompletedAt
);

/// <summary>Fila completa de la tabla maestra: identidad + resumen por test + alertas activas.</summary>
public record MasterPatientRowDto(
    MasterPatientIdentityDto Patient,
    IReadOnlyList<MasterPatientResultDto> Results,
    int AlertCount
);

/// <summary>
/// Consulta las filas de la tabla maestra del módulo en una sola pasada
/// (sin N+1): pacientes con asignaciones, resumen por test y conteo de
/// alertas activas. Con <c>ProfessionalId</c> filtra por el alcance del
/// profesional (ViewOwn); con <c>StateCodes</c>/<c>CityId</c> acota la zona
/// (unión de estados; ciudad con precedencia).
/// </summary>
public record GetMasterRowsQuery(
    Guid? ProfessionalId = null,
    IReadOnlyList<string>? StateCodes = null,
    Guid? CityId = null
) : IRequest<IReadOnlyList<MasterPatientRowDto>>;

public sealed class GetMasterRowsQueryHandler(IHealthTestRepository repository, ICacheService cache)
    : IRequestHandler<GetMasterRowsQuery, IReadOnlyList<MasterPatientRowDto>>
{
    public async Task<IReadOnlyList<MasterPatientRowDto>> Handle(
        GetMasterRowsQuery request,
        CancellationToken ct
    )
    {
        // Normaliza y ordena los estados para que el hash de caché sea estable
        // sin importar el orden de selección en el mapa.
        var stateCodes = (request.StateCodes ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
        var hasGeoFilter = request.CityId.HasValue || stateCodes.Count > 0;
        var scopeHash = hasGeoFilter
            ? CacheKeys.HashScope(
                request.ProfessionalId?.ToString() ?? "global",
                stateCodes.Count > 0 ? string.Join(",", stateCodes) : null,
                request.CityId?.ToString()
            )
            : CacheKeys.HashScope(request.ProfessionalId?.ToString() ?? "global");
        var cacheKey = CacheKeys.Stats("health-master", scopeHash);
        return await cache.GetOrCreateAsync(
            cacheKey,
            CacheKeys.StatsTtl(),
            async token =>
            {
                IReadOnlyCollection<Guid>? geoPatientIds = null;
                if (hasGeoFilter)
                {
                    geoPatientIds = await repository.GetPatientIdsByGeoAsync(
                        stateCodes,
                        request.CityId,
                        token
                    );
                    if (geoPatientIds.Count == 0)
                    {
                        return new List<MasterPatientRowDto>();
                    }
                }

                var assignments = await repository.ListAssignmentsWithPatientDataAsync(
                    request.ProfessionalId,
                    geoPatientIds,
                    token
                );
                var alertCounts = await repository.ListActiveAlertCountsByPatientAsync(token);
                var professionalNames = await repository.ListProfessionalNamesByPatientAsync(token);
                var clinicNames = await repository.ListClinicNamesByIdsAsync(
                    assignments
                        .Select(a => a.Patient)
                        .Where(p => p is not null && p.ClinicId.HasValue)
                        .Select(p => p!.ClinicId!.Value)
                        .Distinct(),
                    token
                );
                var insurerNames = await repository.ListInsurerNamesByIdsAsync(
                    assignments
                        .Select(a => a.Patient)
                        .Where(p => p is not null && p.InsurerId.HasValue)
                        .Select(p => p!.InsurerId!.Value)
                        .Distinct(),
                    token
                );

                var rows = new List<MasterPatientRowDto>();
                foreach (var group in assignments.GroupBy(a => a.PatientId))
                {
                    var patient = group.First().Patient!;
                    var results = new List<MasterPatientResultDto>();

                    foreach (
                        var versionGroup in group.GroupBy(a => a.VersionId).OrderBy(g => g.Key)
                    )
                    {
                        var version = versionGroup.First().Version;
                        var evaluations = versionGroup.SelectMany(a => a.Evaluations).ToList();

                        var completed = evaluations
                            .Where(e => e.Status == HealthTestEvaluationStatus.completed)
                            .OrderByDescending(e => e.CompletedAt)
                            .FirstOrDefault();
                        var started = evaluations.FirstOrDefault(e =>
                            e.Status == HealthTestEvaluationStatus.started
                        );
                        var hasStartedAssignment = versionGroup.Any(a =>
                            a.Status == HealthTestAssignmentStatus.in_progress
                        );

                        string state;
                        decimal? score = null;
                        decimal? pct = null;
                        string? qualifier = null;
                        string? severity = null;
                        DateTime? completedAt = null;

                        if (completed is not null)
                        {
                            state = "completado";
                            var scoreResult = completed.Results.FirstOrDefault(r =>
                                r.ResultType == HealthTestResultType.score
                            );
                            score = scoreResult?.Value;
                            pct = completed.ScorePercentage;
                            qualifier = scoreResult?.Qualifier;
                            severity = scoreResult?.Severity?.ToString();
                            completedAt = completed.CompletedAt;
                        }
                        else if (started is not null || hasStartedAssignment)
                        {
                            state = "en-progreso";
                        }
                        else
                        {
                            state = "pendiente";
                        }

                        results.Add(
                            new MasterPatientResultDto(
                                versionGroup.Key,
                                version?.Instrument?.Code,
                                version?.Instrument?.Name ?? version?.Name,
                                version?.Instrument?.Category,
                                state,
                                score,
                                pct,
                                qualifier,
                                severity,
                                completedAt
                            )
                        );
                    }

                    rows.Add(
                        new MasterPatientRowDto(
                            MasterPatientIdentityDto.FromEntity(
                                patient,
                                patient.ClinicId.HasValue
                                    ? clinicNames.GetValueOrDefault(patient.ClinicId.Value)
                                    : null,
                                patient.InsurerId.HasValue
                                    ? insurerNames.GetValueOrDefault(patient.InsurerId.Value)
                                    : null,
                                professionalNames.GetValueOrDefault(patient.Id)
                            ),
                            results,
                            alertCounts.GetValueOrDefault(patient.Id)
                        )
                    );
                }

                return rows;
            },
            ct
        );
    }
}
