using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Telemedicine.Infrastructure.Repositories;

/// <summary>
/// Implementación EF de las adendas del encuentro (F4). Append-only: solo
/// inserta; la lectura es cronológica por <c>(created_at, id)</c>.
/// </summary>
public sealed class EncounterAddendumRepository(TelemedicineDbContext dbContext)
    : IEncounterAddendumRepository
{
    public async Task<IReadOnlyList<EncounterAddendum>> ListByEncounterAsync(
        Guid encounterId,
        CancellationToken ct = default)
        => await dbContext.EncounterAddenda.AsNoTracking()
            .Where(a => a.EncounterId == encounterId)
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);

    public async Task<EncounterAddendum> AddAsync(
        EncounterAddendum addendum,
        CancellationToken ct = default)
    {
        dbContext.EncounterAddenda.Add(addendum);

        // La adenda es PHI: transacción explícita corta para que el trigger de
        // auditoría reciba el actor del JWT (ver EncounterRepository).
        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });

        return addendum;
    }
}
