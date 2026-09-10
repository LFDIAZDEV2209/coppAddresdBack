using System.Net.Http.Json;
using System.Text.Json;
using CoppAddresd.Api.Configuration;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Api.Security;

/// <summary>
/// Cliente del endpoint interno de invitaciones del Auth Service
/// (<c>POST /api/auth/internal/invitations</c>, header X-Internal-Key).
/// Soporta adoptExisting: vincula usuarios existentes sin duplicar.
/// </summary>
public class AuthInvitationsClient(
    HttpClient httpClient,
    IOptions<AuthServiceSettings> settings,
    ILogger<AuthInvitationsClient> logger) : IAuthInvitationsClient
{
    private readonly AuthServiceSettings _settings = settings.Value;

    public async Task<InvitationCreationResult> CreateInvitationAsync(
        string email,
        string firstName,
        string lastName,
        bool adoptExisting = false,
        CancellationToken ct = default)
    {
        var payload = new { email, firstName, lastName, adoptExisting };

        var response = await httpClient.PostAsJsonAsync(
            "/api/auth/internal/invitations", payload, JsonSerializerOptions.Default, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Auth invitación falló: {Status} {Body}",
                (int)response.StatusCode, body);
            throw new HttpRequestException(
                $"El Auth Service rechazó la invitación ({response.StatusCode}).",
                null,
                response.StatusCode);
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        var adopted = root.TryGetProperty("adopted", out var adoptedProp) && adoptedProp.GetBoolean();
        var hasPassword = root.TryGetProperty("hasPassword", out var hpProp) && hpProp.GetBoolean();

        // InvitationId y ExpiresAt pueden ser null en el caso adopt + hasPassword.
        var invitationId = Guid.Empty;
        if (root.TryGetProperty("invitationId", out var invIdProp) && invIdProp.ValueKind != JsonValueKind.Null)
        {
            invitationId = invIdProp.GetGuid();
        }

        var expiresAt = DateTime.MinValue;
        if (root.TryGetProperty("expiresAt", out var expProp) && expProp.ValueKind != JsonValueKind.Null)
        {
            expiresAt = expProp.GetDateTimeOffset().UtcDateTime;
        }

        var link = root.TryGetProperty("link", out var linkProp) && linkProp.ValueKind == JsonValueKind.String
            ? linkProp.GetString()
            : null;

        return new InvitationCreationResult(
            root.GetProperty("userId").GetGuid(),
            invitationId,
            expiresAt,
            link,
            adopted,
            hasPassword);
    }

    public async Task RevokeAsync(Guid invitationId, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync(
            $"/api/auth/internal/invitations/{invitationId}/revoke", null, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Auth revocación falló: {Status} {Body}",
                (int)response.StatusCode, body);
            throw new HttpRequestException(
                $"El Auth Service rechazó la revocación ({response.StatusCode}).",
                null,
                response.StatusCode);
        }
    }
}
