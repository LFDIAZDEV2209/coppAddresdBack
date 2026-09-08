using CoppAddresd.Application.Features.Inventory.Events;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using MediatR;

namespace CoppAddresd.Application.Features.Inventory;

// --- List Exits ---

public record ListExitsQuery(int Page = 1, int PageSize = 20) : IRequest<PaginatedExitsResult>;

public sealed class ListExitsQueryHandler(
    IInventoryRepository repository) : IRequestHandler<ListExitsQuery, PaginatedExitsResult>
{
    public async Task<PaginatedExitsResult> Handle(ListExitsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await repository.ListExitsAsync(page, pageSize, ct);
        var total = items.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedExitsResult(
            items.Select(InventoryExitDto.FromEntity).ToList(),
            total, page, pageSize, totalPages);
    }
}

// --- Get Exit ---

public record GetExitQuery(Guid Id) : IRequest<InventoryExitDto?>;

public sealed class GetExitQueryHandler(
    IInventoryRepository repository) : IRequestHandler<GetExitQuery, InventoryExitDto?>
{
    public async Task<InventoryExitDto?> Handle(GetExitQuery request, CancellationToken ct)
    {
        var exit = await repository.GetExitByIdAsync(request.Id, ct);
        return exit is null ? null : InventoryExitDto.FromEntity(exit);
    }
}

// --- Create Exit ---

public record CreateExitCommand(CreateExitRequest Request) : IRequest<InventoryExitDto>;

public sealed class CreateExitCommandHandler(
    IInventoryRepository repository,
    IInventoryMetricsQueue? metricsQueue = null) : IRequestHandler<CreateExitCommand, InventoryExitDto>
{
    public async Task<InventoryExitDto> Handle(CreateExitCommand request, CancellationToken ct)
    {
        var r = request.Request;
        var reference = $"SAL-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var exit = new InventoryExit
        {
            Id = Guid.NewGuid(),
            Reference = reference,
            Date = r.Date,
            Reason = r.Reason,
            Responsible = r.Responsible,
            PatientName = r.PatientName,
            Notes = r.Notes,
            CreatedAt = DateTime.UtcNow,
            Lines = r.Lines.Select(l => new InventoryExitLine
            {
                Id = Guid.NewGuid(),
                ProductId = l.ProductId,
                ProductName = l.ProductName,
                Quantity = l.Quantity,
                Lot = l.Lot,
                ExpirationDate = l.ExpirationDate,
                UnitCost = l.UnitCost,
            }).ToList()
        };

        await repository.CreateExitAsync(exit, ct);

        // Pre-agregación CQRS (Dashboard #6): enqueue no bloqueante. InventoryExit
        // no tiene TotalCost propio; el procesador lo calcula desde las líneas.
        if (metricsQueue != null)
        {
            var metricLines = await BuildMetricLinesAsync(r.Lines, repository, ct);
            await metricsQueue.EnqueueAsync(new InventoryExitCreatedMetricEvent(
                exit.Id,
                DateOnly.FromDateTime(exit.Date),
                metricLines
            ), ct);
        }

        return InventoryExitDto.FromEntity(exit);
    }

    private static async Task<IReadOnlyList<InventoryMetricLine>> BuildMetricLinesAsync(
        IReadOnlyList<InventoryExitLineInput> lines,
        IInventoryRepository repository,
        CancellationToken ct)
    {
        var result = new List<InventoryMetricLine>();
        foreach (var l in lines)
        {
            var product = await repository.GetProductByIdAsync(l.ProductId, ct);
            result.Add(new InventoryMetricLine(
                l.ProductName,
                product?.Category ?? "general",
                l.Quantity,
                l.UnitCost
            ));
        }
        return result;
    }
}
