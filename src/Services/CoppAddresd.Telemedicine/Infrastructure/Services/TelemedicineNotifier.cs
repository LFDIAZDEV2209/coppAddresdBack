using System.Net.Http.Json;
using System.Text.Json;
using CoppAddresd.Telemedicine.Application.Interfaces;

namespace CoppAddresd.Telemedicine.Infrastructure.Services;

/// <summary>
/// Cliente del endpoint de entrega de notificaciones del backend
/// (<c>POST /api/v1/internal/telemedicine/notifications</c>, header
/// <c>X-Internal-Key</c>): Telemedicina decide qué notificar; el backend entrega
/// push/SMS y devuelve el estado por canal. Best-effort: un fallo se registra y
/// devuelve <c>false</c> (nunca lanza), para que la notificación jamás rompa el
/// flujo de negocio. La resiliencia (reintentos + circuit breaker) la aporta el
/// HttpClient registrado en DI.
/// </summary>
public sealed class TelemedicineNotifier(
    HttpClient httpClient,
    ILogger<TelemedicineNotifier> logger
) : ITelemedicineNotifier
{
    private const string NotificationsPath = "/api/v1/internal/telemedicine/notifications";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Web);

    public async Task<bool> SendAsync(
        TelemedicineNotification notification,
        CancellationToken ct = default
    )
    {
        var payload = new NotificationPayload(
            notification.UserId,
            notification.Title,
            notification.Body,
            // El contrato usa los nombres de canal en PascalCase ("Push"/"Sms").
            notification.Channels.Select(c => c.ToString()).ToList(),
            notification.Data,
            notification.DedupeKey
        );

        try
        {
            var response = await httpClient.PostAsJsonAsync(
                NotificationsPath,
                payload,
                JsonOptions,
                ct
            );

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "El backend rechazó la notificación {DedupeKey} ({StatusCode}).",
                    notification.DedupeKey,
                    (int)response.StatusCode
                );
                return false;
            }

            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogError(
                "Timeout del backend al enviar la notificación {DedupeKey}.",
                notification.DedupeKey
            );
            return false;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "No se pudo enviar la notificación {DedupeKey} al backend.",
                notification.DedupeKey
            );
            return false;
        }
        catch (Exception ex)
        {
            // Contrato best-effort: nada (serialización, respuesta rara) puede
            // tumbar el flujo de negocio que originó la notificación.
            logger.LogWarning(
                ex,
                "Fallo inesperado al enviar la notificación {DedupeKey}.",
                notification.DedupeKey
            );
            return false;
        }
    }

    /// <summary>Shape exacto del contrato de entrega (camelCase vía JsonOptions.Web).</summary>
    private sealed record NotificationPayload(
        Guid UserId,
        string Title,
        string Body,
        IReadOnlyList<string> Channels,
        IReadOnlyDictionary<string, string>? Data,
        string? DedupeKey
    );
}
