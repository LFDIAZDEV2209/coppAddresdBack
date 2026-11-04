namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>Propuesta clínica reproducible con snapshot del perfil y autoría de su ciclo de vida.</summary>
public sealed class ProgramAssignmentRecommendation
{
    public Guid Id { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid PatientId { get; set; }
    /// <summary>Snapshot inmutable del perfil evaluado: resultados, subescalas, biometría y fuentes (D-03).</summary>
    public string ProfileSnapshot { get; set; } = "{}";
    /// <summary>Contenido propuesto y matchedRules con códigos, criterios y fuentes que justifican cada decisión.</summary>
    public string Proposal { get; set; } = "{\"matchedRules\":[]}";
    public string? NarrativeEs { get; set; }
    public string? NarrativeEn { get; set; }
    public string State { get; set; } = "proposed";
    public string EngineVersion { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public Guid? AppliedBy { get; set; }
    public Guid? DismissedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
    public DateTime? DismissedAt { get; set; }
    public ProgramEnrollment? Enrollment { get; set; }
}
