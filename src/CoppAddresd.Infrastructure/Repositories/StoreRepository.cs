using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class StoreRepository(AppDbContext dbContext) : IStoreRepository
{
    public async Task<StoreItem?> GetStoreItemByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.StoreItems
            .AsNoTracking()
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<StoreItem?> GetStoreItemByProductIdAsync(Guid productId, CancellationToken ct = default)
        => await dbContext.StoreItems
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProductId == productId, ct);

    public async Task<(IReadOnlyList<StoreItem> Items, int Total)> ListStoreItemsAsync(
        string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = dbContext.StoreItems
            .AsNoTracking()
            .Include(s => s.Product);

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
            query = query.Where(s => s.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<StoreItem> AddStoreItemAsync(StoreItem item, CancellationToken ct = default)
    {
        dbContext.StoreItems.Add(item);
        await dbContext.SaveChangesAsync(ct);
        return item;
    }

    public async Task UpdateStoreItemAsync(StoreItem item, CancellationToken ct = default)
    {
        dbContext.StoreItems.Update(item);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteStoreItemAsync(StoreItem item, CancellationToken ct = default)
    {
        dbContext.StoreItems.Remove(item);
        await dbContext.SaveChangesAsync(ct);
    }
}
