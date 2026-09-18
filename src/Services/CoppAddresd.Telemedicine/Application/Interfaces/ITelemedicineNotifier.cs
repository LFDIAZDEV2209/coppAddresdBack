namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>
/// Canal de entrega soportado por el contrato de notificaciones del backend
/// (F2). El shape HTTP usa los nombres en PascalCase ("Push"/"Sms").
/// </summary>
public enum TelemedicineNotificationChannel
{
    Push,
    Sms,
}

/// <summary>
/// Notificación saliente hacia el backend, dueño de la entrega (push/SMS).
/// Telemedicina decide QUÉ notificar (evento, destinatario, contenido y datos
/// de navegación); el backend decide CÓMO entregarlo. <see cref="DedupeKey"/>
/// es la clave idempotente para la entrega del backend.
/// </summary>
public sealed record TelemedicineNotification(
    Guid UserId,
    string Title,
    string Body,
    IReadOnlyList<TelemedicineNotificationChannel> Channels,
    IReadOnlyDictionary<string, string>? Data = null,
    string? DedupeKey = null
);

/// <summary>
/// Cliente de notificaciones del backend (contrato F2:
/// <c>POST {Backend:BaseUrl}/api/v1/internal/telemedicine/notifications</c> con
/// header <c>X-Internal-Key</c> y status por canal). Best-effort por diseño:
/// un fallo se registra y devuelve <c>false</c>; NUNCA debe romper el flujo de
/// negocio que originó la notificación.
/// </summary>
public interface ITelemedicineNotifier
{
    /// <summary>
    /// Envía la notificación. Devuelve <c>true</c> si el backend la aceptó (2xx);
    /// <c>false</c> ante rechazo o fallo transitorio (para reintentar el tick
    /// siguiente, cuando aplique).
    /// </summary>
    Task<bool> SendAsync(
        TelemedicineNotification notification,
        CancellationToken ct = default
    );
}
