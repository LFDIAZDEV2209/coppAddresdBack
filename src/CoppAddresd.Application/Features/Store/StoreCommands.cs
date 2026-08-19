using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;

namespace CoppAddresd.Application.Features.Store;

// --- List Store Items ---

public record ListStoreItemsQuery(
    string? Status = null, int Page = 1, int PageSize = 20)
    : IRequest<PaginatedStoreItemsResult>;

public sealed class ListStoreItemsQueryHandler(
    IStoreRepository repository) : IRequestHandler<ListStoreItemsQuery, PaginatedStoreItemsResult>
{
    public async Task<PaginatedStoreItemsResult> Handle(ListStoreItemsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, total) = await repository.ListStoreItemsAsync(
            request.Status, page, pageSize, ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedStoreItemsResult(
            items.Select(StoreItemListItemDto.FromEntity).ToList(),
            total, page, pageSize, totalPages);
    }
}

// --- Get Store Item ---

public record GetStoreItemQuery(Guid Id) : IRequest<StoreItemDto?>;

public sealed class GetStoreItemQueryHandler(
    IStoreRepository repository) : IRequestHandler<GetStoreItemQuery, StoreItemDto?>
{
    public async Task<StoreItemDto?> Handle(GetStoreItemQuery request, CancellationToken ct)
    {
        var item = await repository.GetStoreItemByIdAsync(request.Id, ct);
        return item is null ? null : StoreItemDto.FromEntity(item);
    }
}

// --- Create Store Item ---

public record CreateStoreItemCommand(CreateStoreItemRequest Request) : IRequest<StoreItemDto>;

public sealed class CreateStoreItemCommandHandler(
    IStoreRepository repository) : IRequestHandler<CreateStoreItemCommand, StoreItemDto>
{
    public async Task<StoreItemDto> Handle(CreateStoreItemCommand request, CancellationToken ct)
    {
        var r = request.Request;

        if (await repository.GetStoreItemByProductIdAsync(r.ProductId, ct) is not null)
            throw new InvalidOperationException("Este producto ya está en la tienda.");

        var item = new StoreItem
        {
            Id = Guid.NewGuid(),
            ProductId = r.ProductId,
            SalePrice = r.SalePrice,
            Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim(),
            Featured = r.Featured,
            Status = StoreItemStatuses.Visible,
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddStoreItemAsync(item, ct);
        return StoreItemDto.FromEntity(item);
    }
}

// --- Update Store Item ---

public record UpdateStoreItemCommand(Guid Id, UpdateStoreItemRequest Request) : IRequest<StoreItemDto?>;

public sealed class UpdateStoreItemCommandHandler(
    IStoreRepository repository) : IRequestHandler<UpdateStoreItemCommand, StoreItemDto?>
{
    public async Task<StoreItemDto?> Handle(UpdateStoreItemCommand request, CancellationToken ct)
    {
        var item = await repository.GetStoreItemByIdAsync(request.Id, ct);
        if (item is null) return null;

        var r = request.Request;
        item.SalePrice = r.SalePrice;
        item.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        item.Featured = r.Featured;
        item.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateStoreItemAsync(item, ct);
        return StoreItemDto.FromEntity(item);
    }
}

// --- Hide Store Item (soft delete) ---

public record HideStoreItemCommand(Guid Id) : IRequest<bool>;

public sealed class HideStoreItemCommandHandler(
    IStoreRepository repository) : IRequestHandler<HideStoreItemCommand, bool>
{
    public async Task<bool> Handle(HideStoreItemCommand request, CancellationToken ct)
    {
        var item = await repository.GetStoreItemByIdAsync(request.Id, ct);
        if (item is null) return false;

        item.Status = StoreItemStatuses.Oculto;
        item.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateStoreItemAsync(item, ct);
        return true;
    }
}

// --- Restore Store Item ---

public record RestoreStoreItemCommand(Guid Id) : IRequest<bool>;

public sealed class RestoreStoreItemCommandHandler(
    IStoreRepository repository) : IRequestHandler<RestoreStoreItemCommand, bool>
{
    public async Task<bool> Handle(RestoreStoreItemCommand request, CancellationToken ct)
    {
        var item = await repository.GetStoreItemByIdAsync(request.Id, ct);
        if (item is null) return false;

        item.Status = StoreItemStatuses.Visible;
        item.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateStoreItemAsync(item, ct);
        return true;
    }
}

// --- Delete Store Item (hard delete) ---

public record DeleteStoreItemCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteStoreItemCommandHandler(
    IStoreRepository repository) : IRequestHandler<DeleteStoreItemCommand, bool>
{
    public async Task<bool> Handle(DeleteStoreItemCommand request, CancellationToken ct)
    {
        var item = await repository.GetStoreItemByIdAsync(request.Id, ct);
        if (item is null) return false;

        await repository.DeleteStoreItemAsync(item, ct);
        return true;
    }
}
