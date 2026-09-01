namespace CoppAddresd.Application.Common;

/// <summary>
/// Error tipado de comunicación con el Food AI Service: conserva el status
/// code y el detalle para que el controller mapee el error real (502) en vez
/// de un 500 genérico.
/// </summary>
public sealed class FoodAiException : Exception
{
    public FoodAiException(int statusCode, string detail)
        : base($"Food AI Service respondió {statusCode}: {detail}")
    {
        StatusCode = statusCode;
        Detail = detail;
    }

    /// <summary>Status HTTP devuelto por el Food AI Service.</summary>
    public int StatusCode { get; }

    /// <summary>Detalle devuelto por el servicio (body del error).</summary>
    public string Detail { get; }
}