using CoppAddresd.Application.Features.Sos;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Despacha el canal de correo de una alerta SOS (REQ-SOS-03): genera una
/// plantilla HTML propia y la envía al correo del
/// contacto de emergencia con dedupe durable <c>sos:email:{alertId}</c>.
/// Sin correo registrado el canal queda <c>SinDestino</c> (distinto de
/// <c>NoConfigurado</c> = proveedor de correo ausente) y NO se envía nada.
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
    // Compatibilidad con consumidores que ya inyectaban el contexto antes del logger.
    public SosEmailDispatcher(IEmailService emailService, INotificationDedupeRepository dedupe,
        AppDbContext dbContext, ILogger<SosEmailDispatcher> logger)
        : this(emailService, dedupe, logger, null, dbContext) { }

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
            var processed = Enum.TryParse<SosChannelStatus>(
                existing.EmailStatus,
                ignoreCase: true,
                out var parsed
            )
                ? parsed
                : alert.EmailChannelStatus;

            // Backfill: alertas despachadas antes de persistir el canal en la
            // fila (la columna existe desde la migración AddSosEmailChannelStatus).
            if (recipient is null) await UpdateAlertChannelAsync(alert, processed, ct);
            return processed;
        }

        var target = recipient is not null ? recipient.Email :
            alert.DestinationEmail ?? SosSupport.NormalizeEmail(
                SosSupport.ExtractEmergencyContactEmail(alert.Patient?.EmergencyContact));
        SosChannelStatus finalStatus;

        if (string.IsNullOrWhiteSpace(target))
        {
            finalStatus = SosChannelStatus.SinDestino;
            logger.LogInformation(
                "SOS correo sin destino registrado: alertId={AlertId}.",
                alert.Id
            );
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

        if (recipient is null) await UpdateAlertChannelAsync(alert, finalStatus, ct);

        logger.LogInformation(
            "SOS correo despachado: alertId={AlertId} estado={Status}.",
            alert.Id,
            finalStatus
        );

        return finalStatus;
    }

    /// <summary>Actualiza el estado del canal de correo en <c>app.sos_alerts</c> (transacción corta).</summary>
    private async Task UpdateAlertChannelAsync(
        SosAlert alert,
        SosChannelStatus status,
        CancellationToken ct
    )
    {
        alert.EmailChannelStatus = status;
        if (dbContext is null) return;
        try
        {
            var tracked = await dbContext.SosAlerts.FirstOrDefaultAsync(x => x.Id == alert.Id, ct);
            if (tracked is null)
            {
                return;
            }

            tracked.EmailChannelStatus = status;
            tracked.EmailUpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is NpgsqlException)
        {
            // La persistencia del estado del canal es best-effort: no debe
            // tumbar el procesador ni la respuesta ya dada al paciente.
            logger.LogWarning(
                ex,
                "No se pudo persistir el estado del canal de correo: alertId={AlertId}.",
                alert.Id
            );
        }
    }
}
