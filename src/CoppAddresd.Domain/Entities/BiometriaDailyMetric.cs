namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Fila pre-agregada de métricas biométricas clínicas del programa ANTARES.
/// Alimentada por BiometriaMetricsProcessorHostedService (CQRS Channel Pattern).
/// metric_key: imc_distribution | grasa_distribution | glucosa_distribution |
///             community_avg | city_patient_count
/// dimension_key: categoría OMS / ADA | sexo_categoría | ciudad_id | "imc" | "grasa" | "glucosa"
/// total_count: número de pacientes / registros en esa categoría o métrica.
/// total_value: suma acumulada del valor biométrico (para calcular promedio en O(1): total_value / total_count).
/// </summary>
public sealed class BiometriaDailyMetric
{
    public DateOnly MetricDate { get; set; }
    public string MetricKey { get; set; } = string.Empty;
    public string DimensionKey { get; set; } = string.Empty;
    public long TotalCount { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
