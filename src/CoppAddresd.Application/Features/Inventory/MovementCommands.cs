using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Inventory;

// --- List Movements ---

public record ListMovementsQuery(
    string? Search = null, string? Direction = null,
    DateOnly? DateFrom = null, DateOnly? DateTo = null,
    int Page = 1, int PageSize = 20)
    : IRequest<PaginatedMovementsResult>;

public sealed class ListMovementsQueryHandler(
    IInventoryRepository repository) : IRequestHandler<ListMovementsQuery, PaginatedMovementsResult>
{
    public async Task<PaginatedMovementsResult> Handle(ListMovementsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await repository.ListMovementsAsync(
            request.Search, request.Direction, request.DateFrom, request.DateTo, page, pageSize, ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(result.Total / (double)pageSize));

        return new PaginatedMovementsResult(
            result.Items.Select(InventoryMovementDto.FromEntity).ToList(),
            result.Total, page, pageSize, totalPages);
    }
}

// --- Get Analytics ---

public record GetInventoryAnalyticsQuery(DateOnly? DateFrom = null, DateOnly? DateTo = null)
    : IRequest<InventoryAnalyticsDto>;

public sealed class GetInventoryAnalyticsQueryHandler(
    IInventoryRepository repository) : IRequestHandler<GetInventoryAnalyticsQuery, InventoryAnalyticsDto>
{
    public async Task<InventoryAnalyticsDto> Handle(GetInventoryAnalyticsQuery request, CancellationToken ct)
        => await repository.GetAnalyticsAsync(request.DateFrom, request.DateTo, ct);
}
