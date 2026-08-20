namespace CoppAddresd.Domain.Enums;

public static class ProductStatuses
{
    public const string Activo = "Activo";
    public const string Inactivo = "Inactivo";

    public static bool IsValid(string? value)
        => value is Activo or Inactivo;
}
