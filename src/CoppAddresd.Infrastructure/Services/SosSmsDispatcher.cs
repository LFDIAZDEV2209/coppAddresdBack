using System.Diagnostics;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Despacho del canal SMS de una alerta SOS (REQ-SOS-03, D3): plantilla fija
/// 100% server-side (el cliente jamás envía texto ni destino), timeout de 5 s
/// por intento y hasta 2 reintentos con backoff exponencial. Deduplicación
/// durable vía <c>app.notification_dedupe_keys</c> con clave
/// <c>sos:sms:{alertId}</c>: un canal ya <c>Enviado</c> jamás se reenvía y
/// los reintentos del sistema no duplican. Degradación: sin credenciales o
/// ante timeout el canal queda registrado y la alerta permanece Activa —
/// nunca se propaga un 500.
/// (REQ-SOS-06) Los logs NO incluyen teléfono, coordenadas ni cuerpo del SMS:
/// solo alertId, canal, estado y latencia.
/// </summary>
public sealed class SosSmsDispatcher(
    ISmsSender smsSender,
    INotificationDedupeRepository dedupe,
    AppDbContext dbContext,
    IOptions<SosWebhookSettings> webhookSettings,
    ILogger<SosSmsDispatcher> logger,
    ISosLocationLinkService? locationLinks = null
) : ISosSmsDispatcher
{
    /// <summary>Timeout estricto por intento (D3): 5 segundos.</summary>
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Hasta 2 reintentos tras el primer intento (3 intentos totales).</summary>
    private const int MaxRetries = 2;

    public Task<SosChannelStatus> DispatchAsync(SosAlert alert, CancellationToken ct = default)
        => SosRecipientDispatch.RunAsync(alert, "sms", dedupe, dbContext, DispatchOneAsync, logger, ct);

    private async Task<SosChannelStatus> DispatchOneAsync(
        SosAlert alert,
        SosAlertRecipient? recipient,
        CancellationToken ct = default
    )
    {
        var dedupeKey = SosRecipientDispatch.Key("sms", alert.Id, recipient);
        var existing = await dedupe.GetByKeyAsync(dedupeKey, ct);
        if (
            existing?.SmsStatus is not null
            && existing.SmsStatus != nameof(SosChannelStatus.Pendiente).ToLowerInvariant()
        )
        {
            // El canal ya fue procesado (sent/failed/timeout/disabled): el
            // outbox evita reenvíos dobles aunque el procesador reintente.
            logger.LogInformation(
                "SOS SMS ya procesado (estado={Status}): alertId={AlertId}.",
                existing.SmsStatus,
                alert.Id
            );
            return Enum.TryParse<SosChannelStatus>(existing.SmsStatus, true, out var previous) ? previous : alert.SmsChannelStatus;
        }

        var body = SosSmsTemplate.Build(alert, locationLinks?.CreateLink(alert));
        var callbackUrl = BuildStatusCallbackUrl(alert.Id);
        if (callbackUrl is not null && recipient is not null) callbackUrl += $"&recipientId={recipient.Id}";

        var finalStatus = SosChannelStatus.Fallido;
        string? detail = null;

        if (!smsSender.IsConfigured)
        {
            finalStatus = SosChannelStatus.NoConfigurado;
            detail = "sms:not_configured";
        }
        else
        {
            var stopwatch = Stopwatch.StartNew();
            for (var attempt = 0; attempt <= MaxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                // Timeout estricto por intento, acotado además por el token de
                // la operación (no bloquea el procesador más allá de lo debido).
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                attemptCts.CancelAfter(AttemptTimeout);

                try
                {
                    var result = await smsSender.SendAsync(
                        alert.DestinationPhoneE164,
                        body,
                        attemptCts.Token,
                        callbackUrl
                    );

                    if (result.Success)
                    {
                        if (recipient is not null) recipient.SmsProviderMessageId = result.ProviderMessageId;
                        finalStatus = SosChannelStatus.Enviado;
                        detail = null;
                        break;
                    }

                    // Rechazo determinista del proveedor (número inválido,
                    // credenciales, 4xx): reintentar no cambia el resultado.
                    if (
                        result.Error is not null
                        && result.Error.StartsWith("twilio:", StringComparison.Ordinal)
                    )
                    {
                        finalStatus = SosChannelStatus.Fallido;
                        detail = result.Error;
                        break;
                    }

                    finalStatus = SosChannelStatus.Fallido;
                    detail = "sms:provider";
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Timeout del intento (el token global sigue activo).
                    finalStatus = SosChannelStatus.Timeout;
                    detail = "sms:timeout";
                    logger.LogWarning(
                        "SOS SMS timeout en intento {Attempt}: alertId={AlertId}.",
                        attempt + 1,
                        alert.Id
                    );
                }

                if (attempt < MaxRetries)
                {
                    // Backoff exponencial corto (1s, 2s) con el token global:
                    // el procesador nunca queda colgado.
                    await Task.Delay(TimeSpan.FromSeconds(1 << attempt), ct);
                }
            }

            stopwatch.Stop();
            logger.LogInformation(
                "SOS SMS despachado: alertId={AlertId}, status={Status}, latencyMs={LatencyMs}.",
                alert.Id,
                finalStatus,
                stopwatch.ElapsedMilliseconds
            );
        }

        // Outbox: registra el estado final durable (un Enviado es pegajoso).
        await dedupe.UpsertAsync(
            dedupeKey,
            Guid.Empty,
            pushStatus: null,
            smsStatus: finalStatus.ToString().ToLowerInvariant(),
            voiceStatus: null,
            ct: ct
        );

        await UpdateAlertChannelAsync(
            alert,
            a => a.SmsChannelStatus = finalStatus,
            a => a.SmsUpdatedAt = DateTime.UtcNow,
            a => a.SmsDetail = detail,
            recipient,
            ct
        );

        return finalStatus;
    }

    /// <summary>
    /// URL pública de callback de Twilio para el canal SMS. Null cuando no hay
    /// <c>SosWebhook:BaseUrl</c> configurado (sin seguimiento de entrega).
    /// </summary>
    private string? BuildStatusCallbackUrl(Guid alertId)
    {
        var baseUrl = webhookSettings.Value.NormalizedBaseUrl;
        return baseUrl is null
            ? null
            : $"{baseUrl}/api/v1/sos/webhooks/twilio/sms?alertId={alertId}";
    }

    /// <summary>Actualiza el estado del canal en <c>app.sos_alerts</c> (transacción corta).</summary>
    private async Task UpdateAlertChannelAsync(
        SosAlert alert,
        Action<SosAlert> setStatus,
        Action<SosAlert> setTimestamp,
        Action<SosAlert> setDetail,
        SosAlertRecipient? recipient,
        CancellationToken ct
    )
    {
        try
        {
            if (recipient is not null) { setStatus(alert); setTimestamp(alert); setDetail(alert); return; }
            var tracked = await dbContext.SosAlerts.FirstOrDefaultAsync(x => x.Id == alert.Id, ct);
            if (tracked is null)
            {
                return;
            }

            setStatus(tracked);
            setTimestamp(tracked);
            setDetail(tracked);
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is NpgsqlException)
        {
            // La persistencia del estado del canal es best-effort: no debe
            // tumbar el procesador ni la respuesta ya dada al paciente.
            logger.LogWarning(
                ex,
                "No se pudo persistir el estado del canal SMS: alertId={AlertId}.",
                alert.Id
            );
        }
    }
}

/// <summary>
/// SMS breve y accionable. No incluye documento, referencia interna ni
/// coordenadas sueltas. El cuerpo NUNCA se registra en logs (REQ-SOS-06).
/// </summary>
public static class SosSmsTemplate
{
    private const string GsmBasic = "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";
    private const string GsmExtended = "\f^{}\\[~]|€";

    public static string Build(SosAlert alert, string? locationUrl = null)
    {
        var name = GsmFriendly(SosMessageFormatting.FullName(alert.Patient, "Tu contacto"));
        var maps = SosMessageFormatting.LocationLink(alert, locationUrl);
        // El snapshot del enriquecedor ordena calle, ciudad/área, región, país.
        // En SMS se priorizan los dos primeros; el correo conserva todo.
        var address = maps is null || string.IsNullOrWhiteSpace(alert.LocationDescription) ? null
            : GsmFriendly(string.Join(", ", alert.LocationDescription.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(2)));
        var readings = SosMessageFormatting.ReportedVitals(alert);
        var vitals = readings.Count == 0 ? "Signos no compartidos."
            : "Signos: " + string.Join("; ", readings.Select(r => GsmFriendly($"{r.Label} {r.Value}")));
        if (readings.Count > 0)
        {
            vitals += "\n" + SosMessageFormatting.SmsReadingTimes(alert);
            var missing = SosMessageFormatting.MissingVitals(alert);
            if (missing.Length > 0) vitals += "\nSin dato: " + GsmFriendly(string.Join("/", missing)) + ".";
        }
        var phone = SosMessageFormatting.PatientPhone(alert.Patient);
        var gsm = IsGsm(name + address + maps + vitals);
        // Se priorizan los signos completos y el mapa íntegro: hasta dos
        // partes GSM como objetivo; Unicode puede requerir hasta cuatro.
        var budget = gsm ? 306 : readings.Count > 0 ? 268 : 201;
        var header = "Alerta SOS: " + Clip(name, gsm ? 48 : 24, gsm) + " necesita tu ayuda.\nCopp Adresd · " + SosMessageFormatting.FormatAlertTime(alert.CreatedAt) + " COL";
        var contact = phone is null ? "" : "\nLlamar: " + phone;
        if (maps is null) return header + "\n" + vitals + "\nUbicacion no compartida." + contact;
        var prefix = address is null ? "" : "Direccion: ";
        var reference = address ?? "Direccion no disponible.";
        var available = budget - Units(header + "\n" + vitals + "\n" + prefix + "\nMapa: " + maps + contact, gsm);
        // Nunca recorta signos ni la capacidad del mapa por imponer un número
        // de segmentos. Un origen excepcionalmente largo puede necesitar más.
        reference = FitAddress(reference, Math.Max(8, available), gsm);
        return header + "\n" + vitals + "\n" + prefix + reference + "\nMapa: " + maps + contact;
    }

    private static bool IsGsm(string text) => text.All(c => GsmBasic.Contains(c) || GsmExtended.Contains(c));
    private static int Units(string text, bool gsm) => gsm ? text.Sum(c => GsmExtended.Contains(c) ? 2 : 1) : text.Length;

    private static string FitAddress(string reference, int budget, bool gsm)
    {
        if (Units(reference, gsm) <= budget) return reference;
        var separator = reference.IndexOf(", ", StringComparison.Ordinal);
        if (separator >= 0)
        {
            var locality = reference[(separator + 2)..];
            var streetBudget = budget - Units(", " + locality, gsm);
            if (streetBudget >= 8) return Clip(reference[..separator], streetBudget, gsm) + ", " + locality;
        }
        return Clip(reference, budget, gsm);
    }

    /// <summary>Solo presentación SMS: no modifica nombres/direcciones persistidos.</summary>
    private static string GsmFriendly(string text)
    {
        var result = new System.Text.StringBuilder();
        var singleLine = SosMessageFormatting.SingleLine(text)
            .Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"')
            .Replace('–', '-').Replace('—', '-').Replace("…", "...");
        foreach (var rune in singleLine.EnumerateRunes())
        {
            var value = rune.ToString();
            if (IsGsm(value)) { result.Append(value); continue; }
            var plain = string.Concat(value.Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark));
            // Mantiene intactos los caracteres que no admiten transliteración
            // latina (p.ej. nombres chinos); no los convierte en signos «?».
            result.Append(IsGsm(plain) ? plain : value);
        }
        return result.ToString();
    }

    private static string Clip(string text, int budget, bool gsm)
    {
        if (Units(text, gsm) <= budget) return text;
        var result = new System.Text.StringBuilder();
        var elements = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        var used = 0;
        while (elements.MoveNext())
        {
            var value = elements.GetTextElement();
            var units = Units(value, gsm);
            if (used + units > Math.Max(0, budget - 3)) break;
            result.Append(value); used += units;
        }
        return result.ToString().TrimEnd() + "...";
    }
}
