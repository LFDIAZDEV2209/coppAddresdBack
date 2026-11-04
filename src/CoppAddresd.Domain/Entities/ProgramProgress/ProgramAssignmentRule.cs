namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>Regla determinista de asignación identificada por código estable para desempate y trazabilidad.</summary>
public sealed class ProgramAssignmentRule
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    /// <summary>Condición principal opcional; las reglas de subescalas no necesitan una condición asignable.</summary>
    public string? ConditionCode { get; set; }
    public string Criteria { get; set; } = "{}";
    /// <summary>Referencias al catálogo por nombre, nunca por identificadores fijos entre entornos.</summary>
    public string Target { get; set; } = "{}";
    public int Priority { get; set; }
    public decimal Weight { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
