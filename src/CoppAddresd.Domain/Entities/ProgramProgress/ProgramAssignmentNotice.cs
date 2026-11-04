namespace CoppAddresd.Domain.Entities.ProgramProgress;

/// <summary>Aviso bilingüe por destinatario: inbox ERP y entrega paciente con dedupe transaccional.</summary>
public sealed class ProgramAssignmentNotice
{
    public Guid Id { get; set; }
    public Guid RecommendationId { get; set; }
    public Guid UserId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string TitleEs { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string MessageEs { get; set; } = string.Empty;
    public string MessageEn { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public bool PushCompleted { get; set; }
    public bool ChatCompleted { get; set; }
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? LeaseUntil { get; set; }
}
