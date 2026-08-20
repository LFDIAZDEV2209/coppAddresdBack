using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Domain.Entities;

/// <summary>
/// Sesión de video de una consulta. Representa una ventana de conexión concreta
/// (cuándo se abrió, cuándo terminó, duración). Separada de la cita y de la
/// sala: su ciclo de vida es independiente aunque relacionado.
/// </summary>
public sealed class TelemedicineSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AppointmentId { get; set; }

    public Guid RoomId { get; set; }

    public TelemedicineSessionStatus Status { get; set; } = TelemedicineSessionStatus.Created;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public long? DurationSeconds { get; set; }

    /// <summary>Usuario que finalizó la sesión (típicamente el profesional).</summary>
    public Guid? EndedBy { get; set; }

    public string? EndReason { get; set; }

    /// <summary>Momento del último evento del proveedor recibido (webhook).</summary>
    public DateTimeOffset? LastProviderEventAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public TelemedicineAppointment? Appointment { get; set; }

    public VirtualRoom? Room { get; set; }

    public ClinicalEncounter? Encounter { get; set; }
}
