using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

public interface IInventoryRepository
{
    // Products
    Task<Product?> GetProductByIdAsync(Guid id, CancellationToken ct = default);
    Task<Product?> GetProductBySkuAsync(string sku, CancellationToken ct = default);
    Task<(IReadOnlyList<Product> Items, int Total)> ListProductsAsync(
        string? search, string? category, string? status, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListSuppliersAsync(CancellationToken ct = default);
    Task<Product> AddProductAsync(Product product, CancellationToken ct = default);
    Task UpdateProductAsync(Product product, CancellationToken ct = default);
    Task DeleteProductAsync(Product product, CancellationToken ct = default);

    // Entries
    Task<InventoryEntry?> GetEntryByIdAsync(Guid id, CancellationToken ct = default);
    Task<InventoryEntry> AddEntryAsync(InventoryEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<InventoryEntry>> ListEntriesAsync(int page, int pageSize, CancellationToken ct = default);

    // Exits
    Task<InventoryExit?> GetExitByIdAsync(Guid id, CancellationToken ct = default);
    Task<InventoryExit> AddExitAsync(InventoryExit exit, CancellationToken ct = default);
    Task<IReadOnlyList<InventoryExit>> ListExitsAsync(int page, int pageSize, CancellationToken ct = default);

    // Movements
    Task<IReadOnlyList<InventoryMovement>> ListMovementsAsync(
        string? search, string? direction, int page, int pageSize, CancellationToken ct = default);

    // Analytics
    Task<InventoryAnalyticsDto> GetAnalyticsAsync(CancellationToken ct = default);

    // Stock
    Task AdjustStockAsync(Guid productId, int delta, string reference, string direction, string reason, string? user, CancellationToken ct = default);
}
