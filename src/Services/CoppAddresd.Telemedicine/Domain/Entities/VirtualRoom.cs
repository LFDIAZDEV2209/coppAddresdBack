using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Domain.Entities;

/// <summary>
/// Sala virtual creada en el proveedor de video (Twilio en la primera
/// implementación). <c>Provider</c>/<c>ProviderRoomSid</c>/<c>ProviderRoomName</c>
/// aíslan el detalle del proveedor: el dominio solo conoce el concepto de sala.
/// Una cita tiene a lo sumo una sala.
/// </summary>
public sealed class VirtualRoom
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AppointmentId { get; set; }

    /// <summary>Identificador del proveedor ("twilio", futuros...).</summary>
    public string Provider { get; set; } = "twilio";

    /// <summary>Sid de la sala en el proveedor (respuesta de Twilio Rooms).</summary>
    public string ProviderRoomSid { get; set; } = default!;

    /// <summary>Nombre único determinista de la sala (p. ej. "apt-{appointmentId}").</summary>
    public string ProviderRoomName { get; set; } = default!;

    public VirtualRoomStatus Status { get; set; } = VirtualRoomStatus.Created;

    /// <summary>Inicio de la ventana en que los participantes pueden entrar.</summary>
    public DateTimeOffset ScheduledOpenAt { get; set; }

    /// <summary>Fin de la ventana: la sala expira y no se emiten más tokens.</summary>
    public DateTimeOffset ScheduledCloseAt { get; set; }

    public int MaxParticipants { get; set; } = 2;

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Appointment? Appointment { get; set; }

    public ICollection<TelemedicineSession> Sessions { get; set; } = [];
}
