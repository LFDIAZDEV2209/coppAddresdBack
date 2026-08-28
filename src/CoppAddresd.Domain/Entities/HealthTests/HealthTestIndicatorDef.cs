namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Definición configurable de un indicador derivado. La fórmula vive en
/// <c>Computation</c> (jsonb): <c>{"formula":"weighted|sum|avg",
/// "sources":[{"resultType":"subscale","code":"...","weight":0.3}]}</c>.
/// Crear/editar indicadores es DML, no código (SPEC A11).
/// </summary>
public sealed class HealthTestIndicatorDef
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>Fórmula y fuentes en jsonb (ver doc de clase).</summary>
    public string Computation { get; set; } = default!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
