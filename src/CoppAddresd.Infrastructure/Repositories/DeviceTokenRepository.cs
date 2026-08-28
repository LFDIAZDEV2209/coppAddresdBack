using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class DeviceTokenRepository(AppDbContext dbContext) : IDeviceTokenRepository
{
    public async Task<DeviceToken?> GetByUserAndTokenAsync(Guid userId, string token, CancellationToken ct = default)
        => await dbContext.DeviceTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Token == token, ct);

    public async Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken ct = default)
        => await dbContext.DeviceTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Token == token, ct);

    public async Task<IReadOnlyList<DeviceToken>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await dbContext.DeviceTokens
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

    public async Task AddAsync(DeviceToken deviceToken, CancellationToken ct = default)
    {
        dbContext.DeviceTokens.Add(deviceToken);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(DeviceToken deviceToken, CancellationToken ct = default)
    {
        dbContext.DeviceTokens.Update(deviceToken);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(DeviceToken deviceToken, CancellationToken ct = default)
    {
        dbContext.DeviceTokens.Remove(deviceToken);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteByTokenAsync(string token, CancellationToken ct = default)
    {
        // Se elimina por el change tracker (no ExecuteDelete) para mantener la
        // consistencia con el interceptor de auditoría y los demás repos.
        var existing = await dbContext.DeviceTokens
            .FirstOrDefaultAsync(x => x.Token == token, ct);
        if (existing is null)
        {
            return false;
        }

        dbContext.DeviceTokens.Remove(existing);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }
}