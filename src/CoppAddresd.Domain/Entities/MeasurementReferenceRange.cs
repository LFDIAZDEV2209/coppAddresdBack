namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Rango de referencia de una métrica según edad y género (valores null =
/// aplica a todos). El <see cref="Priority"/> resuelve solapamientos: a mayor
/// valor, mayor precedencia.
/// </summary>
public sealed class MeasurementReferenceRange
{
    public Guid Id { get; set; }

    /// <summary>Métrica a la que pertenece el rango.</summary>
    public Guid MetricId { get; set; }

    /// <summary>Edad mínima (años) a la que aplica el rango. Null = todos.</summary>
    public int? AgeMin { get; set; }

    /// <summary>Edad máxima (años) a la que aplica el rango. Null = todos.</summary>
    public int? AgeMax { get; set; }

    /// <summary>Género al que aplica el rango (<c>M</c>/<c>F</c>/<c>X</c>). Null = todos.</summary>
    public string? Gender { get; set; }

    /// <summary>Límite inferior del rango normal. Null = sin límite inferior.</summary>
    public decimal? MinValue { get; set; }

    /// <summary>Límite superior del rango normal. Null = sin límite superior.</summary>
    public decimal? MaxValue { get; set; }

    /// <summary>Unidad en la que se expresan los límites del rango.</summary>
    public Guid UnitId { get; set; }

    /// <summary>Precedencia ante solapamientos: mayor gana. Default 0.</summary>
    public int Priority { get; set; }

    public string? Notes { get; set; }

    /// <summary>Indica si el rango está vigente.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Métrica a la que pertenece el rango.</summary>
    public MeasurementMetric? Metric { get; set; }

    /// <summary>Unidad de los límites del rango.</summary>
    public UnitOfMeasure? Unit { get; set; }
}