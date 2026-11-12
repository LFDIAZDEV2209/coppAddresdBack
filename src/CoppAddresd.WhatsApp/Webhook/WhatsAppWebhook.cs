using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoppAddresd.WhatsApp.Messaging;
using CoppAddresd.WhatsApp.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.WhatsApp.Webhook;

public sealed class WhatsAppWebhook(
    WhatsAppDbContext db,
    WhatsAppOptions options,
    WhatsAppQueue queue,
    TimeProvider clock,
    ILogger<WhatsAppWebhook> logger
)
{
    public bool Verify(string? mode, string? token) =>
        mode == "subscribe"
        && token is not null
        && CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)),
            SHA256.HashData(Encoding.UTF8.GetBytes(options.WebhookVerifyToken))
        );

    public static bool ValidSignature(ReadOnlySpan<byte> body, string? header, string secret)
    {
        if (
            header is null
            || !header.StartsWith("sha256=", StringComparison.Ordinal)
            || header.Length != 71
        )
            return false;
        byte[] actual;
        try
        {
            actual = Convert.FromHexString(header.AsSpan(7));
        }
        catch (FormatException)
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(
            actual,
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body)
        );
    }

    public async Task<int> AcceptAsync(byte[] body, string? signature, CancellationToken ct)
    {
        if (!ValidSignature(body, signature, options.AppSecret))
        {
            logger.LogWarning("Webhook WhatsApp rechazado: firma ausente o inválida");
            return 401;
        }
        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return 400;
        }
        using (json)
        {
            if (
                !json.RootElement.TryGetProperty("object", out var obj)
                || obj.GetString() != "whatsapp_business_account"
            )
                return 200;
            if (
                !json.RootElement.TryGetProperty("entry", out var entries)
                || entries.ValueKind != JsonValueKind.Array
            )
                return 200;
            var payload = Encoding.UTF8.GetString(body);
            var now = clock.GetUtcNow();
            List<Guid> inserted = [];
            foreach (var entry in entries.EnumerateArray())
            {
                if (
                    !entry.TryGetProperty("changes", out var changes)
                    || changes.ValueKind != JsonValueKind.Array
                )
                    continue;
                foreach (var change in changes.EnumerateArray())
                {
                    if (
                        !change.TryGetProperty("value", out var value)
                        || !value.TryGetProperty("metadata", out var metadata)
                        || !metadata.TryGetProperty("phone_number_id", out var number)
                        || !value.TryGetProperty("messages", out var messages)
                        || messages.ValueKind != JsonValueKind.Array
                    )
                        continue;
                    var receiver = number.GetString();
                    // Un mensaje destinado a otro número de esta app (p.ej. el número de prueba
                    // reemplazado) se descarta; sin este log el caso queda sin rastro en prod
                    // y se percibe como "el bot no responde".
                    if (receiver != options.PhoneNumberId)
                    {
                        logger.LogInformation(
                            "Evento WhatsApp descartado: receptor {Receiver} no es el emisor configurado",
                            MessagePolicy.MaskPhone(receiver ?? "")
                        );
                        continue;
                    }
                    foreach (var message in messages.EnumerateArray())
                    {
                        var wamid = String(message, "id");
                        var from = String(message, "from");
                        if (
                            string.IsNullOrWhiteSpace(wamid)
                            || wamid.Length > 512
                            || string.IsNullOrWhiteSpace(from)
                            || from.Length is < 8 or > 15
                            || !from.All(char.IsAsciiDigit)
                        )
                            continue;
                        if (
                            !long.TryParse(String(message, "timestamp"), out var unix)
                            || unix < 0
                            || unix > 253402300799
                        )
                            continue;
                        var messageAt = DateTimeOffset.FromUnixTimeSeconds(unix);
                        if (messageAt > now)
                            messageAt = now;
                        var text =
                            String(message, "type") == "text"
                            && message.TryGetProperty("text", out var textObject)
                                ? String(textObject, "body") ?? ""
                                : "";
                        var id = Guid.NewGuid();
                        var waId = "+" + from;
                        // El conflicto incrementa la evidencia del duplicado sin reabrir trabajo ya procesado.
                        await db.Database.ExecuteSqlInterpolatedAsync(
                            $"""
                            INSERT INTO whatsapp.raw_events
                            (id, wamid, payload, wa_id, text, received_at, message_at, next_attempt_at, attempts, duplicate_count, sent_fragments)
                            VALUES ({id}, {wamid}, CAST({payload} AS jsonb), {waId}, {text}, {now}, {messageAt}, {now}, 0, 0, 0)
                            ON CONFLICT (wamid) DO UPDATE SET duplicate_count = whatsapp.raw_events.duplicate_count + 1
                            """,
                            ct
                        );
                        queue.Enqueue(id);
                        inserted.Add(id);
                        logger.LogInformation(
                            "Evento WhatsApp persistido para {Phone}",
                            MessagePolicy.MaskPhone(waId)
                        );
                    }
                }
            }
            if (inserted.Count > 0)
            {
                var ids = inserted.ToArray();
                // La interacción se hace visible antes del ACK, sin esperar al LLM.
                // IDs generados que no se insertaron por dedupe no participan en la cancelación.
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE whatsapp.conversations c SET last_inbound_at = GREATEST(c.last_inbound_at, incoming.at)
                    FROM (SELECT l.patient_id, MAX(e.message_at) AS at FROM whatsapp.raw_events e
                        JOIN whatsapp.patient_links l ON l.wa_id = e.wa_id WHERE e.id = ANY({ids}) GROUP BY l.patient_id) incoming
                    WHERE c.patient_id = incoming.patient_id;
                    UPDATE whatsapp.reminders r SET state = 'Cancelled', cancellation_reason = 'patient_interaction', lease_until = NULL
                    WHERE r.state = 'Pending' AND EXISTS (SELECT 1 FROM whatsapp.raw_events e
                        JOIN whatsapp.patient_links l ON l.wa_id = e.wa_id
                        WHERE e.id = ANY({ids}) AND l.patient_id = r.patient_id AND e.message_at >= r.scheduled_at)
                    """,
                    ct
                );
            }
        }
        return 200;
    }

    private static string? String(JsonElement element, string key) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
