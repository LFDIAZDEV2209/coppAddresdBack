namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Opción de respuesta de una pregunta. Lleva el <c>score_value</c> (el scoring
/// es configuración, no código) y los campos <c>depends_on_*</c> para soportar
/// en el futuro preguntas condicionales/saltos (SPEC A6) sin migración.
/// </summary>
public sealed class HealthTestAnswerOption
{
    public Guid Id { get; set; }

    public Guid QuestionId { get; set; }

    public string Text { get; set; } = default!;

    /// <summary>Valor de scoring de elegir esta opción (configuración, SPEC A9).</summary>
    public decimal? ScoreValue { get; set; }

    /// <summary>Opción previa que condiciona mostrar esta (preguntas condicionales, futuro).</summary>
    public Guid? DependsOnQuestionId { get; set; }

    /// <summary>Opción previa que condiciona mostrar esta (preguntas condicionales, futuro).</summary>
    public Guid? DependsOnOptionId { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public HealthTestQuestion? Question { get; set; }
}
