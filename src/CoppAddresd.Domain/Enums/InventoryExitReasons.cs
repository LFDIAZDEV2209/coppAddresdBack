namespace CoppAddresd.Domain.Enums;

public static class InventoryExitReasons
{
    public const string Dispensacion = "Dispensación";
    public const string Venta = "Venta";
    public const string ConsumoInterno = "Consumo interno";
    public const string DevolucionProveedor = "Devolución a proveedor";
    public const string ProductoVencido = "Producto vencido";
    public const string ProductoDanado = "Producto dañado";
    public const string AjusteInventario = "Ajuste de inventario";
    public const string Otro = "Otro";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Dispensacion, Venta, ConsumoInterno, DevolucionProveedor,
        ProductoVencido, ProductoDanado, AjusteInventario, Otro
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());
}
