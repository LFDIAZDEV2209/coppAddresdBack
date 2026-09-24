using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace CoppAddresd.Application.Features.Measurements.Queries.GetMyMeasurements;

/// <summary>
/// Resuelve el paciente por JWT y valida la petición antes de delegar la
/// consulta SQL al repositorio (la paginación con cursor vive en
/// <see cref="IPatientMeasurementRepository"/>, siguiente tarea).
/// Orden: 1) <c>PageSize</c> 1-100 (falla rápido sin I/O → 400),
/// 2) perfil por <c>patient_profiles.user_id</c> (sin perfil → 404, sin
/// revelar otros perfiles: solo se consulta por el <c>UserId</c> del JWT),
/// 3) <c>MetricCodes</c> contra el catálogo ACTIVO (desconocido → 400 con la
/// lista de válidos, precedente metrics-history).
/// </summary>
public sealed class GetMyMeasurementsQueryHandler(
    IPatientRepository patients,
    IClinicalMeasurementRepository catalog,
    IPatientMeasurementRepository measurements
) : IRequestHandler<GetMyMeasurementsQuery, CursorPagedResult<MeasurementItemDto>>
{
    public async Task<CursorPagedResult<MeasurementItemDto>> Handle(
        GetMyMeasurementsQuery request,
        CancellationToken ct
    )
    {
        // 1) Frontera: PageSize fuera de rango → 400 sin tocar la BD.
        if (
            request.PageSize < GetMyMeasurementsQuery.MinPageSize
            || request.PageSize > GetMyMeasurementsQuery.MaxPageSize
        )
        {
            throw new ValidationException([
                new ValidationFailure(
                    "pageSize",
                    $"PAGESIZE_INVALID: el tamaño de página debe estar entre {GetMyMeasurementsQuery.MinPageSize} y {GetMyMeasurementsQuery.MaxPageSize}."
                ),
            ]);
        }

        // 2) Identidad: paciente del JWT vía patient_profiles.user_id.
        // Solo se busca por el UserId autenticado (anti-IDOR): un usuario sin
        // perfil recibe 404, nunca una lista ajena ni un 403 que confirme
        // la existencia de otros perfiles.
        var patient = await patients.GetByUserIdAsync(request.UserId, ct);
        if (patient is null)
        {
            throw new NotFoundException(
                "No existe un perfil de paciente para el usuario autenticado."
            );
        }

        // 3) Filtro opcional: normaliza (trim, sin vacíos, sin duplicados
        // case-insensitive) y valida contra el catálogo ACTIVO completo.
        string[]? normalizedCodes = NormalizeCodes(request.MetricCodes);
        if (normalizedCodes is { Length: > 0 })
        {
            var active = await catalog.GetActiveMetricsWithUnitsAsync(ct);
            var validCodes = active.Select(m => m.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unknown = normalizedCodes.Where(c => !validCodes.Contains(c)).ToList();
            if (unknown.Count > 0)
            {
                var valid = string.Join(
                    ",",
                    active.Select(m => m.Code).OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                );
                throw new ValidationException([
                    new ValidationFailure(
                        "metricCodes",
                        $"METRICS_UNKNOWN: códigos inválidos: {string.Join(",", unknown)}. Códigos válidos: {valid}."
                    ),
                ]);
            }
        }

        // 4) Delega la consulta SQL (orden, cursor opaco y proyección mínima)
        // al repositorio; el handler no compone IQueryable ni toca EF.
        return await measurements.GetPagedAsync(
            patient.Id,
            normalizedCodes,
            request.PageSize,
            request.Cursor,
            ct
        );
    }

    /// <summary>
    /// Normaliza el filtro de códigos: null/vacío → null (sin filtro);
    /// recorta espacios, descarta vacíos y deduplica case-insensitive.
    /// Devuelve null si no queda ningún código útil.
    /// </summary>
    private static string[]? NormalizeCodes(string[]? codes)
    {
        if (codes is null || codes.Length == 0)
        {
            return null;
        }

        var normalized = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? null : normalized;
    }
}
