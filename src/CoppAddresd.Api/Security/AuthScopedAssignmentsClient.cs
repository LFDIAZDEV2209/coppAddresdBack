using System.Net.Http.Json;
using System.Text.Json;
using CoppAddresd.Api.Configuration;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Api.Security;

/// <summary>
/// Cliente de los endpoints internos de asignaciones scoped del Auth Service
/// (<c>api/auth/internal/scoped-assignments</c>, header X-Internal-Key).
/// </summary>
public class AuthScopedAssignmentsClient(
    HttpClient httpClient,
    IOptions<AuthServiceSettings> settings,
    ILogger<AuthScopedAssignmentsClient> logger) : IAuthScopedAssignmentsClient
{
    private readonly AuthServiceSettings _settings = settings.Value;

    public async Task<ScopedAssignmentsResult> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync(
            $"/api/auth/internal/scoped-assignments?userId={userId}", ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Auth scoped GET falló: {Status} {Body}",
                (int)response.StatusCode, body);
            throw new HttpRequestException(
                $"El Auth Service rechazó la lectura de asignaciones ({response.StatusCode}).",
                null,
                response.StatusCode);
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        var roles = root.GetProperty("roles").EnumerateArray()
            .Select(r => new ScopedRoleAssignmentView(
                r.GetProperty("roleId").GetGuid(),
                r.GetProperty("roleName").GetString() ?? string.Empty,
                r.GetProperty("scopeType").GetString() ?? string.Empty,
                r.TryGetProperty("scopeId", out var sid) && sid.ValueKind == JsonValueKind.String
                    ? sid.GetGuid()
                    : null))
            .ToList();

        var permissions = root.GetProperty("permissions").EnumerateArray()
            .Select(p => new ScopedPermissionAssignmentView(
                p.GetProperty("permissionId").GetGuid(),
                p.GetProperty("permissionCode").GetString() ?? string.Empty,
                p.GetProperty("scopeType").GetString() ?? string.Empty,
                p.TryGetProperty("scopeId", out var sid) && sid.ValueKind == JsonValueKind.String
                    ? sid.GetGuid()
                    : null,
                p.GetProperty("effect").GetString() ?? "Grant"))
            .ToList();

        return new ScopedAssignmentsResult(roles, permissions);
    }

    public async Task ReplaceAsync(
        Guid userId,
        IReadOnlyList<ScopedRoleAssignmentInput> roles,
        IReadOnlyList<ScopedPermissionAssignmentInput> permissions,
        Guid? grantedBy,
        CancellationToken ct = default)
    {
        var payload = new
        {
            userId,
            roles = roles.Select(r => new { r.RoleId, r.ScopeType, r.ScopeId }),
            permissions = permissions.Select(p => new { p.PermissionId, p.ScopeType, p.ScopeId, p.Effect }),
            grantedBy,
        };

        var response = await httpClient.PostAsJsonAsync(
            "/api/auth/internal/scoped-assignments", payload, JsonSerializerOptions.Default, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Auth scoped replace falló: {Status} {Body}",
                (int)response.StatusCode, body);

            // Traduce el mensaje del Auth (rol/permiso inexistente, scope
            // inválido...) a una excepción de dominio → 422 ante el cliente.
            if ((int)response.StatusCode == StatusCodes.Status400BadRequest)
            {
                var message = ExtractMessage(body) ?? "Asignación de scopes inválida.";
                throw new UnprocessableEntityException(message);
            }

            throw new HttpRequestException(
                $"El Auth Service rechazó la asignación de scopes ({response.StatusCode}).",
                null,
                response.StatusCode);
        }
    }

    private static string? ExtractMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}