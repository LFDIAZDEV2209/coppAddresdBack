using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Implementación EF del repositorio de alertas SOS.
/// </summary>
public sealed class SosAlertRepository(AppDbContext dbContext) : ISosAlertRepository
{
    public async Task<SosAlert> AddAsync(SosAlert alert, CancellationToken ct = default)
    {
        dbContext.SosAlerts.Add(alert);
        await dbContext.SaveChangesAsync(ct);
        return alert;
    }

    public async Task UpdateAsync(SosAlert alert, CancellationToken ct = default)
    {
        dbContext.SosAlerts.Update(alert);
        await dbContext.SaveChangesAsync(ct);
    }
}
