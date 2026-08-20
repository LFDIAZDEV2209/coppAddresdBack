namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Ítem en la tienda de bienestar. Referencia un producto del inventario y
/// agrega campos de marketplace: precio de venta, descripción, destacado
/// y visibilidad (soft delete → "Oculto").
/// </summary>
public sealed class StoreItem
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public decimal SalePrice { get; set; }

    public string? Description { get; set; }

    public bool Featured { get; set; }

    /// <summary>"Visible" o "Oculto" (soft delete).</summary>
    public string Status { get; set; } = "Visible";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Product Product { get; set; } = default!;
}
