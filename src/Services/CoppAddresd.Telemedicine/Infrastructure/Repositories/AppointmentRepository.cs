using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Telemedicine.Infrastructure.Repositories;

/// <summary>
/// Implementación EF del agregado cita. Los conflictos de concurrencia del
/// agendamiento (exclusión de solapamiento / índice único parcial) llegan como
/// <see cref="DbUpdateException"/> que envuelve la excepción de PostgreSQL y se
/// traducen a <see cref="BusinessRuleViolationException"/> para un error amigable
/// (409).
/// </summary>
public sealed class AppointmentRepository(TelemedicineDbContext dbContext) : IAppointmentRepository
{
    public async Task<TelemedicineAppointment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.Appointments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<TelemedicineAppointment?> GetForUpdateAsync(Guid id, CancellationToken ct = default)
        => await dbContext.Appointments
            .Include(a => a.Cancellations)
            .Include(a => a.Reschedules)
            .Include(a => a.Request)
            .Include(a => a.Room)
                .ThenInclude(r => r!.Sessions)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<TelemedicineAppointment> AddAsync(
        TelemedicineAppointment appointment,
        CancellationToken ct = default)
    {
        dbContext.Appointments.Add(appointment);
        await SaveWithConflictTranslationAsync(ct);
        return appointment;
    }

    public async Task UpdateAsync(
        TelemedicineAppointment appointment,
        CancellationToken ct = default)
    {
        // Historial append-only (cancelaciones/reprogramaciones): los hijos ya
        // existentes cargados por GetForUpdateAsync están Unchanged; cualquier
        // otro estado es un hijo nuevo que EF marcó como Modified (no Added)
        // porque el agregado ya estaba tracked. Se fuerza Added para que se
        // INSERTE en lugar de intentar un UPDATE contra una fila inexistente.
        foreach (var cancellation in appointment.Cancellations)
        {
            if (dbContext.Entry(cancellation).State != EntityState.Unchanged)
            {
                dbContext.Entry(cancellation).State = EntityState.Added;
            }
        }

        foreach (var reschedule in appointment.Reschedules)
        {
            if (dbContext.Entry(reschedule).State != EntityState.Unchanged)
            {
                dbContext.Entry(reschedule).State = EntityState.Added;
            }
        }

        // NOTA: las sesiones NO se fuerzan a Added. A diferencia del historial
        // append-only (que siempre son hijos nuevos), una sesión existente se
        // MODIFICA en sus transiciones de estado (Active → Ended); forzarla a
        // Added intentaría un INSERT contra una fila existente (violación de PK).
        // Las sesiones nuevas se agregan a la colección y EF las marca Added.

        await SaveWithConflictTranslationAsync(ct);
    }

    public async Task<bool> HasActiveOverlapAsync(
        Guid professionalId,
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? excludeAppointmentId = null,
        CancellationToken ct = default)
        => await dbContext.Appointments.AnyAsync(a =>
            a.ProfessionalId == professionalId
            // Estados que ocupan el calendario (mismo criterio que el índice parcial).
            // Inline de la condición: EF no traduce helpers de método.
            && (a.Status == AppointmentStatus.Requested
                || a.Status == AppointmentStatus.Confirmed
                || a.Status == AppointmentStatus.InProgress)
            && a.ScheduledStart < end
            && a.ScheduledEnd > start
            && (excludeAppointmentId == null || a.Id != excludeAppointmentId), ct);

    public async Task<IReadOnlyList<TelemedicineAppointment>> ListByProfessionalAsync(
        Guid professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
        => await dbContext.Appointments.AsNoTracking()
            .Where(a => a.ProfessionalId == professionalId
                        && a.ScheduledStart >= from
                        && a.ScheduledStart < to)
            .OrderBy(a => a.ScheduledStart)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TelemedicineAppointment>> ListByPatientAsync(
        Guid patientId,
        CancellationToken ct = default)
        => await dbContext.Appointments.AsNoTracking()
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.ScheduledStart)
            .ToListAsync(ct);

    private async Task SaveWithConflictTranslationAsync(CancellationToken ct)
    {
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleViolationException(
                "La cita cambió de estado en otra operación concurrente; reintenta la operación.");
        }
        catch (DbUpdateException ex) when (ex.IsExclusionViolation() || ex.IsUniqueViolation())
        {
            throw new BusinessRuleViolationException(
                "El profesional ya tiene una cita que se solapa con el horario solicitado, o la solicitud ya fue confirmada.");
        }
    }
}
