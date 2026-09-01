namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Comentario de un profesional sobre una alerta o una evaluación (historial de
/// revisión manual). Permite el flujo de revisión cualitativa sin tocar los
/// resultados calculados (SPEC A12).
/// </summary>
public sealed class HealthTestComment
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    /// <summary>Evaluación comentada (opcional; si es nulo, comenta sobre el paciente/alertas).</summary>
    public Guid? EvaluationId { get; set; }

    /// <summary>Usuario (auth.users) autor del comentario.</summary>
    public Guid AuthorId { get; set; }

    public string Body { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PatientProfile? Patient { get; set; }

    public HealthTestEvaluation? Evaluation { get; set; }
}
