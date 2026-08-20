using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using MediatR;

namespace CoppAddresd.Application.Features.Inventory;

// --- List Products ---

public record ListProductsQuery(
    string? Search = null, string? Category = null, string? Status = null,
    int Page = 1, int PageSize = 20)
    : IRequest<PaginatedProductsResult>;

public sealed class ListProductsQueryHandler(
    IInventoryRepository repository) : IRequestHandler<ListProductsQuery, PaginatedProductsResult>
{
    public async Task<PaginatedProductsResult> Handle(ListProductsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, total) = await repository.ListProductsAsync(
            request.Search, request.Category, request.Status, page, pageSize, ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedProductsResult(
            items.Select(ProductListItemDto.FromEntity).ToList(),
            total, page, pageSize, totalPages);
    }
}

// --- Get Product ---

public record GetProductQuery(Guid Id) : IRequest<ProductDto?>;

public sealed class GetProductQueryHandler(
    IInventoryRepository repository) : IRequestHandler<GetProductQuery, ProductDto?>
{
    public async Task<ProductDto?> Handle(GetProductQuery request, CancellationToken ct)
    {
        var product = await repository.GetProductByIdAsync(request.Id, ct);
        return product is null ? null : ProductDto.FromEntity(product);
    }
}

// --- List Categories ---

public record ListProductCategoriesQuery : IRequest<IReadOnlyList<string>>;

public sealed class ListProductCategoriesQueryHandler(
    IInventoryRepository repository) : IRequestHandler<ListProductCategoriesQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(ListProductCategoriesQuery _, CancellationToken ct)
        => await repository.ListCategoriesAsync(ct);
}

// --- List Suppliers ---

public record ListProductSuppliersQuery : IRequest<IReadOnlyList<string>>;

public sealed class ListProductSuppliersQueryHandler(
    IInventoryRepository repository) : IRequestHandler<ListProductSuppliersQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(ListProductSuppliersQuery _, CancellationToken ct)
        => await repository.ListSuppliersAsync(ct);
}

// --- Create Product ---

public record CreateProductCommand(CreateProductRequest Request) : IRequest<ProductDto>;

public sealed class CreateProductCommandHandler(
    IInventoryRepository repository) : IRequestHandler<CreateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken ct)
    {
        var r = request.Request;

        if (await repository.GetProductBySkuAsync(r.Sku, ct) is not null)
            throw new InvalidOperationException($"Ya existe un producto con SKU '{r.Sku}'.");

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = r.Sku.Trim(),
            Name = r.Name.Trim(),
            ProductType = r.ProductType.Trim(),
            Category = r.Category.Trim(),
            ActiveIngredient = string.IsNullOrWhiteSpace(r.ActiveIngredient) ? null : r.ActiveIngredient.Trim(),
            Presentation = r.Presentation.Trim(),
            Concentration = string.IsNullOrWhiteSpace(r.Concentration) ? null : r.Concentration.Trim(),
            Unit = r.Unit.Trim(),
            Manufacturer = string.IsNullOrWhiteSpace(r.Manufacturer) ? null : r.Manufacturer.Trim(),
            Supplier = string.IsNullOrWhiteSpace(r.Supplier) ? null : r.Supplier.Trim(),
            Lot = string.IsNullOrWhiteSpace(r.Lot) ? null : r.Lot.Trim(),
            ExpirationDate = r.ExpirationDate,
            Stock = r.Stock,
            MinimumStock = r.MinimumStock,
            MaximumStock = r.MaximumStock,
            Location = string.IsNullOrWhiteSpace(r.Location) ? null : r.Location.Trim(),
            Status = string.IsNullOrWhiteSpace(r.Status) ? "Activo" : r.Status.Trim(),
            UnitCost = r.UnitCost,
            Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddProductAsync(product, ct);
        return ProductDto.FromEntity(product);
    }
}

// --- Update Product ---

public record UpdateProductCommand(Guid Id, UpdateProductRequest Request) : IRequest<ProductDto?>;

public sealed class UpdateProductCommandHandler(
    IInventoryRepository repository) : IRequestHandler<UpdateProductCommand, ProductDto?>
{
    public async Task<ProductDto?> Handle(UpdateProductCommand request, CancellationToken ct)
    {
        var product = await repository.GetProductByIdAsync(request.Id, ct);
        if (product is null) return null;

        var r = request.Request;
        product.Sku = r.Sku.Trim();
        product.Name = r.Name.Trim();
        product.ProductType = r.ProductType.Trim();
        product.Category = r.Category.Trim();
        product.ActiveIngredient = string.IsNullOrWhiteSpace(r.ActiveIngredient) ? null : r.ActiveIngredient.Trim();
        product.Presentation = r.Presentation.Trim();
        product.Concentration = string.IsNullOrWhiteSpace(r.Concentration) ? null : r.Concentration.Trim();
        product.Unit = r.Unit.Trim();
        product.Manufacturer = string.IsNullOrWhiteSpace(r.Manufacturer) ? null : r.Manufacturer.Trim();
        product.Supplier = string.IsNullOrWhiteSpace(r.Supplier) ? null : r.Supplier.Trim();
        product.Lot = string.IsNullOrWhiteSpace(r.Lot) ? null : r.Lot.Trim();
        product.ExpirationDate = r.ExpirationDate;
        product.Stock = r.Stock;
        product.MinimumStock = r.MinimumStock;
        product.MaximumStock = r.MaximumStock;
        product.Location = string.IsNullOrWhiteSpace(r.Location) ? null : r.Location.Trim();
        product.Status = string.IsNullOrWhiteSpace(r.Status) ? product.Status : r.Status.Trim();
        product.UnitCost = r.UnitCost;
        product.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
        product.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateProductAsync(product, ct);
        return ProductDto.FromEntity(product);
    }
}

// --- Delete Product ---

public record DeleteProductCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteProductCommandHandler(
    IInventoryRepository repository) : IRequestHandler<DeleteProductCommand, bool>
{
    public async Task<bool> Handle(DeleteProductCommand request, CancellationToken ct)
    {
        var product = await repository.GetProductByIdAsync(request.Id, ct);
        if (product is null) return false;

        await repository.DeleteProductAsync(product, ct);
        return true;
    }
}
