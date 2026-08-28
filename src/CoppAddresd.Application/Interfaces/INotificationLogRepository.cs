using CoppAddresd.Application.Features.ProgramProgress.DTOs.Notifications;
using CoppAddresd.Domain.Entities.ProgramProgress;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Contexto de entrega de una notificación gamificada (SPEC §20, B): el
/// <c>userId</c> (<c>auth.users</c>) resuelve los tokens FCM del paciente y la
/// <c>timezone</c> (de su inscripción activa) fija el día local y el horario de
/// silencio. Null cuando el paciente no tiene perfil.
/// </summary>
public sealed record NotificationContext(Guid? UserId, string? Timezone);

/// <summary>
/// Repositorio del log de notificaciones gamificadas del programa (SPEC §20):
/// persistencia y conteos anti-spam sobre <c>app.notifications</c>. Agregado
/// separado del repositorio de la inscripción (<c>IProgramRepository</c>) —
/// mismo patrón que <c>IDeviceTokenRepository</c> — porque lo consumen tanto el
/// servicio de notificación de la capa de aplicación (inserción + conteos, best-
/// effort dentro de los flujos de otorgamiento) como los endpoints del centro
/// de notificaciones del móvil (listado + marcar leída).
/// </summary>
public interface INotificationLogRepository
{
    /// <summary>
    /// Resuelve el contexto de entrega del paciente (userId del perfil para los
    /// tokens FCM + timezone de su inscripción activa para el día local y el
    /// horario de silencio), o null si el paciente no tiene perfil.
    /// </summary>
    Task<NotificationContext?> GetContextAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Conteo de notificaciones del mismo <c>type</c> del paciente en la ventana
    /// del día local [dayStartUtc, dayEndUtc): anti-spam "máx. 2 por tipo por
    /// día" (SPEC §20, B).
    /// </summary>
    Task<int> CountByTypeOnDayAsync(
        Guid patientId, string type, DateTime dayStartUtc, DateTime dayEndUtc, CancellationToken ct = default);

    /// <summary>
    /// Conteo TOTAL de notificaciones del paciente en la ventana del día local
    /// [dayStartUtc, dayEndUtc): anti-spam "máx. 6 por día" (SPEC §20, B).
    /// </summary>
    Task<int> CountOnDayAsync(
        Guid patientId, DateTime dayStartUtc, DateTime dayEndUtc, CancellationToken ct = default);

    /// <summary>
    /// Persiste el log de la notificación. Se invoca DENTRO de la transacción
    /// del flujo de otorgamiento (misma unidad de trabajo): el registro queda
    /// atómico con la XP. Un fallo aquí nunca propaga (el servicio lo captura,
    /// SPEC §20, B — AC-42).
    /// </summary>
    Task AddAsync(AppNotification notification, CancellationToken ct = default);

    /// <summary>
    /// Listado paginado del centro de notificaciones del paciente (SPEC §20, D):
    /// orden descendente por <c>sent_at</c>, con el total de filas y el
    /// <c>unreadCount</c> para el badge del móvil.
    /// </summary>
    Task<PaginatedNotificationsResult> ListAsync(
        Guid patientId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Marca una notificación del paciente como leída (<c>read_at = now</c>).
    /// Devuelve false si la notificación no existe o no pertenece al paciente
    /// (anti-IDOR, AC-11: el llamador devuelve 404 sin distinguir existencia).
    /// </summary>
    Task<bool> MarkReadAsync(Guid notificationId, Guid patientId, CancellationToken ct = default);
}