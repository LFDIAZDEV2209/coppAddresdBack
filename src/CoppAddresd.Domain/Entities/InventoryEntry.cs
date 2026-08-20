namespace CoppAddresd.Domain.Entities;

/// <summary>Encabezado de una entrada de inventario (stock in).</summary>
public sealed class InventoryEntry
{
    public Guid Id { get; set; }

    public string Reference { get; set; } = default!;

    public DateTime Date { get; set; }

    public string Reason { get; set; } = default!;

    public string? Supplier { get; set; }

    public string? Document { get; set; }

    public string? Responsible { get; set; }

    public string? Notes { get; set; }

    public decimal TotalCost { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InventoryEntryLine> Lines { get; set; } = [];
}
