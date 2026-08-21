using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Telemedicine.Infrastructure.Repositories;

/// <summary>
/// Implementación EF del encuentro clínico. La creación es idempotente: si dos
/// guardados concurrentes intentan crear el encuentro de la misma cita, el que
/// pierde la carrera contra el índice único se desvincula (el intento fallido
/// queda en estado <c>Added</c>) y devuelve el existente TRACKEADO para que el
/// handler aplique sus cambios sin error.
/// </summary>
public sealed class EncounterRepository(TelemedicineDbContext dbContext) : IEncounterRepository
{
    public async Task<ClinicalEncounter?> GetByAppointmentIdAsync(
        Guid appointmentId,
        CancellationToken ct = default)
        => await dbContext.Encounters.AsNoTracking()
            .FirstOrDefaultAsync(e => e.AppointmentId == appointmentId, ct);

    public async Task<ClinicalEncounter?> GetForUpdateByAppointmentIdAsync(
        Guid appointmentId,
        CancellationToken ct = default)
        => await dbContext.Encounters
            .FirstOrDefaultAsync(e => e.AppointmentId == appointmentId, ct);

    public async Task<ClinicalEncounter> AddAsync(
        ClinicalEncounter encounter,
        CancellationToken ct = default)
    {
        dbContext.Encounters.Add(encounter);

        try
        {
            await SaveWithAuditContextAsync(ct);
            return encounter;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // El encuentro es 1:1 con la cita: el único duplicado posible es el
            // encuentro de la misma cita creado por un guardado concurrente.
            // Se desvincula el intento fallido (tras un SaveChanges fallido EF
            // no resetea los estados) y se devuelve el existente TRACKEADO.
            dbContext.Entry(encounter).State = EntityState.Detached;
            return await dbContext.Encounters
                .FirstAsync(e => e.AppointmentId == encounter.AppointmentId, ct);
        }
    }

    public async Task UpdateAsync(ClinicalEncounter encounter, CancellationToken ct = default)
        => await SaveWithAuditContextAsync(ct);

    /// <summary>
    /// Transacción explícita corta (patrón <c>CreateExecutionStrategy</c> del
    /// proyecto): habilita la propagación del actor del JWT a los GUC
    /// <c>audit.*</c> (el interceptor dispara en BEGIN), que el trigger del
    /// registro clínico lee para atribuir quién modificó la PHI. Un
    /// <c>SaveChanges</c> de una sola sentencia no abre transacción y la
    /// auditoría quedaría con actor <c>SYSTEM</c>.
    /// </summary>
    private async Task SaveWithAuditContextAsync(CancellationToken ct)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }
}
