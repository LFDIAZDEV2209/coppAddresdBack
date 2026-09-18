using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Domain.Entities;

/// <summary>
/// Registro de despacho de una notificación por cita (F2). Es la memoria de
/// deduplicación del microservicio: el índice único <c>(appointment_id, kind)</c>
/// garantiza que un recordatorio se envíe UNA sola vez, aunque el barrido corra
/// en varias réplicas o repita ticks. La entrega real (push/SMS) la hace el
/// backend; esta tabla solo registra qué se notificó y cuándo.
/// </summary>
public sealed class NotificationDispatch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Cita notificada (referencia débil dentro del agregado).</summary>
    public Guid AppointmentId { get; set; }

    /// <summary>Tipo de notificación; parte de la clave de deduplicación.</summary>
    public NotificationDispatchKind Kind { get; set; }

    /// <summary>Instante UTC en que el backend aceptó el envío.</summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
