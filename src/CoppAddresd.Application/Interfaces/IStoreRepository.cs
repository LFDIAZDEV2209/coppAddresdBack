using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

public interface IStoreRepository
{
    Task<StoreItem?> GetStoreItemByIdAsync(Guid id, CancellationToken ct = default);
    Task<StoreItem?> GetStoreItemByProductIdAsync(Guid productId, CancellationToken ct = default);
    Task<(IReadOnlyList<StoreItem> Items, int Total)> ListStoreItemsAsync(
        string? status, int page, int pageSize, CancellationToken ct = default);
    Task<StoreItem> AddStoreItemAsync(StoreItem item, CancellationToken ct = default);
    Task UpdateStoreItemAsync(StoreItem item, CancellationToken ct = default);
    Task DeleteStoreItemAsync(StoreItem item, CancellationToken ct = default);
}
