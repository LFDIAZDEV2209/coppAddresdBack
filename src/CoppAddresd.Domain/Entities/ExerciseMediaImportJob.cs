namespace CoppAddresd.Domain.Entities;

/// <summary>Importación durable, sin datos clínicos; lease recuperable tras reinicio.</summary>
public sealed class ExerciseMediaImportJob
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int SourceId { get; set; }
    public string Kind { get; set; } = "Video";
    public string Status { get; set; } = "queued";
    public int Attempts { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public Guid? MediaId { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
