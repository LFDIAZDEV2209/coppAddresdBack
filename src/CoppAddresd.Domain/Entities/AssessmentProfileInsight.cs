namespace CoppAddresd.Domain.Entities;

public sealed class AssessmentProfileInsight
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid RequestedBy { get; set; }
    public string SourceRevision { get; set; } = "";
    public string Language { get; set; } = "es";
    public string ContentJson { get; set; } = "{}";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
