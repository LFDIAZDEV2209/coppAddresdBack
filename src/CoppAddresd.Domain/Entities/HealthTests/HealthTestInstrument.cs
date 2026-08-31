namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Instrumento de evaluación (test) a nivel lógico. Es reutilizable entre
/// baterías y no contiene preguntas propias: cada pregunta/scoring/interpretación
/// vive en una <see cref="HealthTestVersion"/> snapshot inmutable, de modo que
/// los resultados históricos de una versión no cambian cuando se publica otra.
/// </summary>
public sealed class HealthTestInstrument
{
    public Guid Id { get; set; }

    /// <summary>Código único de negocio (ej: "temp", "orp").</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>Categoría funcional (ej: "psicologico", "nutricion", "clinico").</summary>
    public string? Category { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<HealthTestVersion> Versions { get; set; } = [];
}
