namespace CoppAddresd.Domain.Entities;

/// <summary>Movimiento de inventario (registro de auditoría de stock).</summary>
public sealed class InventoryMovement
{
    public Guid Id { get; set; }

    public DateTime DateTime { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = default!;

    public string Direction { get; set; } = default!;

    public int Quantity { get; set; }

    public int StockBefore { get; set; }

    public int StockAfter { get; set; }

    public string? Lot { get; set; }

    public string? User { get; set; }

    public string? Reason { get; set; }

    public string? Reference { get; set; }

    public Product Product { get; set; } = default!;
}
