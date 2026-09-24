using CoppAddresd.Application.Features.ProgramProgress.DTOs.MetricsHistory;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetMetricsHistory;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Measurements.Queries.GetMyMetricsHistory;

/// <summary>
/// Historial de métricas clínicas del paciente SIN exigir inscripción activa
/// (endpoint abierto <c>GET /api/v1/me/metrics-history</c>, Fase 7, móvil).
/// Misma forma de respuesta que <c>GET /api/v1/program/me/metrics-history</c>
/// (<see cref="MetricsHistoryResponseDto"/>: serie diaria por métrica) y misma
/// proyección (<see cref="GetMetricsHistoryQueryHandler.BuildCachePayload"/> +
/// <see cref="GetMetricsHistoryQueryHandler.TrimToRequest"/>): solo cambia la
/// fuente del contexto (ventana UTC sin inscripción,
/// <c>GetOpenMetricsHistoryContextAsync</c>) y la validación de días es
/// estricta (fuera de rango → 400 en vez de clamp).
///
/// El <c>UserId</c> llega resuelto de la identidad del JWT por la capa API
/// (nunca del body, anti-IDOR): sin perfil → 404.
/// Caché por paciente (<c>my-metrics-history:{patientId}:v1</c>, TTL 5 min,
/// fail-open), clave SEPARADA del endpoint de programa (ventanas distintas:
/// UTC vs zona de la inscripción).
/// </summary>
public sealed record GetMyMetricsHistoryQuery(Guid UserId, IReadOnlyList<string> Codes, int Days)
    : IRequest<MetricsHistoryResponseDto>
{
    /// <summary>Días cuando el cliente no envía <c>days</c>.</summary>
    public const int DefaultDays = 180;

    /// <summary>Mínimo aceptado (menor → 400).</summary>
    public const int MinDays = 7;

    /// <summary>Máximo aceptado (mayor → 400).</summary>
    public const int MaxDays = 365;
}

public sealed class GetMyMetricsHistoryQueryHandler(
    IPatientRepository patients,
    IMetricsHistoryRepository repository,
    ICacheService cache,
    ILogger<GetMyMetricsHistoryQueryHandler> logger
) : IRequestHandler<GetMyMetricsHistoryQuery, MetricsHistoryResponseDto>
{
    public async Task<MetricsHistoryResponseDto> Handle(
        GetMyMetricsHistoryQuery request,
        CancellationToken ct
    )
    {
        // 400 en frontera: días fuera de [7, 365] (el endpoint de programa
        // clampea; el abierto rechaza — contrato de T1.4).
        var days = request.Days <= 0 ? GetMyMetricsHistoryQuery.DefaultDays : request.Days;
        if (days < GetMyMetricsHistoryQuery.MinDays || days > GetMyMetricsHistoryQuery.MaxDays)
        {
            throw new ValidationException([
                new ValidationFailure(
                    "days",
                    $"DAYS_INVALID: los días deben estar entre {GetMyMetricsHistoryQuery.MinDays} y {GetMyMetricsHistoryQuery.MaxDays}."
                ),
            ]);
        }

        // Identidad: paciente del JWT vía patient_profiles.user_id (anti-IDOR).
        var patient = await patients.GetByUserIdAsync(request.UserId, ct);
        if (patient is null)
        {
            throw new NotFoundException(
                "No existe un perfil de paciente para el usuario autenticado."
            );
        }

        var requested =
            request.Codes.Count == 0 ? GetMetricsHistoryQuery.DefaultCodes : request.Codes;
        // Higiene de códigos: sin duplicados ni case-variants (se emite el
        // Code del CATÁLOGO, nunca el string del request).
        var codes = requested.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Contexto ABIERTO (sin inscripción) cacheado por paciente; el recorte
        // por-request (whitelist → 400, filtro de días, fallback de IMC) ocurre
        // DESPUÉS del caché y nunca se cachea.
        var cachePayload = await cache.GetOrCreateAsync(
            CacheKeys.MyMetricsHistory(patient.Id),
            CacheKeys.MetricsHistoryTtl,
            async token =>
            {
                var context = await repository.GetOpenMetricsHistoryContextAsync(patient.Id, token);
                return GetMetricsHistoryQueryHandler.BuildCachePayload(context);
            },
            ct
        );

        // Whitelist contra el catálogo ACTIVO COMPLETO (cacheado): 400 en CADA
        // request, mismo formato METRICS_UNKNOWN que el endpoint de programa.
        var validCodes = cachePayload
            .Metrics.Select(m => m.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = codes.Where(c => !validCodes.Contains(c)).ToList();
        if (unknown.Count > 0)
        {
            var valid = string.Join(
                ",",
                cachePayload
                    .Metrics.Select(m => m.Code)
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            );
            throw new ValidationException([
                new ValidationFailure(
                    "codes",
                    $"METRICS_UNKNOWN: códigos inválidos: {string.Join(",", unknown)}. Códigos válidos: {valid}."
                ),
            ]);
        }

        var response = GetMetricsHistoryQueryHandler.TrimToRequest(cachePayload, codes, days);

        logger.LogInformation(
            "Me.MetricsHistory: métricas={Count} días={Days}",
            response.Metrics.Count,
            days
        );

        return response;
    }
}
