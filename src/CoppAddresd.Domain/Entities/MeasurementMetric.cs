namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Métrica clínica del catálogo: qué se mide (glucosa en ayunas, peso, presión
/// sistólica...) con su unidad por defecto y categoría.
/// </summary>
public sealed class MeasurementMetric
{
    public Guid Id { get; set; }

    /// <summary>Código único de la métrica (ej. <c>glucose_fasting</c>).</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>Unidad por defecto en la que se expresa la métrica.</summary>
    public Guid DefaultUnitId { get; set; }

    /// <summary>
    /// Categoría de la métrica: <c>vital</c> | <c>metabolic</c> | <c>body_comp</c>
    /// | <c>test_score</c> (futuro, cuando se conecten los tests).
    /// </summary>
    public string Category { get; set; } = default!;

    /// <summary>Indica si la métrica está disponible para nuevas mediciones.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Unidad por defecto de la métrica.</summary>
    public UnitOfMeasure? DefaultUnit { get; set; }

    /// <summary>Rangos de referencia definidos para esta métrica.</summary>
    public ICollection<MeasurementReferenceRange> ReferenceRanges { get; set; } = [];

    /// <summary>Mediciones registradas con esta métrica.</summary>
    public ICollection<ClinicalMeasurement> Measurements { get; set; } = [];
}