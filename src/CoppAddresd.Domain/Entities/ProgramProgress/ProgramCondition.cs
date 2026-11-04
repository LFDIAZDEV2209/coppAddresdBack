namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>Condición clínica o criterio de escala del catálogo; los criterios y sus fuentes son editables por datos.</summary>
public sealed class ProgramCondition
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEs { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Icd10Code { get; set; }
    /// <summary>Criterios combinables; las escalas de referencia declaran assignable=false.</summary>
    public string Criteria { get; set; } = "{}";
    public string SourceRef { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
