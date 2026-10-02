using System.Diagnostics;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    ILogger<SosSmsDispatcher> logger
) : ISosSmsDispatcher
{
    /// <summary>Timeout estricto por intento (D3): 5 segundos.</summary>
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Hasta 2 reintentos tras el primer intento (3 intentos totales).</summary>
    private const int MaxRetries = 2;

    public async Task<SosChannelStatus> DispatchAsync(
        SosAlert alert,
        CancellationToken ct = default
    )
    {
        var dedupeKey = $"sos:sms:{alert.Id}";
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
            return alert.SmsChannelStatus;
        }

        var body = SosSmsTemplate.Build(alert);

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
                        attemptCts.Token
                    );

                    if (result.Success)
                    {
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
            ct
        );

        return finalStatus;
    }

    /// <summary>Actualiza el estado del canal en <c>app.sos_alerts</c> (transacción corta).</summary>
    private async Task UpdateAlertChannelAsync(
        SosAlert alert,
        Action<SosAlert> setStatus,
        Action<SosAlert> setTimestamp,
        Action<SosAlert> setDetail,
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
/// Plantilla del SMS de SOS enriquecido: 100% server-side. Incluye nombre
/// completo, edad, documento, signos vitales (demo), ubicación con enlace a
/// Maps y referencia correlacionada. El cuerpo NUNCA se registra en logs
/// (REQ-SOS-06).
/// </summary>
public static class SosSmsTemplate
{
    public static string Build(SosAlert alert)
    {
        var patient = alert.Patient;
        var name = SosMessageFormatting.FullName(patient, "Tu contacto");
        var age = SosMessageFormatting.Age(patient?.DateOfBirth);
        var document = string.IsNullOrWhiteSpace(patient?.DocumentNumber)
            ? null
            : patient!.DocumentNumber!.Trim();

        var lines = new List<string>
        {
            $"🚨 SOS CoppAdresd — {name}"
                + (age is null ? string.Empty : $", {age} años")
                + (document is null ? string.Empty : $", doc {document}")
                + $", activó una emergencia el {SosMessageFormatting.FormatAlertTime(alert.CreatedAt)}.",
        };

        var vitals = SosMessageFormatting.SmsVitals(alert);
        if (vitals is not null)
        {
            lines.Add(vitals);
        }

        var maps = SosMessageFormatting.MapsLink(alert);
        lines.Add(maps is null ? "Ubicación no disponible" : $"Ubicación: {maps}");

        lines.Add(
            $"Contacta a {SosMessageFormatting.FirstName(patient, name)} de inmediato. Ref {alert.Id.ToString()[..8]}."
        );

        return string.Join("\n", lines);
    }
}
