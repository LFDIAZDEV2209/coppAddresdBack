namespace CoppAddresd.Application.Features.Store;

// --- Store Items ---

public record StoreItemDto(
    Guid Id, Guid ProductId, string ProductName, string ProductSku,
    string ProductType, decimal SalePrice, string? Description,
    bool Featured, string Status, DateTime CreatedAt, DateTime? UpdatedAt)
{
    public static StoreItemDto FromEntity(Domain.Entities.StoreItem si) => new(
        si.Id, si.ProductId, si.Product.Name, si.Product.Sku,
        si.Product.ProductType, si.SalePrice, si.Description,
        si.Featured, si.Status, si.CreatedAt, si.UpdatedAt);
}

public record StoreItemListItemDto(
    Guid Id, string ProductName, string ProductSku, string ProductType,
    decimal SalePrice, int Stock, bool Featured, string Status, DateTime CreatedAt)
{
    public static StoreItemListItemDto FromEntity(Domain.Entities.StoreItem si) => new(
        si.Id, si.Product.Name, si.Product.Sku, si.Product.ProductType,
        si.SalePrice, si.Product.Stock, si.Featured, si.Status, si.CreatedAt);
}

public record CreateStoreItemRequest(
    Guid ProductId, decimal SalePrice, string? Description, bool Featured);

public record UpdateStoreItemRequest(
    decimal SalePrice, string? Description, bool Featured);

public record PaginatedStoreItemsResult(
    IReadOnlyList<StoreItemListItemDto> Data, int Total, int Page, int PageSize, int TotalPages);
