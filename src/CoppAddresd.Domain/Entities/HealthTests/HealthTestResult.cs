using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Resultado calculado de una evaluación: score total, score por subescala o
/// indicador derivado. Snapshot inmutable persistido al completar la evaluación
/// (append-only): el histórico de una evaluación NO se re-computa si las reglas
/// o rangos cambian después (SPEC A11).
/// </summary>
public sealed class HealthTestResult
{
    public Guid Id { get; set; }

    public Guid EvaluationId { get; set; }

    public HealthTestResultType ResultType { get; set; }

    /// <summary>Código estable del resultado (ej: "temp_sanguineo", "iapnea").</summary>
    public string Code { get; set; } = default!;

    public string Label { get; set; } = default!;

    public decimal Value { get; set; }

    /// <summary>Calificador cualitativo del valor (ej: "bajo", "dominante").</summary>
    public string? Qualifier { get; set; }

    public HealthTestSeverity? Severity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public HealthTestEvaluation? Evaluation { get; set; }
}
