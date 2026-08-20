namespace CoppAddresd.Domain.Entities;

/// <summary>Encabezado de una salida de inventario (stock out).</summary>
public sealed class InventoryExit
{
    public Guid Id { get; set; }

    public string Reference { get; set; } = default!;

    public DateTime Date { get; set; }

    public string Reason { get; set; } = default!;

    public string? Responsible { get; set; }

    public string? PatientName { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InventoryExitLine> Lines { get; set; } = [];
}
