using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;

namespace CoppAddresd.Application.Features.Inventory;

// --- List Entries ---

public record ListEntriesQuery(int Page = 1, int PageSize = 20) : IRequest<PaginatedEntriesResult>;

public sealed class ListEntriesQueryHandler(
    IInventoryRepository repository) : IRequestHandler<ListEntriesQuery, PaginatedEntriesResult>
{
    public async Task<PaginatedEntriesResult> Handle(ListEntriesQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await repository.ListEntriesAsync(page, pageSize, ct);
        var total = items.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return new PaginatedEntriesResult(
            items.Select(InventoryEntryDto.FromEntity).ToList(),
            total, page, pageSize, totalPages);
    }
}

// --- Get Entry ---

public record GetEntryQuery(Guid Id) : IRequest<InventoryEntryDto?>;

public sealed class GetEntryQueryHandler(
    IInventoryRepository repository) : IRequestHandler<GetEntryQuery, InventoryEntryDto?>
{
    public async Task<InventoryEntryDto?> Handle(GetEntryQuery request, CancellationToken ct)
    {
        var entry = await repository.GetEntryByIdAsync(request.Id, ct);
        return entry is null ? null : InventoryEntryDto.FromEntity(entry);
    }
}

// --- Create Entry ---

public record CreateEntryCommand(CreateEntryRequest Request) : IRequest<InventoryEntryDto>;

public sealed class CreateEntryCommandHandler(
    IInventoryRepository repository) : IRequestHandler<CreateEntryCommand, InventoryEntryDto>
{
    public async Task<InventoryEntryDto> Handle(CreateEntryCommand request, CancellationToken ct)
    {
        var r = request.Request;
        var reference = $"ENT-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var entry = new InventoryEntry
        {
            Id = Guid.NewGuid(),
            Reference = reference,
            Date = r.Date,
            Reason = r.Reason,
            Supplier = r.Supplier,
            Document = r.Document,
            Responsible = r.Responsible,
            Notes = r.Notes,
            TotalCost = r.Lines.Sum(l => l.Quantity * l.UnitCost),
            CreatedAt = DateTime.UtcNow,
            Lines = r.Lines.Select(l => new InventoryEntryLine
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

        await repository.AddEntryAsync(entry, ct);

        foreach (var line in entry.Lines)
            await repository.AdjustStockAsync(line.ProductId, line.Quantity, reference,
                MovementDirections.Entrada, r.Reason, r.Responsible, ct);

        return InventoryEntryDto.FromEntity(entry);
    }
}
