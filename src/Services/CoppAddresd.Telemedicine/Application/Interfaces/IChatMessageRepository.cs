using CoppAddresd.Telemedicine.Domain.Entities;

namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>
/// Persistencia del chat de la consulta (F3). La lectura es incremental por
/// cursor <c>(created_at, id)</c> y la escritura es un append simple (sin
/// edición ni borrado).
/// </summary>
public interface IChatMessageRepository
{
    /// <summary>
    /// Mensajes de una cita posteriores al cursor, ordenados por
    /// <c>(created_at, id)</c> ascendente y limitados a <paramref name="limit"/>.
    /// Sin cursor devuelve los primeros mensajes de la consulta.
    /// </summary>
    Task<IReadOnlyList<ChatMessage>> ListAfterAsync(
        Guid appointmentId,
        DateTimeOffset? after,
        Guid? afterId,
        int limit,
        CancellationToken ct = default
    );

    /// <summary>Persiste un mensaje nuevo y lo devuelve (append-only).</summary>
    Task<ChatMessage> AddAsync(ChatMessage message, CancellationToken ct = default);
}
