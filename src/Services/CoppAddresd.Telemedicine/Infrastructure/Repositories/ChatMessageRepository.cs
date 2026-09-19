using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Telemedicine.Infrastructure.Repositories;

/// <summary>
/// Implementación EF del chat de la consulta (F3). Lectura incremental por
/// cursor keyset <c>(created_at, id)</c> (sin offset) y append simple.
/// </summary>
public sealed class ChatMessageRepository(TelemedicineDbContext dbContext) : IChatMessageRepository
{
    public async Task<IReadOnlyList<ChatMessage>> ListAfterAsync(
        Guid appointmentId,
        DateTimeOffset? after,
        Guid? afterId,
        int limit,
        CancellationToken ct = default
    )
    {
        var query = dbContext
            .ChatMessages.AsNoTracking()
            .Where(m => m.AppointmentId == appointmentId);

        if (after is { } cursor)
        {
            // Keyset sobre (created_at, id): el id desempata mensajes con el
            // mismo instante (la precisión de Postgres es de microsegundos).
            var cursorUtc = cursor.UtcDateTime;
            query = afterId is { } cursorId
                ? query.Where(m =>
                    m.CreatedAt > cursorUtc
                    || (m.CreatedAt == cursorUtc && m.Id.CompareTo(cursorId) > 0))
                : query.Where(m => m.CreatedAt > cursorUtc);
        }

        return await query
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<ChatMessage> AddAsync(ChatMessage message, CancellationToken ct = default)
    {
        dbContext.ChatMessages.Add(message);
        await dbContext.SaveChangesAsync(ct);
        return message;
    }
}
