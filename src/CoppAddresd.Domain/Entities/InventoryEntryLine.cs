namespace CoppAddresd.Domain.Entities;

/// <summary>Línea de detalle de una entrada de inventario.</summary>
public sealed class InventoryEntryLine
{
    public Guid Id { get; set; }

    public Guid EntryId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = default!;

    public int Quantity { get; set; }

    public string? Lot { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public decimal UnitCost { get; set; }

    public InventoryEntry Entry { get; set; } = default!;
    public Product Product { get; set; } = default!;
}
