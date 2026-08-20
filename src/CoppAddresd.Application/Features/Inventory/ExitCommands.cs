using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
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
    IInventoryRepository repository) : IRequestHandler<CreateExitCommand, InventoryExitDto>
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

        await repository.AddExitAsync(exit, ct);

        foreach (var line in exit.Lines)
            await repository.AdjustStockAsync(line.ProductId, -line.Quantity, reference,
                MovementDirections.Salida, r.Reason, r.Responsible, ct);

        return InventoryExitDto.FromEntity(exit);
    }
}
