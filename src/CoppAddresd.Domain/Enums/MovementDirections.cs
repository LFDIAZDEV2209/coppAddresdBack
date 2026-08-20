namespace CoppAddresd.Domain.Enums;

public static class MovementDirections
{
    public const string Entrada = "Entrada";
    public const string Salida = "Salida";

    public static bool IsValid(string? value)
        => value is Entrada or Salida;
}
