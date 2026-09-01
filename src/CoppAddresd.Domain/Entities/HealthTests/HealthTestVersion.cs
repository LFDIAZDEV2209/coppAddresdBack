using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Versión snapshot inmutable de un instrumento. Contiene sus propias
/// preguntas, opciones, rangos, estrategia de scoring y peso. Las evaluaciones
/// referencian SIEMPRE esta versión: modificar un instrumento publicado implica
/// clonar a <c>version_number + 1</c> y retirar la anterior, preservando los
/// históricos (SPEC A2).
/// </summary>
public sealed class HealthTestVersion
{
    public Guid Id { get; set; }

    public Guid InstrumentId { get; set; }

    /// <summary>Número de versión consecutivo por instrumento (1, 2, 3...).</summary>
    public int VersionNumber { get; set; }

    /// <summary>Nombre legible (ej: "PHQ-X v2").</summary>
    public string? Name { get; set; }

    public HealthTestVersionStatus Status { get; set; } = HealthTestVersionStatus.draft;

    /// <summary>Indica la versión por defecto para nuevas asignaciones.</summary>
    public bool IsCurrent { get; set; }

    public HealthTestScoringStrategy ScoringStrategy { get; set; } = HealthTestScoringStrategy.sum;

    /// <summary>Peso/metadata del test (replicado de la UX mobile, ej: 40/50).</summary>
    public int? Points { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PublishedAt { get; set; }

    public DateTime? RetiredAt { get; set; }

    // Navigation
    public HealthTestInstrument? Instrument { get; set; }

    public ICollection<HealthTestQuestion> Questions { get; set; } = [];

    public ICollection<HealthTestScoreRange> ScoreRanges { get; set; } = [];
}
