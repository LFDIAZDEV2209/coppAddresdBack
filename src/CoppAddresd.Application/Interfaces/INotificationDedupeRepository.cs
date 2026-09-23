using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Dedupe persistente de notificaciones internas (F2): registra la
/// <c>dedupeKey</c> aportada por el emisor (microservicio de Telemedicina) en
/// <c>app.notification_dedupe_keys</c> junto con el último estado por canal.
/// El índice único de la tabla evita filas duplicadas; el estado pegajoso de
/// <c>sent</c> evita reenviar un canal ya entregado y permite reintentar los
/// que quedaron <c>failed | disabled | skipped</c>.
/// </summary>
public interface INotificationDedupeRepository
{
    /// <summary>Devuelve el registro de la clave (o null si nunca se emitió).</summary>
    Task<NotificationDedupeKey?> GetByKeyAsync(string dedupeKey, CancellationToken ct = default);

    /// <summary>
    /// Crea o actualiza el registro de la clave con el resultado de esta
    /// llamada. Un canal previamente <c>sent</c> nunca se degrada; el resto se
    /// actualiza con el estado más reciente (los null no pisan valores).
    /// </summary>
    Task UpsertAsync(
        string dedupeKey,
        Guid userId,
        string? pushStatus,
        string? smsStatus,
        CancellationToken ct = default);
}
