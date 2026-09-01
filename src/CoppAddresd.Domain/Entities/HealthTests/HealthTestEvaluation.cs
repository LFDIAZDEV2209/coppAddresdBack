using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Una ejecución concreta de una versión de test por un paciente. Apunta a la
/// versión (nunca al instrumento) para preservar el histórico. Al completarse
/// guarda el score total y porcentaje; las respuestas viven en
/// <see cref="HealthTestResponse"/> y los resultados calculados en
/// <see cref="HealthTestResult"/>.
/// </summary>
public sealed class HealthTestEvaluation
{
    public Guid Id { get; set; }

    public Guid AssignmentId { get; set; }

    public Guid PatientId { get; set; }

    public Guid VersionId { get; set; }

    public HealthTestEvaluationStatus Status { get; set; } = HealthTestEvaluationStatus.started;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public decimal? Score { get; set; }

    public decimal? ScorePercentage { get; set; }

    // Navigation
    public HealthTestAssignment? Assignment { get; set; }

    public PatientProfile? Patient { get; set; }

    public HealthTestVersion? Version { get; set; }

    public ICollection<HealthTestResponse> Responses { get; set; } = [];

    public ICollection<HealthTestResult> Results { get; set; } = [];
}
