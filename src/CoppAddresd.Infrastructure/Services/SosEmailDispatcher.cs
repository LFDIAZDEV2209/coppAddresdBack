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
/// Despacha el canal de correo de una alerta SOS (REQ-SOS-03): reutiliza el
/// texto del SMS (plantilla fija server-side) y lo envía al correo del
/// contacto de emergencia con dedupe durable <c>sos:email:{alertId}</c>.
/// Sin correo registrado el canal queda <c>SinDestino</c> (distinto de
/// <c>NoConfigurado</c> = proveedor de correo ausente) y NO se envía nada.
/// Nunca lanza: devuelve el estado final del canal.
/// </summary>
public sealed class SosEmailDispatcher(
    IEmailService emailService,
    INotificationDedupeRepository dedupe,
    AppDbContext dbContext,
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
            var processed = Enum.TryParse<SosChannelStatus>(
                existing.EmailStatus,
                ignoreCase: true,
                out var parsed
            )
                ? parsed
                : alert.EmailChannelStatus;

            // Backfill: alertas despachadas antes de persistir el canal en la
            // fila (la columna existe desde la migración AddSosEmailChannelStatus).
            await UpdateAlertChannelAsync(alert, processed, ct);
            return processed;
        }

        // Destino: snapshot congelado en la activación; fallback al perfil
        // para alertas creadas antes de que existiera el snapshot.
        var target =
            alert.DestinationEmail
            ?? SosSupport.NormalizeEmail(
                SosSupport.ExtractEmergencyContactEmail(alert.Patient?.EmergencyContact)
            );
        SosChannelStatus finalStatus;

        if (string.IsNullOrWhiteSpace(target))
        {
            finalStatus = SosChannelStatus.SinDestino;
            logger.LogInformation(
                "SOS correo sin destino registrado: alertId={AlertId}.",
                alert.Id
            );
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

        await UpdateAlertChannelAsync(alert, finalStatus, ct);

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
