using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Regla configurable de alerta clínica. La condición vive en <c>Condition</c>
/// (jsonb): <c>{"when":{"resultType":"score","code":"orp","severity":["alto","critico"]}}</c>.
/// Al completar una evaluación, el motor evalúa las reglas activas contra los
/// resultados y genera <see cref="HealthTestAlert"/> sin duplicados (SPEC A12).
/// </summary>
public sealed class HealthTestAlertRule
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>Condición en jsonb (ver doc de clase).</summary>
    public string Condition { get; set; } = default!;

    public HealthTestSeverity Severity { get; set; } = HealthTestSeverity.high;

    /// <summary>Plantilla de mensaje con placeholders (ej: "ORP {value} - {label}").</summary>
    public string? MessageTemplate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
