using CoppAddresd.Application.Features.Store;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class StoreRepository(AppDbContext dbContext) : IStoreRepository
{
    public async Task<StoreStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var total = await dbContext.StoreItems.CountAsync(ct);
        var visible = await dbContext.StoreItems.CountAsync(x => x.Status == "Visible", ct);
        var hidden = await dbContext.StoreItems.CountAsync(x => x.Status == "Oculto", ct);
        var featured = await dbContext.StoreItems.CountAsync(x => x.Featured && x.Status == "Visible", ct);
        return new StoreStatsDto(total, visible, hidden, featured);
    }

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
        IQueryable<StoreItem> query = dbContext.StoreItems
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
