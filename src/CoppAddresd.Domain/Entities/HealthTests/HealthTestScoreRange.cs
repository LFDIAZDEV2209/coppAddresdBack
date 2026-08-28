using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Rango de interpretación de un score para una versión: traduce un valor
/// numérico a una etiqueta legible y una severidad (ej: "alto"/critical).
/// Configuración por versión; los cambios solo afectan evaluaciones nuevas
/// (SPEC A9).
/// </summary>
public sealed class HealthTestScoreRange
{
    public Guid Id { get; set; }

    public Guid VersionId { get; set; }

    public decimal MinValue { get; set; }

    public decimal MaxValue { get; set; }

    /// <summary>Etiqueta legible (ej: "bajo", "moderado").</summary>
    public string Label { get; set; } = default!;

    public HealthTestSeverity Severity { get; set; } = HealthTestSeverity.low;

    public bool IsActive { get; set; } = true;

    // Navigation
    public HealthTestVersion? Version { get; set; }
}
