namespace CoppAddresd.Domain.Enums;

public static class InventoryEntryReasons
{
    public const string Compra = "Compra";
    public const string RecepcionProveedor = "Recepción de proveedor";
    public const string DevolucionCliente = "Devolución de cliente";
    public const string Donacion = "Donación";
    public const string AjustePositivo = "Ajuste positivo";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Compra, RecepcionProveedor, DevolucionCliente, Donacion, AjustePositivo
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());
}
