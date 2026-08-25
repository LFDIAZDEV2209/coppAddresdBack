using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Domain.Entities;

/// <summary>
/// Historial inmutable de reprogramaciones de una cita (append-only). La
/// aplicación de una reprogramación actualiza la hora de la cita, incrementa
/// <c>RescheduleCount</c> y registra aquí el cambio.
/// </summary>
public sealed class AppointmentReschedule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AppointmentId { get; set; }

    public RescheduleRequestedBy RequestedBy { get; set; }

    public Guid? RequestedByUserId { get; set; }

    public DateTimeOffset FromStart { get; set; }

    public DateTimeOffset ToStart { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset RescheduledAt { get; set; } = DateTimeOffset.UtcNow;

    public Appointment? Appointment { get; set; }
}
