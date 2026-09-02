using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Pregunta perteneciente a una versión concreta de un instrumento. Lleva su
/// sección/subescala (para scoring por subescala), tipo, dirección de scoring
/// y orden. El <c>section</c> agrupa ítems (ej: sub-escalas de temperamento,
/// dominios del ORP) y alimenta los resultados de tipo <c>subscale</c>.
/// </summary>
public sealed class HealthTestQuestion
{
    public Guid Id { get; set; }

    public Guid VersionId { get; set; }

    /// <summary>Código estable dentro de la versión (ej: "temp_01").</summary>
    public string Code { get; set; } = default!;

    /// <summary>Sección o subescala a la que pertenece (null si no aplica).</summary>
    public string? Section { get; set; }

    public string Text { get; set; } = default!;

    public HealthTestQuestionType Type { get; set; } = HealthTestQuestionType.scale;

    public HealthTestScoringDirection ScoringDirection { get; set; } =
        HealthTestScoringDirection.positive;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    // Metadata del tipo num (biometría): unidad y rango válido del valor.
    public string? Unit { get; set; }

    public decimal? MinValue { get; set; }

    public decimal? MaxValue { get; set; }

    public decimal? DefaultValue { get; set; }

    // Etiquetas de extremos de la escala (min_lbl/max_lbl del render).
    public string? MinLabel { get; set; }

    public string? MaxLabel { get; set; }

    /// <summary>Instrucción/ayuda opcional bajo el texto de la pregunta (sub del render).</summary>
    public string? Hint { get; set; }

    // Navigation
    public HealthTestVersion? Version { get; set; }

    public ICollection<HealthTestAnswerOption> Options { get; set; } = [];
}
