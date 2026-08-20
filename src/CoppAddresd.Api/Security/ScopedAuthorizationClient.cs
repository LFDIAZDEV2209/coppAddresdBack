using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoppAddresd.Api.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Api.Security;

/// <summary>
/// Introspección de permisos por contexto contra el Auth Service. Los
/// resultados se cachean en memoria keyed por (userId, securityStamp, scope,
/// permiso): al cambiar una asignación, el Auth Service invalida el security
/// stamp del usuario (bump) y la clave del caché deja de coincidir — la
/// invalidación es natural, sin purgas cruzadas entre servicios. TTL = vida
/// del access token (15 min) como cota de staleness.
/// </summary>
public interface IScopedAuthorizationClient
{
    /// <summary>¿Tiene el usuario el permiso en la cadena de scopes (o global)?</summary>
    Task<bool> AuthorizeAsync(
        Guid userId,
        string securityStamp,
        string permissionCode,
        IReadOnlyList<ScopeEntry> scopeChain,
        CancellationToken ct = default);

    /// <summary>Códigos de permiso efectivos del usuario para la cadena de scopes.</summary>
    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        Guid userId,
        string securityStamp,
        IReadOnlyList<ScopeEntry> scopeChain,
        CancellationToken ct = default);
}

public class ScopedAuthorizationClient(
    HttpClient httpClient,
    IOptions<AuthServiceSettings> settings,
    IMemoryCache cache,
    ILogger<ScopedAuthorizationClient> logger) : IScopedAuthorizationClient
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

        var url = BuildUrl("/api/auth/internal/authorize", userId, scopeChain,
            new Dictionary<string, string> { ["permissionCode"] = permissionCode });

        var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var allowed = doc.RootElement.TryGetProperty("allowed", out var prop) && prop.GetBoolean();

        logger.LogDebug("Introspección scoped: user {UserId} permission {Permission} chain {Chain} -> {Allowed}",
            userId, permissionCode, ScopeEntry.EncodeChain(scopeChain), allowed);

        cache.Set(key, allowed, CacheTtl);
        return allowed;
    }

    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        Guid userId,
        string securityStamp,
        IReadOnlyList<ScopeEntry> scopeChain,
        CancellationToken ct = default)
    {
        var key = CacheKey(userId, securityStamp, "*", scopeChain);
        if (cache.TryGetValue(key, out IReadOnlyList<string>? cached) && cached is not null)
        {
            return cached;
        }

        var url = BuildUrl("/api/auth/internal/scoped-permissions", userId, scopeChain, null);

        var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var permissions = doc.RootElement
            .GetProperty("permissions")
            .EnumerateArray()
            .Select(p => p.GetString() ?? string.Empty)
            .Where(p => p.Length > 0)
            .ToList();

        cache.Set(key, (IReadOnlyList<string>)permissions, CacheTtl);
        return permissions;
    }

    private string BuildUrl(
        string path,
        Guid userId,
        IReadOnlyList<ScopeEntry> scopeChain,
        IDictionary<string, string>? extra)
    {
        var scopes = ScopeEntry.EncodeChain(scopeChain);
        var query = $"userId={userId}&scopes={Uri.EscapeDataString(scopes)}";
        if (extra is not null)
        {
            query += "&" + string.Join("&", extra.Select(kv =>
                $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        }

        return $"{path}?{query}";
    }

    private static string CacheKey(
        Guid userId,
        string securityStamp,
        string permissionCode,
        IReadOnlyList<ScopeEntry> scopeChain)
    {
        var raw = $"{userId}|{securityStamp}|{permissionCode}|{ScopeEntry.EncodeChain(scopeChain)}";
        return "scope:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}
