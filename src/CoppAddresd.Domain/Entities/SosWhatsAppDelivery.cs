namespace CoppAddresd.Domain.Entities;

/// <summary>Outbox por alerta y teléfono congelado. No contiene credenciales.</summary>
public sealed class SosWhatsAppDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AlertId { get; set; }
    public string Destination { get; set; } = "";
    public string PatientName { get; set; } = "";
    public int ContactOrder { get; set; }
    public string State { get; set; } = "Pending";
    public string? ParametersJson { get; set; }
    public string? Wamid { get; set; }
    public string? ErrorCode { get; set; }
    public int Attempts { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int DeliveryRank { get; set; }
    public string? DeliveryStatus { get; set; }
}
