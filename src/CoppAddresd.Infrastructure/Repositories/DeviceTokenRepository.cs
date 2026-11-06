using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class DeviceTokenRepository(AppDbContext dbContext) : IDeviceTokenRepository
{
    public async Task<DeviceToken> RegisterForUserAsync(Guid userId, string token, string platform, string? language, CancellationToken ct = default)
    {
        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Los reintentos recargan el propietario persistido y descartan entradas Added/Deleted obsoletas tras una transacción fallida.
            foreach (var entry in dbContext.ChangeTracker.Entries<DeviceToken>().Where(e => e.Entity.Token == token).ToList())
                entry.State = EntityState.Detached;
            // Un token físico no debe seguir recibiendo notificaciones de otra cuenta.
            // Serializa los registros de este token entre réplicas de la API, incluso cuando todavía no existe una fila.
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({token}, 0))", ct);
            var linked = await dbContext.DeviceTokens.Where(d => d.Token == token).ToListAsync(ct);
            var current = linked.SingleOrDefault(d => d.UserId == userId);
            dbContext.DeviceTokens.RemoveRange(linked.Where(d => d.UserId != userId));
            if (current is null)
            {
                current = new DeviceToken { Id = Guid.NewGuid(), UserId = userId, Token = token,
                    Platform = platform, Language = language ?? "es", CreatedAt = DateTimeOffset.UtcNow };
                dbContext.DeviceTokens.Add(current);
            }
            else
            {
                current.Platform = platform;
                if (language is not null) current.Language = language;
                current.UpdatedAt = DateTimeOffset.UtcNow;
            }
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return current;
        });
    }

    public async Task<bool> UnregisterForUserAsync(Guid userId, string token, CancellationToken ct = default)
    {
        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            foreach (var entry in dbContext.ChangeTracker.Entries<DeviceToken>().Where(e => e.Entity.Token == token).ToList())
                entry.State = EntityState.Detached;
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({token}, 0))", ct);
            var current = await dbContext.DeviceTokens.SingleOrDefaultAsync(d => d.UserId == userId && d.Token == token, ct);
            if (current is null) return false;
            dbContext.DeviceTokens.Remove(current);
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        });
    }

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
