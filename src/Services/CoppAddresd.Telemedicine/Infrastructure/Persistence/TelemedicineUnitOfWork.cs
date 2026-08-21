using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Telemedicine.Infrastructure.Persistence;

/// <summary>
/// Implementación de <see cref="ITelemedicineUnitOfWork"/> sobre el DbContext del
/// microservicio. La transacción es corta y predecible: solo operaciones de BD
/// dentro de la acción (skill <c>transactions</c> del proyecto).
/// Con <c>EnableRetryOnFailure</c> (NpgsqlRetryingExecutionStrategy), las
/// transacciones manuales DEBEN ejecutarse vía <c>CreateExecutionStrategy()</c>
/// (patrón documentado del proyecto); la estrategia reintenta los fallos
/// transitorios re-ejecutando toda la acción, por lo que esta debe ser idempotente.
/// </summary>
public sealed class TelemedicineUnitOfWork(TelemedicineDbContext dbContext) : ITelemedicineUnitOfWork
{
    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                await action(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }
}
