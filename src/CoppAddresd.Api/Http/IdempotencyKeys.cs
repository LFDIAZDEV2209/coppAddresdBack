namespace CoppAddresd.Api.Http;

/// <summary>
/// Soporte del header opcional <c>X-Idempotency-Key</c> (Fase 12, resiliencia
/// en red débil): el móvil encola mutaciones offline y las reintenta con la
/// misma clave. Si el body ya trae su propia clave tiene prioridad; si no, se
/// usa el header. Claves vacías o mayores a 64 caracteres se ignoran (mismo
/// tope que <c>clientRequestId</c>).
/// </summary>
public static class IdempotencyKeys
{
    public const string HeaderName = "X-Idempotency-Key";

    public const int MaxLength = 64;

    public static string? Resolve(Microsoft.AspNetCore.Http.HttpRequest request, string? bodyKey)
    {
        if (!string.IsNullOrWhiteSpace(bodyKey))
            return bodyKey;

        if (!request.Headers.TryGetValue(HeaderName, out var values))
            return null;

        var header = values.ToString().Trim();
        if (header.Length == 0 || header.Length > MaxLength)
            return null;

        return header;
    }
}
