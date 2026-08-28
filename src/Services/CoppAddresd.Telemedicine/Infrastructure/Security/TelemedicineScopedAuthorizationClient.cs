using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoppAddresd.Telemedicine.Infrastructure.Cache;
using CoppAddresd.Telemedicine.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Telemedicine.Infrastructure.Security;

/// <summary>
/// Introspección de permisos por contexto contra el Auth Service (mismo
/// patrón que <c>ScopedAuthorizationClient</c> del backend del ERP): los
/// permisos de los roles asignados con scope de clínica no viajan en el JWT
/// (solo los globales), por lo que el micro consulta al Auth Service con la
/// cadena de scopes del contexto activo. El resultado se cachea de forma
/// DISTRIBUIDA (Valkey, compartido entre réplicas) keyed por security stamp:
/// la invalidación es natural cuando el Auth Service revoca tokens, y con
/// varias réplicas del micro solo la primera golpea al Auth Service. El
/// caché es fail-open: si Valkey está caído, la introspección HTTP se
/// ejecuta siempre (igual que antes de existir el caché).
/// </summary>
public interface ITelemedicineScopedAuthorizationClient
{
    /// <summary>¿Tiene el usuario el permiso en la cadena de scopes (o global)?</summary>
    Task<bool> AuthorizeAsync(
        Guid userId,
        string securityStamp,
        string permissionCode,
        IReadOnlyList<ScopeEntry> scopeChain,
        CancellationToken ct = default
    );
}

/// <summary>Entrada de la cadena de scopes (Clinic:id | Organization:id | Global).</summary>
public sealed record ScopeEntry(string ScopeType, Guid? ScopeId)
{
    public static readonly ScopeEntry Global = new("Global", null);

    public static string EncodeChain(IEnumerable<ScopeEntry> chain) =>
        string.Join(
            "|",
            chain.Select(scope =>
                scope.ScopeId is { } id ? $"{scope.ScopeType}:{id}" : scope.ScopeType
            )
        );
}

public sealed class TelemedicineScopedAuthorizationClient(
    HttpClient httpClient,
    IOptions<AuthServiceSettings> settings,
    ICacheService cache,
    ILogger<TelemedicineScopedAuthorizationClient> logger
) : ITelemedicineScopedAuthorizationClient
{
    private readonly AuthServiceSettings _settings = settings.Value;

    /// <summary>TTL = vida del access token: cota máxima de staleness; la
    /// invalidación activa es la rotación del security stamp (cambia la clave).</summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    /// <summary>Versión de la clave (bump para invalidar el dominio entero).</summary>
    private const string KeyVersion = "v1";

    public async Task<bool> AuthorizeAsync(
        Guid userId,
        string securityStamp,
        string permissionCode,
        IReadOnlyList<ScopeEntry> scopeChain,
        CancellationToken ct = default
    )
    {
        var key = CacheKey(userId, securityStamp, permissionCode, scopeChain);

        // bool es value type y el contrato de caché usa T : class: se cachea
        // como string "1"/"0". El miss no distingue "sin caché" de "caché
        // degradado" — en ambos casos se hace la introspección HTTP.
        var cached = await cache.GetAsync<string>(key, ct);
        if (cached is not null)
        {
            return cached == "1";
        }

        var scopes = ScopeEntry.EncodeChain(scopeChain);
        var url =
            $"/api/auth/internal/authorize?userId={userId}"
            + $"&permissionCode={Uri.EscapeDataString(permissionCode)}"
            + $"&scopes={Uri.EscapeDataString(scopes)}";

        var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var allowed = doc.RootElement.TryGetProperty("allowed", out var prop) && prop.GetBoolean();

        logger.LogDebug(
            "Introspección scoped: user {UserId} permission {Permission} chain {Chain} -> {Allowed}",
            userId,
            permissionCode,
            scopes,
            allowed
        );

        await cache.SetAsync(key, allowed ? "1" : "0", CacheTtl, ct);
        return allowed;
    }

    private static string CacheKey(
        Guid userId,
        string securityStamp,
        string permissionCode,
        IReadOnlyList<ScopeEntry> scopeChain
    )
    {
        var raw = $"{userId}|{securityStamp}|{permissionCode}|{ScopeEntry.EncodeChain(scopeChain)}";
        // El prefijo de servicio (tele:) lo añade la implementación de caché.
        return $"scope:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant()}:{KeyVersion}";
    }
}
