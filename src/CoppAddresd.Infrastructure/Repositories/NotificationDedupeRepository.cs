using CoppAddresd.Application.Features.Telemedicine;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Implementación EF del dedupe de notificaciones internas (F2) sobre
/// <c>app.notification_dedupe_keys</c>. La creación se apoya en el índice
/// único: una violación de unicidad (<c>23505</c>) significa que otra petición
/// creó la fila y se reintenta como update. El estado <c>sent</c> es pegajoso
/// (un canal ya entregado no se degrada) para que los reintentos no dupliquen.
/// </summary>
public sealed class NotificationDedupeRepository(AppDbContext dbContext)
    : INotificationDedupeRepository
{
    public async Task<NotificationDedupeKey?> GetByKeyAsync(
        string dedupeKey, CancellationToken ct = default)
        => await dbContext.NotificationDedupeKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DedupeKey == dedupeKey, ct);

    public async Task UpsertAsync(
        string dedupeKey,
        Guid userId,
        string? pushStatus,
        string? smsStatus,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entry = await dbContext.NotificationDedupeKeys
            .FirstOrDefaultAsync(x => x.DedupeKey == dedupeKey, ct);

        if (entry is null)
        {
            entry = new NotificationDedupeKey
            {
                Id = Guid.NewGuid(),
                DedupeKey = dedupeKey,
                UserId = userId,
                PushStatus = pushStatus,
                SmsStatus = smsStatus,
                CreatedAt = now,
            };

            dbContext.NotificationDedupeKeys.Add(entry);

            try
            {
                await dbContext.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Carrera: otra petición insertó la misma clave. La entidad
                // fallida sale del change tracker y se actualiza la fila real.
                dbContext.Entry(entry).State = EntityState.Detached;
                entry = await dbContext.NotificationDedupeKeys
                    .FirstOrDefaultAsync(x => x.DedupeKey == dedupeKey, ct);
            }
        }

        if (entry is null)
        {
            return;
        }

        if (entry.PushStatus != TelemedicineNotificationStatus.Sent && pushStatus is not null)
        {
            entry.PushStatus = pushStatus;
        }

        if (entry.SmsStatus != TelemedicineNotificationStatus.Sent && smsStatus is not null)
        {
            entry.SmsStatus = smsStatus;
        }

        entry.UpdatedAt = now;
        await dbContext.SaveChangesAsync(ct);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
