using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoppAddresd.Application.WhatsApp.Messaging;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.WhatsApp.Messaging;

/// <summary>Adaptador Cloud API sin reintentos HTTP implícitos: cada intento se traza en el worker.</summary>
public sealed class MetaCloudWhatsAppSender(
    HttpClient client,
    WhatsAppOptions options,
    TimeProvider clock,
    ILogger<MetaCloudWhatsAppSender> logger
) : IWhatsAppMessageSender
{
    public Task<WhatsAppSendResult> SendTextAsync(
        string waId,
        string text,
        DateTimeOffset lastInboundAt,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        var age = clock.GetUtcNow() - lastInboundAt;
        if (age < TimeSpan.Zero || age >= TimeSpan.FromHours(24))
        {
            logger.LogWarning(
                "Texto WhatsApp rechazado fuera de ventana para {Phone}",
                MessagePolicy.MaskPhone(waId)
            );
            return Task.FromResult(
                WhatsAppSendResult.Failed("window_closed", WhatsAppFailureKind.Permanent)
            );
        }
        if (string.IsNullOrEmpty(text) || text.Length > 4096)
            return Task.FromResult(
                WhatsAppSendResult.Failed("invalid_text", WhatsAppFailureKind.Permanent)
            );
        return SendAsync(
            waId,
            new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = waId.TrimStart('+'),
                type = "text",
                text = new { body = text, preview_url = false },
            },
            ct
        );
    }

    public Task<WhatsAppSendResult> SendTemplateAsync(
        string waId,
        string name,
        string language,
        IReadOnlyList<string> bodyParameters,
        CancellationToken ct
    ) =>
        SendAsync(
            waId,
            new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = waId.TrimStart('+'),
                type = "template",
                template = new
                {
                    name,
                    language = new { code = language },
                    components = new[]
                    {
                        new
                        {
                            type = "body",
                            parameters = bodyParameters
                                .Select(value => new { type = "text", text = value })
                                .ToArray(),
                        },
                    },
                },
            },
            ct
        );

    /// <summary>
    /// Marca como leído el mensaje entrante y activa el indicador de escritura
    /// (tres puntos). Meta lo descarta al responder o a los 25 segundos; el
    /// worker lo refresca cada 20 s mientras prepara la respuesta del agente.
    /// Es cosmético: cualquier fallo queda en log Debug y se ignora.
    /// </summary>
    public async Task ShowTypingAsync(string inboundWamid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(inboundWamid))
            return;
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://graph.facebook.com/v23.0/{Uri.EscapeDataString(options.PhoneNumberId)}/messages"
        );
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            options.AccessToken
        );
        request.Content = JsonContent.Create(
            new
            {
                messaging_product = "whatsapp",
                status = "read",
                message_id = inboundWamid,
                typing_indicator = new { type = "text" },
            }
        );
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var response = await client.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
                logger.LogDebug(
                    "Indicador de escritura rechazado por Meta: HTTP {Status}",
                    (int)response.StatusCode
                );
        }
        // Cosmético: cualquier fallo se traga, salvo la cancelación del llamador
        // (disciplina OCE del módulo). El timeout interno solo detiene la llamada.
        catch (Exception ex)
            when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Indicador de escritura fallido (cosmético)");
        }
    }

    private async Task<WhatsAppSendResult> SendAsync(
        string waId,
        object payload,
        CancellationToken ct
    )
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://graph.facebook.com/v23.0/{Uri.EscapeDataString(options.PhoneNumberId)}/messages"
        );
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            options.AccessToken
        );
        request.Content = JsonContent.Create(payload);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var response = await client.SendAsync(request, timeout.Token);
            int? code = null;
            string? wamid = null;
            try
            {
                using var json = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(timeout.Token),
                    cancellationToken: timeout.Token
                );
                if (
                    json.RootElement.TryGetProperty("error", out var error)
                    && error.TryGetProperty("code", out var codeElement)
                    && codeElement.TryGetInt32(out var parsed)
                )
                    code = parsed;
                if (
                    response.IsSuccessStatusCode
                    && json.RootElement.TryGetProperty("messages", out var messages)
                    && messages.GetArrayLength() > 0
                    && messages[0].TryGetProperty("id", out var id)
                )
                    wamid = id.GetString();
            }
            catch (JsonException)
            { /* Un proxy puede responder HTML; clasificar por HTTP sin guardar el cuerpo. */
            }
            if (!string.IsNullOrWhiteSpace(wamid))
            {
                logger.LogInformation(
                    "Envío WhatsApp aceptado para {Phone}",
                    MessagePolicy.MaskPhone(waId)
                );
                return new(wamid);
            }
            var kind = response.IsSuccessStatusCode
                ? WhatsAppFailureKind.Transient
                : MessagePolicy.Classify((int)response.StatusCode, code);
            var safeCode =
                code?.ToString(System.Globalization.CultureInfo.InvariantCulture)
                ?? $"http_{(int)response.StatusCode}";
            logger.LogWarning(
                "Envío WhatsApp fallido para {Phone}: {Code} {Kind}",
                MessagePolicy.MaskPhone(waId),
                safeCode,
                kind
            );
            return WhatsAppSendResult.Failed(safeCode, kind);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return WhatsAppSendResult.Failed("timeout", WhatsAppFailureKind.Transient);
        }
        catch (HttpRequestException)
        {
            return WhatsAppSendResult.Failed("network", WhatsAppFailureKind.Transient);
        }
    }
}
