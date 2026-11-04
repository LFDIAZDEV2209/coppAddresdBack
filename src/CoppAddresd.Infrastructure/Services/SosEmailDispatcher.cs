using CoppAddresd.Application.Features.Sos;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Despacha el canal de correo de una alerta SOS (REQ-SOS-03): genera una
/// plantilla HTML propia y la envía al correo del
/// contacto de emergencia con dedupe durable <c>sos:email:{alertId}</c>.
/// Nunca lanza: devuelve el estado final del canal.
/// </summary>
public sealed class SosEmailDispatcher(
    IEmailService emailService,
    INotificationDedupeRepository dedupe,
    ILogger<SosEmailDispatcher> logger,
    ISosLocationLinkService? locationLinks = null,
    AppDbContext? dbContext = null
) : ISosEmailDispatcher
{
    private const string PendienteStatus = "pendiente";

    public Task<SosChannelStatus> DispatchAsync(SosAlert alert, CancellationToken ct = default)
        => SosRecipientDispatch.RunAsync(alert, "email", dedupe, dbContext, DispatchOneAsync, logger, ct);

    private async Task<SosChannelStatus> DispatchOneAsync(SosAlert alert, SosAlertRecipient? recipient, CancellationToken ct)
    {
        var dedupeKey = SosRecipientDispatch.Key("email", alert.Id, recipient);

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

        var target = recipient is null ? SosSupport.ExtractEmergencyContactEmail(alert.Patient?.EmergencyContact) : recipient.Email;
        SosChannelStatus finalStatus;

        if (string.IsNullOrWhiteSpace(target))
        {
            finalStatus = SosChannelStatus.NoConfigurado;
            logger.LogWarning("SOS correo sin destino configurado: alertId={AlertId}.", alert.Id);
        }
        else if (!emailService.IsConfigured)
        {
            finalStatus = SosChannelStatus.NoConfigurado;
            logger.LogWarning("SOS correo sin transporte configurado: alertId={AlertId}.", alert.Id);
        }
        else
        {
            var body = SosEmailTemplate.BuildHtml(alert, locationLinks?.CreateLink(alert));
            try
            {
                await emailService.SendEmailAsync(
                    target,
                    SosEmailTemplate.Subject,
                    body,
                    isHtml: true,
                    cancellationToken: ct
                );
                finalStatus = SosChannelStatus.Enviado;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                finalStatus = SosChannelStatus.Fallido;
                logger.LogError("SOS correo falló: alertId={AlertId}, exceptionType={ExceptionType}.", alert.Id, ex.GetType().Name);
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
