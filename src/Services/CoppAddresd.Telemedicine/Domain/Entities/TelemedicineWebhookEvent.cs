namespace CoppAddresd.Telemedicine.Domain.Entities;

/// <summary>
/// Registro de un webhook del proveedor de video ya procesado. Es la clave de
/// idempotencia: un mismo evento (misma combinación <c>event_type + room_sid +
/// participant_sid</c>) se procesa una sola vez, aunque el proveedor reintente
/// o duplique el envío. El índice único en BD serializa los duplicados
/// concurrentes y el payload se conserva con fines de auditoría.
/// </summary>
public sealed class TelemedicineWebhookEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tipo de evento del proveedor (p. ej. "room-ended", "participant-connected").</summary>
    public string EventType { get; set; } = default!;

    /// <summary>Sid de la sala en el proveedor (contexto del evento).</summary>
    public string RoomSid { get; set; } = default!;

    /// <summary>Sid del participante (vacío para eventos de sala, p. ej. room-ended).</summary>
    public string ParticipantSid { get; set; } = string.Empty;

    /// <summary>Payload crudo del evento (solo metadatos del proveedor, sin PHI clínica).</summary>
    public string? PayloadJson { get; set; }

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
