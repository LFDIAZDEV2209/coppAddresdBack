using MediatR;

namespace CoppAddresd.Application.Features.Measurements.Queries.GetMyMeasurements;

/// <summary>
/// Lectura self-service de mediciones clínicas propias (Fase 7, móvil).
/// El <c>UserId</c> llega resuelto de la identidad del JWT por la capa API
/// (nunca del body, anti-IDOR): el handler lo mapea a
/// <c>app.patient_profiles.user_id</c> y nunca acepta un patientId del cliente.
/// <c>PageSize</c> por defecto 20, rango 1-100. <c>Cursor</c> es opaco
/// (lo emite el repositorio). <c>MetricCodes</c> es un filtro opcional por
/// códigos canónicos del catálogo (case-insensitive).
/// </summary>
public sealed record GetMyMeasurementsQuery(
    Guid UserId,
    int PageSize = GetMyMeasurementsQuery.DefaultPageSize,
    string? Cursor = null,
    string[]? MetricCodes = null
) : IRequest<CursorPagedResult<MeasurementItemDto>>
{
    /// <summary>Tamaño de página cuando el cliente no lo envía.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Mínimo aceptado (menor → 400).</summary>
    public const int MinPageSize = 1;

    /// <summary>Máximo aceptado (mayor → 400, skill pagination).</summary>
    public const int MaxPageSize = 100;
}
