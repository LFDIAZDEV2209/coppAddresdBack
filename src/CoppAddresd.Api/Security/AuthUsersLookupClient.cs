using System.Text.Json;
using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Api.Security;

/// <summary>
/// Cliente del endpoint interno <c>api/auth/internal/users/lookup</c>
/// (X-Internal-Key). Lee el estado de una cuenta por correo para el preflight
/// del alta de empleados; un fallo devuelve null (best-effort).
/// </summary>
public class AuthUsersLookupClient(
    HttpClient httpClient,
    ILogger<AuthUsersLookupClient> logger) : IAuthUsersLookupClient
{
    public async Task<AccountLookupResult?> LookupByEmailAsync(
        string email,
        CancellationToken ct = default
    )
    {
        try
        {
            var response = await httpClient.GetAsync(
                $"/api/auth/internal/users/lookup?email={Uri.EscapeDataString(email)}",
                ct
            );

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Auth users lookup falló: {Status}",
                    (int)response.StatusCode
                );
                return null;
            }

            using var doc = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(ct)
            );
            var root = doc.RootElement;

            return new AccountLookupResult(
                root.GetProperty("exists").GetBoolean(),
                root.GetProperty("isActive").GetBoolean(),
                root.GetProperty("hasPassword").GetBoolean()
            );
        }
        catch (Exception ex)
            when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(
                ex,
                "No se pudo consultar el estado de la cuenta para {Email}",
                email
            );
            return null;
        }
    }
}
