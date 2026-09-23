using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Telemedicine.Infrastructure.Repositories;

/// <summary>
/// Implementación EF de la pre-consulta del paciente (F4). La creación es
/// idempotente (1:1 con la cita): si dos autoguardados concurrentes intentan
/// crear la fila, el que pierde la carrera contra el índice único se
/// desvincula y devuelve la existente TRACKEADA para que el handler la
/// actualice.
/// </summary>
public sealed class PreVisitIntakeRepository(TelemedicineDbContext dbContext)
    : IPreVisitIntakeRepository
{
    public async Task<PreVisitIntake?> GetByAppointmentIdAsync(
        Guid appointmentId,
        CancellationToken ct = default)
        => await dbContext.PreVisitIntakes.AsNoTracking()
            .FirstOrDefaultAsync(i => i.AppointmentId == appointmentId, ct);

    public async Task<PreVisitIntake?> GetForUpdateByAppointmentIdAsync(
        Guid appointmentId,
        CancellationToken ct = default)
        => await dbContext.PreVisitIntakes
            .FirstOrDefaultAsync(i => i.AppointmentId == appointmentId, ct);

    public async Task<PreVisitIntake> AddAsync(
        PreVisitIntake intake,
        CancellationToken ct = default)
    {
        dbContext.PreVisitIntakes.Add(intake);

        try
        {
            await SaveWithAuditContextAsync(ct);
            return intake;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // La pre-consulta es 1:1 con la cita: el único duplicado posible es
            // la fila creada por un autoguardado concurrente. Tras un
            // SaveChanges fallido EF no resetea estados: se desvincula el
            // intento fallido y se devuelve la existente TRACKEADA.
            dbContext.Entry(intake).State = EntityState.Detached;
            return await dbContext.PreVisitIntakes
                .FirstAsync(i => i.AppointmentId == intake.AppointmentId, ct);
        }
    }

    public async Task UpdateAsync(PreVisitIntake intake, CancellationToken ct = default)
        => await SaveWithAuditContextAsync(ct);

    /// <summary>
    /// Transacción explícita corta (patrón <c>CreateExecutionStrategy</c> del
    /// proyecto): la pre-consulta es PHI y el trigger de auditoría lee el actor
    /// de los GUC <c>audit.*</c>, que el interceptor propaga al iniciar la
    /// transacción. Un <c>SaveChanges</c> de una sentencia no abre transacción
    /// y la auditoría quedaría con actor <c>SYSTEM</c>.
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
