using System.Net.Http.Json;
using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Cliente tipado hacia el endpoint interno del servicio de Community
/// (SPEC A13): entrega un mensaje directo del perfil de sistema al usuario
/// destinatario, de modo que la notificación aparezca en la app del paciente.
/// Se autentica con el header <c>X-Internal-Key</c>.
/// </summary>
public sealed class CommunityMessageSender(
    HttpClient httpClient,
    IOptions<CommunityServiceSettings> settings,
    ILogger<CommunityMessageSender> logger
) : ICommunityMessageSender
{
    private readonly CommunityServiceSettings _settings = settings.Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc />
    public string Provider => "community";

    /// <inheritdoc />
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.BaseUrl)
        && !string.IsNullOrWhiteSpace(_settings.InternalApiKey);

    /// <inheritdoc />
    public async Task<CommunityMessageResult> SendDirectMessageAsync(
        Guid? patientUserId,
        string body,
        Guid? actorUserId,
        CancellationToken ct = default)
    {
        if (patientUserId is null || patientUserId == Guid.Empty)
        {
            return new CommunityMessageResult(
                false,
                null,
                null,
                SkipReason: "El paciente no tiene cuenta en la comunidad."
            );
        }

        if (!IsConfigured)
        {
            logger.LogWarning(
                "CommunityMessageSender sin configurar (Community:BaseUrl / Community:InternalApiKey)."
            );
            return new CommunityMessageResult(
                false,
                null,
                null,
                SkipReason: "El canal de comunidad no está configurado."
            );
        }

        var endpoint = string.IsNullOrWhiteSpace(_settings.InternalDirectMessageEndpoint)
            ? "/api/internal/messages/direct"
            : _settings.InternalDirectMessageEndpoint;

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_settings.BaseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}"
            )
            {
                Content = JsonContent.Create(
                    new DirectMessagePayload(null, patientUserId, body, actorUserId)
                ),
            };
            request.Headers.TryAddWithoutValidation(
                "X-Internal-Key",
                _settings.InternalApiKey
            );

            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning(
                    "Community rechazó el mensaje directo ({Status}): {Detail}",
                    (int)response.StatusCode,
                    detail
                );
                return new CommunityMessageResult(
                    false,
                    null,
                    $"Community respondió {(int)response.StatusCode}."
                );
            }

            var payload = await response.Content.ReadFromJsonAsync<DirectMessageResponse>(
                JsonOptions,
                ct
            );
            return new CommunityMessageResult(
                true,
                payload?.MessageId.ToString(),
                null
            );
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Error de red entregando el mensaje directo a Community.");
            return new CommunityMessageResult(false, null, "No se pudo contactar a Community.");
        }
    }

    /// <summary>Cuerpo del endpoint interno de Community.</summary>
    private sealed record DirectMessagePayload(
        Guid? RecipientProfileId,
        Guid? PatientUserId,
        string Body,
        Guid? ActorUserId
    );

    /// <summary>Respuesta del endpoint interno de Community.</summary>
    private sealed record DirectMessageResponse(Guid MessageId, Guid RecipientProfileId);
}
