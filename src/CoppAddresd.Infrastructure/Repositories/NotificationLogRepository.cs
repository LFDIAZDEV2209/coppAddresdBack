using CoppAddresd.Application.Features.ProgramProgress.DTOs.Notifications;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Implementación EF del log de notificaciones gamificadas (SPEC §20) sobre
/// <c>app.notifications</c>. Operaciones acotadas al paciente (anti-IDOR: todo
/// filtro lleva <c>patient_id</c>, nunca se confía en ids sin el dueño).
///
/// La inserción (<see cref="AddAsync"/>) comparte la unidad de trabajo del flujo
/// de otorgamiento que la invoca (mismo <c>AppDbContext</c> scoped): el registro
/// queda atómico con la XP; el servicio de notificación la captura con best-
/// effort y nunca propaga fallos a la transacción (SPEC §20, B — AC-42).
/// </summary>
public sealed class NotificationLogRepository(AppDbContext dbContext) : INotificationLogRepository
{
    public async Task<NotificationContext?> GetContextAsync(Guid patientId, CancellationToken ct)
    {
        // userId del perfil (auth.users) + timezone de la inscripción activa.
        // Dos lecturas set-based pequeñas (sin N+1); el contexto se usa una vez
        // por notificación.
        var profile = await dbContext.PatientProfiles.AsNoTracking()
            .Where(p => p.Id == patientId)
            .Select(p => (Guid?)p.UserId)
            .FirstOrDefaultAsync(ct);
        if (profile is null)
        {
            return null;
        }

        var timezone = await dbContext.ProgramEnrollments.AsNoTracking()
            .Where(e => e.PatientId == patientId && e.Status == ProgramEnrollmentStatus.Active)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => (string?)e.Timezone)
            .FirstOrDefaultAsync(ct);

        return new NotificationContext(profile, timezone);
    }

    public async Task<int> CountByTypeOnDayAsync(
        Guid patientId, string type, DateTime dayStartUtc, DateTime dayEndUtc, CancellationToken ct)
        => await dbContext.AppNotifications.AsNoTracking()
            .CountAsync(n => n.PatientId == patientId
                && n.Type == type
                && n.SentAt >= dayStartUtc && n.SentAt < dayEndUtc, ct);

    public async Task<int> CountOnDayAsync(
        Guid patientId, DateTime dayStartUtc, DateTime dayEndUtc, CancellationToken ct)
        => await dbContext.AppNotifications.AsNoTracking()
            .CountAsync(n => n.PatientId == patientId
                && n.SentAt >= dayStartUtc && n.SentAt < dayEndUtc, ct);

    public async Task AddAsync(AppNotification notification, CancellationToken ct)
    {
        dbContext.AppNotifications.Add(notification);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<PaginatedNotificationsResult> ListAsync(
        Guid patientId, int page, int pageSize, CancellationToken ct)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var total = await dbContext.AppNotifications.AsNoTracking()
            .CountAsync(n => n.PatientId == patientId, ct);

        var unreadCount = await dbContext.AppNotifications.AsNoTracking()
            .CountAsync(n => n.PatientId == patientId && n.ReadAt == null, ct);

        var items = await dbContext.AppNotifications.AsNoTracking()
            .Where(n => n.PatientId == patientId)
            .OrderByDescending(n => n.SentAt)
            .Select(n => new NotificationDto(
                n.Id,
                n.Type,
                n.Title,
                n.Message,
                n.Priority,
                n.Channel,
                n.SentAt,
                n.ReadAt))
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        return new PaginatedNotificationsResult(
            items, total, safePage, safePageSize,
            (int)Math.Ceiling(total / (double)safePageSize), unreadCount);
    }

    public async Task<bool> MarkReadAsync(Guid notificationId, Guid patientId, CancellationToken ct)
    {
        // Update dirigido (convención ExecuteUpdate del módulo): no carga la
        // entidad solo para cambiar read_at.
        var updated = await dbContext.AppNotifications
            .Where(n => n.Id == notificationId && n.PatientId == patientId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);

        return updated > 0;
    }
}