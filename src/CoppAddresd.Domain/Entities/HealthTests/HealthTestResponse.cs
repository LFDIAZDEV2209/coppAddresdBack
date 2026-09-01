namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Respuesta dada por el paciente a una pregunta de una evaluación. Para
/// escalas/selección apunta a la opción elegida (<c>AnswerOptionId</c>); para
/// preguntas abiertas guarda el texto en <c>ValueText</c>. Una evaluación
/// completada inmuniza sus respuestas (SPEC A8).
/// </summary>
public sealed class HealthTestResponse
{
    public Guid Id { get; set; }

    public Guid EvaluationId { get; set; }

    public Guid QuestionId { get; set; }

    /// <summary>Opción elegida (scale/single/multi); null en preguntas abiertas.</summary>
    public Guid? AnswerOptionId { get; set; }

    /// <summary>Texto libre (preguntas abiertas).</summary>
    public string? ValueText { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public HealthTestEvaluation? Evaluation { get; set; }

    public HealthTestQuestion? Question { get; set; }

    public HealthTestAnswerOption? AnswerOption { get; set; }
}
