namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Producto del catálogo de inventario. Cubre productos clínicos y bienestar.
/// El tipo se almacena como string validado contra <c>ProductTypes.All</c>.
/// </summary>
public sealed class Product
{
    public Guid Id { get; set; }

    public string Sku { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>Tipo de producto (Medicamento, Alimento saludable, etc.).</summary>
    public string ProductType { get; set; } = default!;

    public string Category { get; set; } = default!;

    public string? ActiveIngredient { get; set; }

    public string Presentation { get; set; } = default!;

    public string? Concentration { get; set; }

    public string Unit { get; set; } = default!;

    public string? Manufacturer { get; set; }

    public string? Supplier { get; set; }

    public string? Lot { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public int Stock { get; set; }

    public int MinimumStock { get; set; }

    public int MaximumStock { get; set; }

    public string? Location { get; set; }

    /// <summary>Activo o Inactivo.</summary>
    public string Status { get; set; } = "Activo";

    public decimal UnitCost { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation — referenced by entries/exits and store items.
    public ICollection<InventoryEntryLine> EntryLines { get; set; } = [];
    public ICollection<InventoryExitLine> ExitLines { get; set; } = [];
    public ICollection<InventoryMovement> Movements { get; set; } = [];
    public ICollection<StoreItem> StoreItems { get; set; } = [];
}
