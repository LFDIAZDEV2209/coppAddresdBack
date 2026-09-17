using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Repositorio del módulo de notificaciones de alertas de tests de salud
/// (plantillas, versiones, log de entregas y agregados para gráficos).
/// </summary>
public sealed class HealthTestNotificationRepository(AppDbContext dbContext)
    : IHealthTestNotificationRepository
{
    // ── Plantillas ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<HealthTestNotificationTemplate>> ListTemplatesAsync(
        NotificationChannel? channel,
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = BuildTemplateQuery(channel, search, isActive);

        return await query
            .OrderBy(t => t.NameEs)
            .Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100))
            .Take(Math.Clamp(pageSize, 1, 100))
            .Include(t => t.Versions)
            .ToListAsync(ct);
    }

    public async Task<int> CountTemplatesAsync(
        NotificationChannel? channel,
        string? search,
        bool? isActive,
        CancellationToken ct = default
    ) => await BuildTemplateQuery(channel, search, isActive).CountAsync(ct);

    private IQueryable<HealthTestNotificationTemplate> BuildTemplateQuery(
        NotificationChannel? channel,
        string? search,
        bool? isActive
    )
    {
        var query = dbContext.HealthTestNotificationTemplates.AsQueryable();

        if (channel is not null)
        {
            query = query.Where(t => t.Channel == channel);
        }

        if (isActive is not null)
        {
            query = query.Where(t => t.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(t =>
                EF.Functions.ILike(t.Code, term)
                || EF.Functions.ILike(t.NameEs, term)
                || (t.NameEn != null && EF.Functions.ILike(t.NameEn, term))
                || EF.Functions.ILike(t.BodyTemplateEs, term)
                || (t.BodyTemplateEn != null && EF.Functions.ILike(t.BodyTemplateEn, term))
            );
        }

        return query;
    }

    public async Task<HealthTestNotificationTemplate?> GetTemplateByIdAsync(
        Guid id,
        bool includeVersions = false,
        CancellationToken ct = default
    )
    {
        var query = dbContext.HealthTestNotificationTemplates.AsQueryable();
        if (includeVersions)
        {
            query = query.Include(t => t.Versions);
        }

        return await query.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<HealthTestNotificationTemplate?> GetTemplateByCodeAsync(
        string code,
        CancellationToken ct = default
    )
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await dbContext
            .HealthTestNotificationTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Code == normalized, ct);
    }

    public async Task AddTemplateAsync(
        HealthTestNotificationTemplate template,
        CancellationToken ct = default
    )
    {
        await dbContext.HealthTestNotificationTemplates.AddAsync(template, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateTemplateAsync(
        HealthTestNotificationTemplate template,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestNotificationTemplates.Update(template);
        await dbContext.SaveChangesAsync(ct);
    }

    // ── Versiones ─────────────────────────────────────────────────────────────

    public async Task AddTemplateVersionAsync(
        HealthTestNotificationTemplateVersion version,
        CancellationToken ct = default
    )
    {
        await dbContext.HealthTestNotificationTemplateVersions.AddAsync(version, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<HealthTestNotificationTemplateVersion>> ListTemplateVersionsAsync(
        Guid templateId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestNotificationTemplateVersions.AsNoTracking()
            .Where(v => v.TemplateId == templateId)
            .OrderByDescending(v => v.Version)
            .ToListAsync(ct);

    public async Task<HealthTestNotificationTemplateVersion?> GetTemplateVersionAsync(
        Guid templateId,
        int version,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestNotificationTemplateVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.TemplateId == templateId && v.Version == version, ct);

    public async Task<int> GetNextTemplateVersionNumberAsync(
        Guid templateId,
        CancellationToken ct = default
    )
    {
        var max = await dbContext
            .HealthTestNotificationTemplateVersions.AsNoTracking()
            .Where(v => v.TemplateId == templateId)
            .Select(v => (int?)v.Version)
            .MaxAsync(ct);

        return (max ?? 0) + 1;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetTemplateUsageCountsAsync(
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestNotifications.AsNoTracking()
            .Where(n => n.TemplateId != null)
            .GroupBy(n => n.TemplateId!.Value)
            .Select(g => new { TemplateId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TemplateId, x => x.Count, ct);

    // ── Log de notificaciones ─────────────────────────────────────────────────

    public async Task AddNotificationsAsync(
        IReadOnlyList<HealthTestNotification> notifications,
        CancellationToken ct = default
    )
    {
        if (notifications.Count == 0)
        {
            return;
        }

        await dbContext.HealthTestNotifications.AddRangeAsync(notifications, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<(
        IReadOnlyList<HealthTestNotification> Items,
        int Total
    )> ListNotificationsAsync(
        Guid? alertId,
        Guid? patientId,
        NotificationChannel? channel,
        NotificationStatus? status,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = dbContext
            .HealthTestNotifications.AsNoTracking()
            .Include(n => n.Patient)
            .Include(n => n.Template)
            .AsQueryable();

        if (alertId is not null)
        {
            query = query.Where(n => n.AlertId == alertId);
        }

        if (patientId is not null)
        {
            query = query.Where(n => n.PatientId == patientId);
        }

        if (channel is not null)
        {
            query = query.Where(n => n.Channel == channel);
        }

        if (status is not null)
        {
            query = query.Where(n => n.Status == status);
        }

        if (from is not null)
        {
            query = query.Where(n => n.CreatedAt >= from);
        }

        if (to is not null)
        {
            query = query.Where(n => n.CreatedAt <= to);
        }

        var total = await query.CountAsync(ct);
        var clampedSize = Math.Clamp(pageSize, 1, 100);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * clampedSize)
            .Take(clampedSize)
            .ToListAsync(ct);

        return (items, total);
    }

    // ── Apoyo para el envío ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<HealthTestAlert>> ListAlertsByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    )
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext
            .HealthTestAlerts.AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.Result)
            .Include(a => a.Rule)
            .Where(a => ids.Contains(a.Id))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, PatientProfile>> GetPatientsByIdsAsync(
        IReadOnlyCollection<Guid> patientIds,
        CancellationToken ct = default
    )
    {
        if (patientIds.Count == 0)
        {
            return new Dictionary<Guid, PatientProfile>();
        }

        return await dbContext
            .PatientProfiles.AsNoTracking()
            .Where(p => patientIds.Contains(p.Id) && p.DeletedAt == null)
            .ToDictionaryAsync(p => p.Id, ct);
    }

    // ── Gráficos ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyDictionary<string, int>> CountAlertsBySeverityAsync(
        Guid? professionalId,
        CancellationToken ct = default
    )
    {
        var scoped = await GetScopedPatientIdsAsync(professionalId, ct);
        var query = dbContext.HealthTestAlerts.AsNoTracking();
        if (scoped is not null)
        {
            query = query.Where(a => scoped.Contains(a.PatientId));
        }

        var rows = await query
            .GroupBy(a => a.Severity)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Key.ToString(), r => r.Count);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountAlertsByStatusAsync(
        Guid? professionalId,
        CancellationToken ct = default
    )
    {
        var scoped = await GetScopedPatientIdsAsync(professionalId, ct);
        var query = dbContext.HealthTestAlerts.AsNoTracking();
        if (scoped is not null)
        {
            query = query.Where(a => scoped.Contains(a.PatientId));
        }

        var rows = await query
            .GroupBy(a => a.Status)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Key.ToString(), r => r.Count);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountAlertsByIndicatorAsync(
        Guid? professionalId,
        int top,
        CancellationToken ct = default
    )
    {
        var scoped = await GetScopedPatientIdsAsync(professionalId, ct);
        var query = dbContext.HealthTestAlerts.AsNoTracking();
        if (scoped is not null)
        {
            query = query.Where(a => scoped.Contains(a.PatientId));
        }

        var rows = await query
            .GroupBy(a => a.Result != null ? a.Result.Label : "Sin indicador")
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(Math.Clamp(top, 1, 50))
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Key, r => r.Count);
    }

    public async Task<IReadOnlyList<(DateTime Date, int Count)>> CountAlertsByDayAsync(
        Guid? professionalId,
        int days,
        CancellationToken ct = default
    )
    {
        var from = DateTime.UtcNow.Date.AddDays(-(Math.Clamp(days, 1, 180) - 1));
        var scoped = await GetScopedPatientIdsAsync(professionalId, ct);
        var query = dbContext.HealthTestAlerts.AsNoTracking().Where(a => a.CreatedAt >= from);
        if (scoped is not null)
        {
            query = query.Where(a => scoped.Contains(a.PatientId));
        }

        var rows = await query
            .GroupBy(a => a.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return FillDays(rows.Select(r => (r.Date, r.Count)), days);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountNotificationsByChannelAsync(
        Guid? professionalId,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default
    )
    {
        var query = await BuildNotificationChartQueryAsync(professionalId, from, to, ct);
        var rows = await query
            .GroupBy(n => n.Channel)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Key.ToString(), r => r.Count);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountNotificationsByStatusAsync(
        Guid? professionalId,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default
    )
    {
        var query = await BuildNotificationChartQueryAsync(professionalId, from, to, ct);
        var rows = await query
            .GroupBy(n => n.Status)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Key.ToString(), r => r.Count);
    }

    public async Task<IReadOnlyList<(DateTime Date, int Count)>> CountNotificationsByDayAsync(
        Guid? professionalId,
        int days,
        CancellationToken ct = default
    )
    {
        var clamped = Math.Clamp(days, 1, 180);
        var from = DateTime.UtcNow.Date.AddDays(-(clamped - 1));
        var query = await BuildNotificationChartQueryAsync(professionalId, from, null, ct);
        var rows = await query
            .GroupBy(n => n.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return FillDays(rows.Select(r => (r.Date, r.Count)), clamped);
    }

    private async Task<IQueryable<HealthTestNotification>> BuildNotificationChartQueryAsync(
        Guid? professionalId,
        DateTime? from,
        DateTime? to,
        CancellationToken ct
    )
    {
        var scoped = await GetScopedPatientIdsAsync(professionalId, ct);
        var query = dbContext.HealthTestNotifications.AsNoTracking();
        if (scoped is not null)
        {
            query = query.Where(n => n.PatientId != null && scoped.Contains(n.PatientId!.Value));
        }

        if (from is not null)
        {
            query = query.Where(n => n.CreatedAt >= from);
        }

        if (to is not null)
        {
            query = query.Where(n => n.CreatedAt <= to);
        }

        return query;
    }

    private async Task<List<Guid>?> GetScopedPatientIdsAsync(
        Guid? professionalId,
        CancellationToken ct
    )
    {
        if (professionalId is null)
        {
            return null;
        }

        return await dbContext
            .PatientProfessionalAssignments.AsNoTracking()
            .Where(a => a.ProfessionalId == professionalId && a.Status == "Active")
            .Select(a => a.PatientId)
            .Distinct()
            .ToListAsync(ct);
    }

    private static IReadOnlyList<(DateTime Date, int Count)> FillDays(
        IEnumerable<(DateTime Date, int Count)> rows,
        int days
    )
    {
        var map = rows.ToDictionary(r => r.Date.Date, r => r.Count);
        var today = DateTime.UtcNow.Date;
        var result = new List<(DateTime, int)>(days);
        for (var i = days - 1; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            result.Add((date, map.TryGetValue(date, out var count) ? count : 0));
        }

        return result;
    }
}
