using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoppAddresd.Telemedicine.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Telemedicine.Infrastructure.Security;

/// <summary>
/// Introspección de permisos por contexto contra el Auth Service (mismo
/// patrón que <c>ScopedAuthorizationClient</c> del backend del ERP): los
/// permisos de los roles asignados con scope de clínica no viajan en el JWT
/// (solo los globales), por lo que el micro consulta al Auth Service con la
/// cadena de scopes del contexto activo. Cache keyed por security stamp:
/// la invalidación es natural cuando el Auth Service revoca tokens.
/// </summary>
public interface ITelemedicineScopedAuthorizationClient
{
    /// <summary>¿Tiene el usuario el permiso en la cadena de scopes (o global)?</summary>
    Task<bool> AuthorizeAsync(
        Guid userId,
        string securityStamp,
        string permissionCode,
        IReadOnlyList<ScopeEntry> scopeChain,
        CancellationToken ct = default);
}

/// <summary>Entrada de la cadena de scopes (Clinic:id | Organization:id | Global).</summary>
public sealed record ScopeEntry(string ScopeType, Guid? ScopeId)
{
    public static readonly ScopeEntry Global = new("Global", null);

    public static string EncodeChain(IEnumerable<ScopeEntry> chain)
        => string.Join("|", chain.Select(scope =>
            scope.ScopeId is { } id ? $"{scope.ScopeType}:{id}" : scope.ScopeType));
}

public sealed class TelemedicineScopedAuthorizationClient(
    HttpClient httpClient,
    IOptions<AuthServiceSettings> settings,
    IMemoryCache cache,
    ILogger<TelemedicineScopedAuthorizationClient> logger)
    : ITelemedicineScopedAuthorizationClient
{
    private readonly AuthServiceSettings _settings = settings.Value;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public async Task<bool> AuthorizeAsync(
        Guid userId,
        string securityStamp,
        string permissionCode,
        IReadOnlyList<ScopeEntry> scopeChain,
        CancellationToken ct = default)
    {
        var key = CacheKey(userId, securityStamp, permissionCode, scopeChain);
        if (cache.TryGetValue(key, out bool cached))
        {
            return cached;
        }

        var scopes = ScopeEntry.EncodeChain(scopeChain);
        var url = $"/api/auth/internal/authorize?userId={userId}" +
                  $"&permissionCode={Uri.EscapeDataString(permissionCode)}" +
                  $"&scopes={Uri.EscapeDataString(scopes)}";

        var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var allowed = doc.RootElement.TryGetProperty("allowed", out var prop) && prop.GetBoolean();

        logger.LogDebug("Introspección scoped: user {UserId} permission {Permission} chain {Chain} -> {Allowed}",
            userId, permissionCode, scopes, allowed);

        cache.Set(key, allowed, CacheTtl);
        return allowed;
    }

    private static string CacheKey(
        Guid userId,
        string securityStamp,
        string permissionCode,
        IReadOnlyList<ScopeEntry> scopeChain)
    {
        var raw = $"{userId}|{securityStamp}|{permissionCode}|{ScopeEntry.EncodeChain(scopeChain)}";
        return "tele-scope:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}