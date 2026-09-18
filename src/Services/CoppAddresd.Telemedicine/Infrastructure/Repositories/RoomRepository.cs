using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Telemedicine.Infrastructure.Repositories;

/// <summary>
/// Implementación EF del agregado de sala virtual + sesiones + registro de
/// webhooks. Sigue el mismo patrón que el repositorio de citas: carga sin
/// tracking para lectura, con tracking para mutación, y traducción de los
/// conflictos de concurrencia de PostgreSQL a errores de dominio.
/// </summary>
public sealed class RoomRepository(TelemedicineDbContext dbContext) : IRoomRepository
{
    public async Task<VirtualRoom?> GetByAppointmentIdAsync(
        Guid appointmentId,
        bool includeSessions = false,
        CancellationToken ct = default
    ) =>
        await Query(includeSessions).FirstOrDefaultAsync(r => r.AppointmentId == appointmentId, ct);

    public async Task<VirtualRoom?> GetByProviderRoomSidAsync(
        string providerRoomSid,
        bool includeSessions = false,
        CancellationToken ct = default
    ) =>
        await Query(includeSessions)
            .FirstOrDefaultAsync(r => r.ProviderRoomSid == providerRoomSid, ct);

    public async Task<IReadOnlyList<VirtualRoom>> ListByAppointmentIdsAsync(
        IReadOnlyCollection<Guid> appointmentIds,
        CancellationToken ct = default
    )
    {
        if (appointmentIds.Count == 0)
        {
            return [];
        }

        return await dbContext
            .Rooms.AsNoTracking()
            .Where(r => appointmentIds.Contains(r.AppointmentId))
            .ToListAsync(ct);
    }

    public async Task<VirtualRoom?> GetForUpdateAsync(
        Guid appointmentId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .Rooms.Include(r => r.Sessions)
            .FirstOrDefaultAsync(r => r.AppointmentId == appointmentId, ct);

    public async Task<VirtualRoom?> GetForUpdateByProviderRoomSidAsync(
        string providerRoomSid,
        CancellationToken ct = default
    ) =>
        await dbContext
            .Rooms.Include(r => r.Sessions)
            .FirstOrDefaultAsync(r => r.ProviderRoomSid == providerRoomSid, ct);

    public async Task<VirtualRoom> AddAsync(VirtualRoom room, CancellationToken ct = default)
    {
        dbContext.Rooms.Add(room);

        try
        {
            await dbContext.SaveChangesAsync(ct);
            return room;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // La sala es 1:1 con la cita: el único duplicado posible es la sala de
            // la misma cita creada por otra petición concurrente (join-token).
            // Devolver la existente hace la creación idempotente sin error.
            return await dbContext
                .Rooms.AsNoTracking()
                .Include(r => r.Sessions)
                .FirstAsync(r => r.AppointmentId == room.AppointmentId, ct);
        }
    }

    public async Task UpdateAsync(VirtualRoom room, CancellationToken ct = default)
    {
        // Las sesiones se agregan a la colección (EF las marca Added) y se
        // modifican en sus transiciones (Active → Ended): NUNCA se fuerzan a
        // Added, o un UPDATE de una sesión existente se convertiría en un INSERT
        // contra una fila existente (violación de PK).

        await SaveWithConflictTranslationAsync(ct);
    }

    public async Task AddWebhookEventAsync(
        TelemedicineWebhookEvent webhookEvent,
        CancellationToken ct = default
    )
    {
        dbContext.WebhookEvents.Add(webhookEvent);

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            throw new BusinessRuleViolationException("El webhook del proveedor ya fue procesado.");
        }
    }

    public async Task<(IReadOnlyList<TelemedicineSession> Items, int Total)> ListSessionsAsync(
        Guid? appointmentId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = dbContext.Sessions.AsNoTracking();

        if (appointmentId is not null)
            query = query.Where(s => s.AppointmentId == appointmentId);

        if (from is not null)
            query = query.Where(s => s.StartedAt >= from || s.CreatedAt >= from);

        if (to is not null)
            query = query.Where(s => s.StartedAt < to || s.CreatedAt < to);

        var total = await query.CountAsync(ct);

        var items = await query
            .Include(s => s.Appointment)
            .OrderByDescending(s => s.StartedAt ?? s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<int> CountActiveSessionsAsync(CancellationToken ct = default) =>
        await dbContext.Sessions.CountAsync(s => s.Status == TelemedicineSessionStatus.Active, ct);

    public async Task<int> CountActiveSessionsAsync(
        Guid professionalId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .Sessions.AsNoTracking()
            .CountAsync(
                s =>
                    s.Status == TelemedicineSessionStatus.Active
                    && s.Appointment != null
                    && s.Appointment.ProfessionalId == professionalId,
                ct
            );

    private IQueryable<VirtualRoom> Query(bool includeSessions)
    {
        var query = dbContext.Rooms.AsNoTracking();
        return includeSessions ? query.Include(r => r.Sessions) : query;
    }

    private async Task SaveWithConflictTranslationAsync(CancellationToken ct)
    {
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsExclusionViolation() || ex.IsUniqueViolation())
        {
            throw new BusinessRuleViolationException(
                "Conflicto de concurrencia al persistir la sala virtual."
            );
        }
    }
}
