namespace CoppAddresd.Domain.Entities;

/// <summary>Línea de detalle de una salida de inventario.</summary>
public sealed class InventoryExitLine
{
    public Guid Id { get; set; }

    public Guid ExitId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = default!;

    public int Quantity { get; set; }

    public string? Lot { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public decimal UnitCost { get; set; }

    public InventoryExit Exit { get; set; } = default!;
    public Product Product { get; set; } = default!;
}
