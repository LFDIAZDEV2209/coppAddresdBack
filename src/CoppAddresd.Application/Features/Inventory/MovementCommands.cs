using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Inventory;

// --- List Movements ---

public record ListMovementsQuery(
    string? Search = null, string? Direction = null,
    int Page = 1, int PageSize = 20)
    : IRequest<PaginatedMovementsResult>;

public sealed class ListMovementsQueryHandler(
    IInventoryRepository repository) : IRequestHandler<ListMovementsQuery, PaginatedMovementsResult>
{
    public async Task<PaginatedMovementsResult> Handle(ListMovementsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, total) = await repository.ListMovementsAsync(
            request.Search, request.Direction, page, pageSize, ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedMovementsResult(
            items.Select(InventoryMovementDto.FromEntity).ToList(),
            total, page, pageSize, totalPages);
    }
}

// --- Get Analytics ---

public record GetInventoryAnalyticsQuery : IRequest<InventoryAnalyticsDto>;

public sealed class GetInventoryAnalyticsQueryHandler(
    IInventoryRepository repository) : IRequestHandler<GetInventoryAnalyticsQuery, InventoryAnalyticsDto>
{
    public async Task<InventoryAnalyticsDto> Handle(GetInventoryAnalyticsQuery _, CancellationToken ct)
        => await repository.GetAnalyticsAsync(ct);
}
