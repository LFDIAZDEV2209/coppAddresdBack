namespace CoppAddresd.Api.Security;

/// <summary>El token del enlace SOS es una capacidad privada, no un dato de diagnóstico.</summary>
public static class RequestLogPath
{
    public static string Redact(PathString path) => path.StartsWithSegments("/api/v1/sos/maps")
        ? "/api/v1/sos/maps/[redacted]"
        : path.StartsWithSegments("/api/v1/s") ? "/api/v1/s/[redacted]" : path.ToString();
}
