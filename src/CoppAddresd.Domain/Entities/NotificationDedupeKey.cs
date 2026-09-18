namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Registro de deduplicación de notificaciones internas (F2, schema
/// <c>app</c>): una fila por <c>dedupeKey</c> registrada por
/// <c>POST /api/v1/internal/telemedicine/notifications</c>.
///
/// Guarda el último estado por canal (<c>sent | skipped | failed | disabled</c>)
/// para dos fines:
/// <list type="bullet">
/// <item>Idempotencia: un canal ya <c>sent</c> nunca se reenvía (índice único
/// por clave + estado pegajoso de <c>sent</c>).</item>
/// <item>Reintentos: un canal que quedó <c>failed</c>/<c>disabled</c>/<c>skipped</c>
/// puede reintentarse con la misma clave (el micro reintenta el dispatch) sin
/// duplicar los canales ya entregados.</item>
/// </list>
/// No contiene PHI: clave opaca (compuesta por el emisor), usuario y estados.
/// </summary>
public sealed class NotificationDedupeKey
{
    public Guid Id { get; set; }

    /// <summary>Clave opaca aportada por el emisor (ej. <c>appointment:{id}:reminder_1h:patient</c>).</summary>
    public string DedupeKey { get; set; } = default!;

    /// <summary>Usuario (<c>auth.users</c>) destinatario de la notificación.</summary>
    public Guid UserId { get; set; }

    /// <summary>Último estado del canal push para esta clave (null = nunca procesado).</summary>
    public string? PushStatus { get; set; }

    /// <summary>Último estado del canal SMS para esta clave (null = nunca procesado).</summary>
    public string? SmsStatus { get; set; }

    /// <summary>Instante UTC en que se registró la clave.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Instante UTC de la última actualización de estado.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
