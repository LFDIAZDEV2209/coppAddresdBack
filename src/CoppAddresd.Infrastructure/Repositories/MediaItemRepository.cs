using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class MediaItemRepository(AppDbContext dbContext) : IMediaItemRepository
{
    public async Task<MediaItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.MediaItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<MediaItem?> GetByStorageKeyAsync(string storageKey, CancellationToken ct = default)
        => await dbContext.MediaItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.StorageKey == storageKey, ct);

    public async Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct = default)
        => await dbContext.MediaItems
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<MediaItem> AddAsync(MediaItem item, CancellationToken ct = default)
    {
        dbContext.MediaItems.Add(item);
        await dbContext.SaveChangesAsync(ct);
        return item;
    }

    public async Task UpdateAsync(MediaItem item, CancellationToken ct = default)
    {
        dbContext.MediaItems.Update(item);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(MediaItem item, CancellationToken ct = default)
    {
        dbContext.MediaItems.Remove(item);
        await dbContext.SaveChangesAsync(ct);
    }
}
