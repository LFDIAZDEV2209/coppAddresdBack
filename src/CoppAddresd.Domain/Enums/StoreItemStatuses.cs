namespace CoppAddresd.Domain.Enums;

public static class StoreItemStatuses
{
    public const string Visible = "Visible";
    public const string Oculto = "Oculto";

    public static bool IsValid(string? value)
        => value is Visible or Oculto;
}
