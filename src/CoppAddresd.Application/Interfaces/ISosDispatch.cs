using System.Threading.Channels;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Cola en memoria (Channel delimitado, patrón CQRS del repo) para despachar
/// canales SOS sin bloquear la respuesta al paciente. El outbox durable son
/// las filas en <c>app.notification_dedupe_keys</c> creadas en la transacción
/// de activación: si el proceso muere antes de encolar, el sweep de arranque
/// del procesador re-encola las pendientes (self-healing).
/// </summary>
public interface ISosDispatchQueue
{
    ValueTask EnqueueAsync(Guid alertId, CancellationToken ct = default);

    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct = default);
}

/// <summary>Despacha el canal SMS de la alerta (plantilla server-side + Twilio).</summary>
public interface ISosSmsDispatcher
{
    /// <summary>
    /// Procesa el canal SMS: respeta la fila de dedupe <c>sos:sms:{alertId}</c>
    /// (un canal <c>Enviado</c> jamás se reenvía), aplica timeout 5 s con hasta
    /// 2 reintentos con backoff, y actualiza el estado del canal en la alerta.
    /// Nunca lanza: devuelve el estado final del canal.
    /// </summary>
    Task<SosChannelStatus> DispatchAsync(SosAlert alert, CancellationToken ct = default);
}

/// <summary>Despacha el canal push FCM al staff asignado (D4).</summary>
public interface ISosPushDispatcher
{
    /// <summary>
    /// Procesa el push hacia los profesionales asignados con tokens vigentes,
    /// con dedupe durable <c>sos:push:{alertId}:{userId}</c>. Purga un token
    /// SOLO cuando Firebase responde inequívocamente Unregistered (D4); ante
    /// fallos transitorios los tokens se preservan. Nunca lanza.
    /// </summary>
    Task<SosPushDispatchResult> DispatchAsync(SosAlert alert, CancellationToken ct = default);
}

/// <summary>Resultado agregado del despacho push de una alerta.</summary>
public sealed record SosPushDispatchResult(SosChannelStatus Status, int Recipients, string? Detail);
