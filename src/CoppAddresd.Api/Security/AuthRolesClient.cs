using System.Net;
using System.Text.Json;
using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Api.Security;

/// <summary>
/// Cliente del endpoint interno de consulta de roles por nombre del Auth
/// Service (<c>api/auth/internal/roles/by-name/{name}</c>, header X-Internal-Key).
/// Sigue el mismo patrón de <see cref="AuthScopedAssignmentsClient"/>.
/// </summary>
public class AuthRolesClient(
    HttpClient httpClient,
    ILogger<AuthRolesClient> logger) : IAuthRolesClient
{
    public async Task<AuthRoleLookupResult?> GetRoleByNameAsync(string name, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync(
            $"/api/auth/internal/roles/by-name/{Uri.EscapeDataString(name)}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Auth roles by-name GET falló: {Status} {Body}",
                (int)response.StatusCode, body);
            return null;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        return new AuthRoleLookupResult(
            root.GetProperty("id").GetGuid(),
            root.GetProperty("name").GetString() ?? string.Empty,
            root.GetProperty("isActive").GetBoolean());
    }
}
