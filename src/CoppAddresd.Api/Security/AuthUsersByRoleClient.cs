using System.Text.Json;
using CoppAddresd.Api.Configuration;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Api.Security;

/// <summary>
/// Cliente del endpoint interno de usuarios por rol del Auth Service
/// (<c>GET /api/auth/internal/users-by-role</c>, header X-Internal-Key).
/// Caché corta (60 s) keyed por roleId: los roles se asignan con poca
/// frecuencia y el filtro del directorio tolera la eventualidad.
/// </summary>
public class AuthUsersByRoleClient(
    HttpClient httpClient,
    IOptions<AuthServiceSettings> settings,
    IMemoryCache cache,
    ILogger<AuthUsersByRoleClient> logger
) : IAuthUsersByRoleClient
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<Guid>> GetUserIdsByRoleAsync(
        Guid roleId,
        CancellationToken ct = default
    )
    {
        if (cache.TryGetValue(roleId, out IReadOnlyList<Guid>? cached) && cached is not null)
        {
            return cached;
        }

        var url = $"/api/auth/internal/users-by-role?roleId={roleId}";

        try
        {
            var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "users-by-role rechazado: {Status} {Body}",
                    response.StatusCode,
                    await response.Content.ReadAsStringAsync(ct)
                );
                return [];
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (
                !doc.RootElement.TryGetProperty("userIds", out var prop)
                || prop.ValueKind != JsonValueKind.Array
            )
            {
                logger.LogWarning("users-by-role sin campo userIds");
                return [];
            }

            var ids = prop.EnumerateArray().Select(e => e.GetGuid()).ToList();

            cache.Set(roleId, ids, CacheTtl);
            return ids;
        }
        catch (Exception exc) when (exc is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exc, "No se pudo consultar users-by-role del Auth Service");
            return [];
        }
    }
}
