namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Unidad de medida del catálogo de mediciones clínicas (mg/dL, kg, cm, mmHg...).
/// </summary>
public sealed class UnitOfMeasure
{
    public Guid Id { get; set; }

    /// <summary>Código único de la unidad (ej. <c>mg_dl</c>, <c>kg</c>).</summary>
    public string Code { get; set; } = default!;

    /// <summary>Nombre legible de la unidad (ej. "Miligramos por decilitro").</summary>
    public string Name { get; set; } = default!;

    /// <summary>Símbolo abreviado (ej. <c>mg/dL</c>, <c>kg</c>).</summary>
    public string Symbol { get; set; } = default!;

    /// <summary>Indica si la unidad está disponible para nuevas mediciones.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Métricas que usan esta unidad como unidad por defecto.</summary>
    public ICollection<MeasurementMetric> Metrics { get; set; } = [];

    /// <summary>Rangos de referencia expresados en esta unidad.</summary>
    public ICollection<MeasurementReferenceRange> ReferenceRanges { get; set; } = [];

    /// <summary>Mediciones registradas en esta unidad.</summary>
    public ICollection<ClinicalMeasurement> Measurements { get; set; } = [];
}