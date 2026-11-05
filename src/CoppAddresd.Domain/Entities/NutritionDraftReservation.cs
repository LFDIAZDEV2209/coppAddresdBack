namespace CoppAddresd.Domain.Entities;

/// <summary>Reserva durable por batería; el ID también identifica el borrador para impedir duplicados tras un reinicio.</summary>
public sealed class NutritionDraftReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid BatteryAssignmentId { get; set; }
    public string State { get; set; } = "pending";
    public string? Reason { get; set; }
    public int Attempts { get; set; }
    public Guid? ClaimToken { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? PrescriptionId { get; set; }
    public bool Fallback { get; set; }
}
