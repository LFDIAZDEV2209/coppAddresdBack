namespace CoppAddresd.Domain.Entities;

/// <summary>Bitácora de cada decisión con actor y snapshot de la versión afectada.</summary>
public sealed class NutritionPrescriptionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PrescriptionId { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = "";
    public int Revision { get; set; }
    public string? Comment { get; set; }
    public int CriticalCount { get; set; }
    public bool CriticalConfirmed { get; set; }
    public bool AutoApproved { get; set; }
    public string SnapshotJson { get; set; } = "{}";
}
