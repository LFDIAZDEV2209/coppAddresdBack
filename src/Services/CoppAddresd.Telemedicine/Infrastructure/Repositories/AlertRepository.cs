using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Telemedicine.Infrastructure.Repositories;

/// <summary>
/// Implementación EF de la bandeja de alertas. Orden estable para la bandeja
/// (no leídas primero, luego fecha descendente) y updates dirigidos con
/// <c>ExecuteUpdateAsync</c> para marcar leídas sin cargar entidades.
/// </summary>
public sealed class AlertRepository(TelemedicineDbContext dbContext) : IAlertRepository
{
    public async Task AddRangeAsync(
        IReadOnlyList<TelemedicineAlert> alerts,
        CancellationToken ct = default)
    {
        dbContext.Alerts.AddRange(alerts);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<TelemedicineAlert> Items, int Total)> ListForUserAsync(
        Guid recipientUserId,
        bool unreadOnly,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = dbContext.Alerts.AsNoTracking()
            .Where(a => a.RecipientUserId == recipientUserId);

        if (unreadOnly)
        {
            query = query.Where(a => a.ReadAt == null);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(a => a.ReadAt == null ? 0 : 1)
            .ThenByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<int> CountUnreadAsync(Guid recipientUserId, CancellationToken ct = default)
        => await dbContext.Alerts.AsNoTracking()
            .CountAsync(a => a.RecipientUserId == recipientUserId && a.ReadAt == null, ct);

    public async Task<(IReadOnlyList<TelemedicineAlert> Items, int Total)> ListAllAsync(
        bool unreadOnly,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = dbContext.Alerts.AsNoTracking();

        if (unreadOnly)
        {
            query = query.Where(a => a.ReadAt == null);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(a => a.ReadAt == null ? 0 : 1)
            .ThenByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<bool> MarkReadByIdAsync(Guid alertId, CancellationToken ct = default)
        => await dbContext.Alerts
            .Where(a => a.Id == alertId && a.ReadAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(a => a.ReadAt, DateTimeOffset.UtcNow),
                ct) > 0;

    public async Task<int> MarkAllReadGlobalAsync(CancellationToken ct = default)
        => await dbContext.Alerts
            .Where(a => a.ReadAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(a => a.ReadAt, DateTimeOffset.UtcNow),
                ct);

    public async Task<int> CountUnreadGlobalAsync(CancellationToken ct = default)
        => await dbContext.Alerts.AsNoTracking()
            .CountAsync(a => a.ReadAt == null, ct);

    public async Task<bool> MarkReadAsync(Guid alertId, Guid recipientUserId, CancellationToken ct = default)
        => await dbContext.Alerts
            .Where(a => a.Id == alertId
                        && a.RecipientUserId == recipientUserId
                        && a.ReadAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(a => a.ReadAt, DateTimeOffset.UtcNow),
                ct) > 0;

    public async Task<int> MarkAllReadAsync(Guid recipientUserId, CancellationToken ct = default)
        => await dbContext.Alerts
            .Where(a => a.RecipientUserId == recipientUserId && a.ReadAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(a => a.ReadAt, DateTimeOffset.UtcNow),
                ct);
}
