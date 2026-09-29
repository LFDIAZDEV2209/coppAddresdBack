using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Despacho del canal push FCM de una alerta SOS hacia el staff asignado
/// (REQ-SOS-04, D4): destinatarios = profesionales con asignación activa en
/// <c>app.patient_professionals</c> y tokens vigentes en
/// <c>app.device_tokens</c>. Dedupe durable <c>sos:push:{alertId}:{userId}</c>
/// impide reenvíos en reintentos. Purga de tokens: SOLO ante respuesta
/// inequívoca <c>Unregistered</c> (<see cref="FcmSendStatus.TokenInvalid"/>);
/// ante errores transitorios (5xx/red/timeout) el token se PRESERVA.
/// (REQ-SOS-06) Logs sin PII: solo alertId/userId/estado.
/// </summary>
public sealed class SosPushDispatcher(
    ISosAlertRepository alerts,
    IDeviceTokenRepository deviceTokens,
    IFcmClient fcmClient,
    INotificationDedupeRepository dedupe,
    ILogger<SosPushDispatcher> logger
) : ISosPushDispatcher
{
    public async Task<SosPushDispatchResult> DispatchAsync(
        SosAlert alert,
        CancellationToken ct = default
    )
    {
        var staffUserIds = await alerts.GetAssignedStaffUserIdsAsync(alert.PatientId, ct);
        if (staffUserIds.Count == 0)
        {
            logger.LogInformation(
                "SOS push sin staff asignado: alertId={AlertId}, patientId={PatientId}.",
                alert.Id,
                alert.PatientId
            );
            await UpdateAlertChannelAsync(
                alert,
                SosChannelStatus.NoConfigurado,
                0,
                "push:no_staff",
                ct
            );
            return new SosPushDispatchResult(SosChannelStatus.NoConfigurado, 0, "push:no_staff");
        }

        var sent = 0;
        var failed = 0;
        var disabled = 0;
        var tokensPurged = 0;

        var title = "🚨 Alerta SOS activa";
        var bodyText =
            "Un paciente activó su alerta SOS de emergencia. Revísala de inmediato en la plataforma.";

        foreach (var staffUserId in staffUserIds)
        {
            var dedupeKey = $"sos:push:{alert.Id}:{staffUserId}";
            var existing = await dedupe.GetByKeyAsync(dedupeKey, ct);
            if (
                existing?.PushStatus is not null
                && existing.PushStatus != nameof(SosChannelStatus.Pendiente).ToLowerInvariant()
            )
            {
                // Ya procesado para este destinatario: el outbox evita dobles.
                continue;
            }

            var tokens = await deviceTokens.GetByUserIdAsync(staffUserId, ct);
            var userResult = SosChannelStatus.Fallido;
            string? userDetail = null;

            if (tokens.Count == 0)
            {
                userResult = SosChannelStatus.NoConfigurado;
                userDetail = "push:no_tokens";
            }

            foreach (var token in tokens)
            {
                var push = await fcmClient.SendAsync(token.Token, title, bodyText, null, ct);
                switch (push.Status)
                {
                    case FcmSendStatus.Sent:
                        sent++;
                        userResult = SosChannelStatus.Enviado;
                        break;

                    case FcmSendStatus.Disabled:
                        disabled++;
                        userResult = SosChannelStatus.NoConfigurado;
                        userDetail = "push:fcm_disabled";
                        break;

                    case FcmSendStatus.TokenInvalid:
                        // (D4) Purga ÚNICAMENTE ante Unregistered inequívoco.
                        failed++;
                        await deviceTokens.DeleteByTokenAsync(token.Token, ct);
                        tokensPurged++;
                        break;

                    default:
                        // Error transitorio (5xx/red): el token SE PRESERVA.
                        failed++;
                        userDetail = $"push:error:{push.ErrorCode}";
                        break;
                }
            }

            if (userResult != SosChannelStatus.Enviado && failed > 0 && tokens.Count > 0)
            {
                userResult = SosChannelStatus.Fallido;
            }

            // Outbox: registra el estado final por destinatario.
            await dedupe.UpsertAsync(
                dedupeKey,
                staffUserId,
                pushStatus: userResult.ToString().ToLowerInvariant(),
                smsStatus: null,
                ct
            );
        }

        // Estado agregado del canal en la alerta.
        var channelStatus =
            sent > 0 ? SosChannelStatus.Enviado
            : disabled > 0 && failed == 0 && sent == 0 ? SosChannelStatus.NoConfigurado
            : failed > 0 ? SosChannelStatus.Fallido
            : SosChannelStatus.NoConfigurado;

        var detail =
            tokensPurged > 0
                ? $"push:{channelStatus.ToString().ToLowerInvariant()},purged={tokensPurged}"
                : $"push:{channelStatus.ToString().ToLowerInvariant()}";

        await UpdateAlertChannelAsync(alert, channelStatus, sent, detail, ct);

        logger.LogInformation(
            "SOS push despachado: alertId={AlertId}, sent={Sent}, failed={Failed}, disabled={Disabled}, purged={Purged}.",
            alert.Id,
            sent,
            failed,
            disabled,
            tokensPurged
        );

        return new SosPushDispatchResult(channelStatus, sent, detail);
    }

    private async Task UpdateAlertChannelAsync(
        SosAlert alert,
        SosChannelStatus status,
        int recipients,
        string? detail,
        CancellationToken ct
    )
    {
        try
        {
            var tracked = await alerts.GetByIdAsync(alert.Id, ct);
            if (tracked is null)
            {
                return;
            }

            tracked.PushChannelStatus = status;
            tracked.PushUpdatedAt = DateTime.UtcNow;
            tracked.PushRecipients = recipients;
            tracked.PushDetail = detail;
            await alerts.UpdateAsync(tracked, ct);
        }
        catch (DbUpdateException)
        {
            // Persistencia del estado del canal best-effort (no tumba al
            // procesador; la alerta sigue Activa y el outbox refleja el intento).
            logger.LogWarning(
                "No se pudo persistir el estado del canal push: alertId={AlertId}.",
                alert.Id
            );
        }
    }
}
