namespace CoppAddresd.Telemedicine.Application.Exceptions;

/// <summary>
/// Un servicio dependiente (p. ej. el backend para datos de referencia) no
/// está disponible o no respondió. Traducida a HTTP 503 por el middleware
/// global. Distinta de un error del dominio: el estado de la petición es
/// correcto pero el servicio upstream falló.
/// </summary>
public sealed class UpstreamUnavailableException(string message) : Exception(message)
{
}
