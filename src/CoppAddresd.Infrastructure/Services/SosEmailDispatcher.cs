using CoppAddresd.Application.Features.Sos;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Despacha el canal de correo de una alerta SOS (REQ-SOS-03): reutiliza el
/// texto del SMS (plantilla fija server-side) y lo envía al correo del
/// contacto de emergencia con dedupe durable <c>sos:email:{alertId}</c>.
/// Nunca lanza: devuelve el estado final del canal.
/// </summary>
public sealed class SosEmailDispatcher(
    IEmailService emailService,
    INotificationDedupeRepository dedupe,
    ILogger<SosEmailDispatcher> logger
) : ISosEmailDispatcher
{
    private const string PendienteStatus = "pendiente";

    public async Task<SosChannelStatus> DispatchAsync(SosAlert alert, CancellationToken ct = default)
    {
        var dedupeKey = $"sos:email:{alert.Id}";

        var existing = await dedupe.GetByKeyAsync(dedupeKey, ct);
        if (
            existing?.EmailStatus is not null
            && !existing.EmailStatus.Equals(PendienteStatus, StringComparison.OrdinalIgnoreCase)
        )
        {
            logger.LogInformation(
                "SOS correo ya procesado: alertId={AlertId} estado={Status}.",
                alert.Id,
                existing.EmailStatus
            );
            return Enum.TryParse<SosChannelStatus>(
                existing.EmailStatus,
                ignoreCase: true,
                out var parsed
            )
                ? parsed
                : SosChannelStatus.Enviado;
        }

        var target = SosSupport.ExtractEmergencyContactEmail(alert.Patient?.EmergencyContact);
        SosChannelStatus finalStatus;

        if (string.IsNullOrWhiteSpace(target))
        {
            finalStatus = SosChannelStatus.NoConfigurado;
            logger.LogWarning("SOS correo sin destino configurado: alertId={AlertId}.", alert.Id);
        }
        else
        {
            var body = SosSmsTemplate.Build(alert);
            try
            {
                await emailService.SendEmailAsync(
                    target,
                    "🚨 SOS CoppAddresd — alerta de emergencia",
                    body,
                    isHtml: false,
                    cancellationToken: ct
                );
                finalStatus = SosChannelStatus.Enviado;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                finalStatus = SosChannelStatus.Fallido;
                logger.LogError(ex, "SOS correo falló: alertId={AlertId}.", alert.Id);
            }
        }

        await dedupe.UpsertAsync(
            dedupeKey,
            Guid.Empty,
            pushStatus: null,
            smsStatus: null,
            voiceStatus: null,
            emailStatus: finalStatus.ToString().ToLowerInvariant(),
            ct: ct
        );

        logger.LogInformation(
            "SOS correo despachado: alertId={AlertId} estado={Status}.",
            alert.Id,
            finalStatus
        );

        return finalStatus;
    }
}
