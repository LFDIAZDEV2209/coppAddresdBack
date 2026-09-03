using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.ListClinicalMetrics;

/// <summary>
/// Lista el catálogo de métricas clínicas activas (GET /catalogs/clinical-metrics,
/// ERP): qué se mide (peso, glucosa, presión...) con su unidad por defecto real.
/// Alimenta el selector del diálogo de línea base para que el POST use ids de BD
/// reales (el backend valida metricId/unitId contra app.measurement_metrics y
/// app.unit_of_measures).
/// </summary>
public sealed record ListClinicalMetricsQuery : IRequest<IReadOnlyList<ClinicalMetricDto>>;

public sealed class ListClinicalMetricsHandler(IProgramRepository programRepository)
    : IRequestHandler<ListClinicalMetricsQuery, IReadOnlyList<ClinicalMetricDto>>
{
    public async Task<IReadOnlyList<ClinicalMetricDto>> Handle(
        ListClinicalMetricsQuery request,
        CancellationToken ct)
    {
        var metrics = await programRepository.ListClinicalMetricsAsync(ct);

        // El DTO exige símbolo de unidad: una métrica sin unidad por defecto no
        // puede alimentar una línea base válida, se omite.
        return metrics
            .Where(m => m.DefaultUnit is not null)
            .Select(m => new ClinicalMetricDto(
                m.Id, m.Code, m.Name, m.DefaultUnitId, m.DefaultUnit!.Symbol))
            .ToList();
    }
}