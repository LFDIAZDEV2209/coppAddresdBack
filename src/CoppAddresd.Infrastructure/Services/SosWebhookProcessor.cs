using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio.Security;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="ISosWebhookProcessor"/>: valida la firma de
/// Twilio con el Auth Token del canal y persiste el estado de entrega sobre la
/// alerta (<c>app.sos_alerts</c>). Best-effort: un callback desconocido o una
/// firma inválida no lanzan; el resultado se informa al controlador.
/// </summary>
public sealed class SosWebhookProcessor(
    AppDbContext dbContext,
    IOptions<VoiceSettings> voiceSettings,
    IOptions<SmsSettings> smsSettings,
    ILogger<SosWebhookProcessor> logger
) : ISosWebhookProcessor
{
    /// <inheritdoc />
    public Task<SosWebhookOutcome> ProcessVoiceAsync(
        Guid? alertId,
        string url,
        string signature,
        IReadOnlyDictionary<string, string> form,
        CancellationToken ct = default
    ) =>
        ProcessAsync(
            alertId,
            url,
            signature,
            form,
            voiceSettings.Value.AuthToken,
            isVoice: true,
            ct
        );

    /// <inheritdoc />
    public Task<SosWebhookOutcome> ProcessSmsAsync(
        Guid? alertId,
        string url,
        string signature,
        IReadOnlyDictionary<string, string> form,
        CancellationToken ct = default
    ) =>
        ProcessAsync(
            alertId,
            url,
            signature,
            form,
            smsSettings.Value.AuthToken,
            isVoice: false,
            ct
        );

    private async Task<SosWebhookOutcome> ProcessAsync(
        Guid? alertId,
        string url,
        string signature,
        IReadOnlyDictionary<string, string> form,
        string authToken,
        bool isVoice,
        CancellationToken ct
    )
    {
        if (
            string.IsNullOrWhiteSpace(authToken)
            || string.IsNullOrWhiteSpace(signature)
            || !new RequestValidator(authToken).Validate(
                url,
                new Dictionary<string, string>(form, StringComparer.Ordinal),
                signature
            )
        )
        {
            logger.LogWarning(
                "Webhook SOS rechazado por firma inválida (canal={Channel}).",
                isVoice ? "voz" : "sms"
            );
            return SosWebhookOutcome.InvalidSignature;
        }

        if (alertId is null)
        {
            return SosWebhookOutcome.NotFound;
        }

        var alert = await dbContext.SosAlerts.FirstOrDefaultAsync(
            x => x.Id == alertId.Value,
            ct
        );
        if (alert is null)
        {
            return SosWebhookOutcome.NotFound;
        }

        if (isVoice)
        {
            var callStatus = form.GetValueOrDefault("CallStatus");
            if (!string.IsNullOrWhiteSpace(callStatus))
            {
                alert.VoiceCallStatus = callStatus.Trim();
            }

            var answeredBy = form.GetValueOrDefault("AnsweredBy");
            if (!string.IsNullOrWhiteSpace(answeredBy))
            {
                alert.VoiceAnsweredBy = answeredBy.Trim();
            }

            if (int.TryParse(form.GetValueOrDefault("CallDuration"), out var duration))
            {
                alert.VoiceDurationSeconds = duration;
            }

            var callSid = form.GetValueOrDefault("CallSid");
            if (!string.IsNullOrWhiteSpace(callSid))
            {
                alert.VoiceProviderCallId = callSid.Trim();
            }

            alert.VoiceUpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var messageStatus = form.GetValueOrDefault("MessageStatus");
            if (!string.IsNullOrWhiteSpace(messageStatus))
            {
                alert.SmsDeliveryStatus = messageStatus.Trim();
            }

            alert.SmsUpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Webhook SOS aplicado: alertId={AlertId}, canal={Channel}, estado={Status}.",
            alert.Id,
            isVoice ? "voz" : "sms",
            isVoice ? alert.VoiceCallStatus : alert.SmsDeliveryStatus
        );
        return SosWebhookOutcome.Processed;
    }
}
