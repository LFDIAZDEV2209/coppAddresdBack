using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>
/// Persistencia de los despachos de notificación por cita (F2). La clave única
/// <c>(appointment_id, kind)</c> es la garantía de "una sola vez": el barrido
/// consulta antes de enviar y registra después de que el backend acepte.
/// </summary>
public interface INotificationDispatchRepository
{
    /// <summary>¿Ya se despachó este tipo de notificación para la cita?</summary>
    Task<bool> ExistsAsync(
        Guid appointmentId,
        NotificationDispatchKind kind,
        CancellationToken ct = default
    );

    /// <summary>
    /// Registra el despacho. Devuelve <c>false</c> si otra instancia ya lo
    /// registró (el perdedor de la carrera no reenvía ni falla).
    /// </summary>
    Task<bool> TryAddAsync(NotificationDispatch dispatch, CancellationToken ct = default);
}
