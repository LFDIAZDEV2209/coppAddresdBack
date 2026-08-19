using CoppAddresd.Application.Features.Inventory;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class InventoryRepository(AppDbContext dbContext) : IInventoryRepository
{
    // --- Products ---

    public async Task<Product?> GetProductByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Product?> GetProductBySkuAsync(string sku, CancellationToken ct = default)
        => await dbContext.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Sku == sku, ct);

    public async Task<(IReadOnlyList<Product> Items, int Total)> ListProductsAsync(
        string? search, string? category, string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = dbContext.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, pattern) ||
                EF.Functions.ILike(p.Sku, pattern) ||
                EF.Functions.ILike(p.ActiveIngredient ?? "", pattern));
        }

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
            query = query.Where(p => p.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<string>> ListCategoriesAsync(CancellationToken ct = default)
        => await dbContext.Products.AsNoTracking()
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> ListSuppliersAsync(CancellationToken ct = default)
        => await dbContext.Products.AsNoTracking()
            .Where(p => p.Supplier != null)
            .Select(p => p.Supplier!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);

    public async Task<Product> AddProductAsync(Product product, CancellationToken ct = default)
    {
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(ct);
        return product;
    }

    public async Task UpdateProductAsync(Product product, CancellationToken ct = default)
    {
        dbContext.Products.Update(product);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteProductAsync(Product product, CancellationToken ct = default)
    {
        dbContext.Products.Remove(product);
        await dbContext.SaveChangesAsync(ct);
    }

    // --- Entries ---

    public async Task<InventoryEntry?> GetEntryByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.InventoryEntries
            .AsNoTracking()
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<InventoryEntry> AddEntryAsync(InventoryEntry entry, CancellationToken ct = default)
    {
        dbContext.InventoryEntries.Add(entry);
        await dbContext.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<IReadOnlyList<InventoryEntry>> ListEntriesAsync(int page, int pageSize, CancellationToken ct)
        => await dbContext.InventoryEntries
            .AsNoTracking()
            .Include(e => e.Lines)
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    // --- Exits ---

    public async Task<InventoryExit?> GetExitByIdAsync(Guid id, CancellationToken ct = default)
        => await dbContext.InventoryExits
            .AsNoTracking()
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<InventoryExit> AddExitAsync(InventoryExit exit, CancellationToken ct = default)
    {
        dbContext.InventoryExits.Add(exit);
        await dbContext.SaveChangesAsync(ct);
        return exit;
    }

    public async Task<IReadOnlyList<InventoryExit>> ListExitsAsync(int page, int pageSize, CancellationToken ct)
        => await dbContext.InventoryExits
            .AsNoTracking()
            .Include(e => e.Lines)
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    // --- Movements ---

    public async Task<(IReadOnlyList<InventoryMovement> Items, int Total)> ListMovementsAsync(
        string? search, string? direction, int page, int pageSize, CancellationToken ct)
    {
        var query = dbContext.InventoryMovements.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(m =>
                EF.Functions.ILike(m.ProductName, pattern) ||
                EF.Functions.ILike(m.Reference ?? "", pattern) ||
                EF.Functions.ILike(m.User ?? "", pattern));
        }

        if (!string.IsNullOrWhiteSpace(direction) && direction != "all")
            query = query.Where(m => m.Direction == direction);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.DateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    // --- Analytics ---

    public async Task<InventoryAnalyticsDto> GetAnalyticsAsync(CancellationToken ct = default)
    {
        var products = await dbContext.Products.AsNoTracking().ToListAsync(ct);
        var entries = await dbContext.InventoryEntries.AsNoTracking().CountAsync(ct);
        var exits = await dbContext.InventoryExits.AsNoTracking().CountAsync(ct);
        var movements = await dbContext.InventoryMovements.AsNoTracking().ToListAsync(ct);

        return new InventoryAnalyticsDto(
            TotalValue: products.Sum(p => p.Stock * p.UnitCost),
            ActiveProducts: products.Count(p => p.Status == "Activo"),
            LowStock: products.Count(p => p.Stock > 0 && p.Stock <= p.MinimumStock),
            OutOfStock: products.Count(p => p.Stock == 0),
            ExpiringSoon: products.Count(p => p.ExpirationDate.HasValue &&
                DateOnly.FromDateTime(p.ExpirationDate.Value) <= DateOnly.FromDateTime(DateTime.Today.AddDays(90)) &&
                DateOnly.FromDateTime(p.ExpirationDate.Value) > DateOnly.FromDateTime(DateTime.Today)),
            Expired: products.Count(p => p.ExpirationDate.HasValue &&
                DateOnly.FromDateTime(p.ExpirationDate.Value) < DateOnly.FromDateTime(DateTime.Today)),
            Entries: entries,
            Exits: exits,
            MovementSeries: [],
            TopMoving: [],
            CategoryValue: products.GroupBy(p => p.Category)
                .Select(g => new CategoryValue(g.Key, g.Sum(p => p.Stock * p.UnitCost)))
                .OrderByDescending(c => c.Value)
                .ToList());
    }

    // --- Stock adjustment ---

    public async Task AdjustStockAsync(Guid productId, int delta, string reference,
        string direction, string reason, string? user, CancellationToken ct = default)
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(x => x.Id == productId, ct);
        if (product is null) return;

        var before = product.Stock;
        product.Stock += delta;
        product.UpdatedAt = DateTime.UtcNow;

        dbContext.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(),
            DateTime = DateTime.UtcNow,
            ProductId = productId,
            ProductName = product.Name,
            Direction = direction,
            Quantity = Math.Abs(delta),
            StockBefore = before,
            StockAfter = product.Stock,
            Lot = product.Lot,
            User = user,
            Reason = reason,
            Reference = reference,
        });

        await dbContext.SaveChangesAsync(ct);
    }
}
