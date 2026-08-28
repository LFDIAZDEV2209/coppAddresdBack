using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Domain.Entities;

/// <summary>
/// Historial inmutable de cancelaciones de una cita (append-only). El estado
/// vigente (razón/quien) vive en <see cref="Appointment"/>; esta
/// tabla conserva la traza completa para auditoría.
/// </summary>
public sealed class AppointmentCancellation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AppointmentId { get; set; }

    public CancelledBy CancelledBy { get; set; }

    /// <summary>Usuario autenticado que ejecutó la cancelación (<c>auth.users</c>).</summary>
    public Guid? CancelledByUserId { get; set; }

    public string Reason { get; set; } = default!;

    public DateTimeOffset CancelledAt { get; set; } = DateTimeOffset.UtcNow;

    public Appointment? Appointment { get; set; }
}
