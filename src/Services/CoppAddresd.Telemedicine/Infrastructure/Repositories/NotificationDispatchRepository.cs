using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Telemedicine.Infrastructure.Repositories;

/// <summary>
/// Implementación EF del registro de despachos (F2). El índice único
/// <c>(appointment_id, kind)</c> resuelve la carrera entre réplicas: el perdedor
/// de <see cref="TryAddAsync"/> recibe <c>false</c> y no reenvía.
/// </summary>
public sealed class NotificationDispatchRepository(TelemedicineDbContext dbContext)
    : INotificationDispatchRepository
{
    public async Task<bool> ExistsAsync(
        Guid appointmentId,
        NotificationDispatchKind kind,
        CancellationToken ct = default
    ) =>
        await dbContext
            .NotificationDispatches.AsNoTracking()
            .AnyAsync(d => d.AppointmentId == appointmentId && d.Kind == kind, ct);

    public async Task<bool> TryAddAsync(
        NotificationDispatch dispatch,
        CancellationToken ct = default
    )
    {
        dbContext.NotificationDispatches.Add(dispatch);

        try
        {
            await dbContext.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Otra instancia registró el mismo (appointment_id, kind) primero.
            dbContext.Entry(dispatch).State = EntityState.Detached;
            return false;
        }
    }
}
