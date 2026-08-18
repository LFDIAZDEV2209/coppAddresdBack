namespace CoppAddresd.Application.Common;

/// <summary>
/// Error tipado de comunicación con el AI Service: conserva el status code y
/// el detalle devuelto por el servicio para que los handlers puedan reaccionar
/// (p. ej. 404 del runtime → re-sincronizar el agente) y el controller pueda
/// propagar el error real al cliente en vez de un 500 genérico.
/// </summary>
public sealed class AiServiceException : Exception
{
    public AiServiceException(int statusCode, string detail)
        : base($"AI Service respondió {statusCode}: {detail}")
    {
        StatusCode = statusCode;
        Detail = detail;
    }

    /// <summary>Status HTTP devuelto por el AI Service.</summary>
    public int StatusCode { get; }

    /// <summary>Detalle devuelto por el AI Service (body del error).</summary>
    public string Detail { get; }
}
