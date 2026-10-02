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
/// Despacho del canal de voz de una alerta SOS: llamada TTS al contacto de
/// emergencia con plantilla fija 100% server-side (el cliente jamás envía
/// texto ni destino), timeout de 5 s por intento y UN reintento (decisión de
/// producto: evitar llamadas duplicadas al contacto). Deduplicación durable
/// vía <c>app.notification_dedupe_keys</c> con clave <c>sos:voice:{alertId}</c>:
/// un canal ya <c>Enviado</c> jamás se rellama y los reintentos del sistema no
/// duplican. Degradación: sin credenciales o ante timeout el canal queda
/// registrado y la alerta permanece Activa — nunca se propaga un 500.
/// Los logs NO incluyen teléfono, coordenadas ni el guion: solo alertId, canal,
/// estado y latencia.
/// </summary>
public sealed class SosVoiceDispatcher(
    IVoiceCaller voiceCaller,
    INotificationDedupeRepository dedupe,
    AppDbContext dbContext,
    ILogger<SosVoiceDispatcher> logger
) : ISosVoiceDispatcher
{
    /// <summary>Timeout estricto por intento: 5 segundos.</summary>
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Un único reintento tras el primer intento (2 intentos totales).</summary>
    private const int MaxRetries = 1;

    public async Task<SosChannelStatus> DispatchAsync(
        SosAlert alert,
        CancellationToken ct = default
    )
    {
        var dedupeKey = $"sos:voice:{alert.Id}";
        var existing = await dedupe.GetByKeyAsync(dedupeKey, ct);
        if (
            existing?.VoiceStatus is not null
            && existing.VoiceStatus != nameof(SosChannelStatus.Pendiente).ToLowerInvariant()
        )
        {
            // El canal ya fue procesado (sent/failed/timeout/disabled): el
            // outbox evita rellamar aunque el procesador reintente.
            logger.LogInformation(
                "SOS voz ya procesado (estado={Status}): alertId={AlertId}.",
                existing.VoiceStatus,
                alert.Id
            );
            return alert.VoiceChannelStatus;
        }

        var sayText = SosVoiceTemplate.Build(alert);

        var finalStatus = SosChannelStatus.Fallido;
        string? detail = null;

        if (!voiceCaller.IsConfigured)
        {
            finalStatus = SosChannelStatus.NoConfigurado;
            detail = "voice:not_configured";
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
                    var result = await voiceCaller.CallAsync(
                        alert.DestinationPhoneE164,
                        sayText,
                        "es-US",
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
                    detail = "voice:provider";
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Timeout del intento (el token global sigue activo).
                    finalStatus = SosChannelStatus.Timeout;
                    detail = "voice:timeout";
                    logger.LogWarning(
                        "SOS voz timeout en intento {Attempt}: alertId={AlertId}.",
                        attempt + 1,
                        alert.Id
                    );
                }

                if (attempt < MaxRetries)
                {
                    // Backoff corto (1s) con el token global: el procesador
                    // nunca queda colgado.
                    await Task.Delay(TimeSpan.FromSeconds(1 << attempt), ct);
                }
            }

            stopwatch.Stop();
            logger.LogInformation(
                "SOS voz despachado: alertId={AlertId}, status={Status}, latencyMs={LatencyMs}.",
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
            smsStatus: null,
            voiceStatus: finalStatus.ToString().ToLowerInvariant(),
            ct: ct
        );

        await UpdateAlertChannelAsync(
            alert,
            a => a.VoiceChannelStatus = finalStatus,
            a => a.VoiceUpdatedAt = DateTime.UtcNow,
            a => a.VoiceDetail = detail,
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
                "No se pudo persistir el estado del canal de voz: alertId={AlertId}.",
                alert.Id
            );
        }
    }
}

/// <summary>
/// Plantilla de la llamada de voz SOS enriquecida: 100% server-side, en
/// español, sin URLs ni coordenadas (la ubicación se indica por SMS). Incluye
/// nombre completo, edad, documento y signos vitales (demo); recomienda
/// revisar el mensaje de texto y llamar al 911. El guion se repite una vez y
/// NUNCA se registra en logs.
/// </summary>
public static class SosVoiceTemplate
{
    public static string Build(SosAlert alert)
    {
        var patient = alert.Patient;
        var name = SosMessageFormatting.FullName(patient);
        var age = SosMessageFormatting.Age(patient?.DateOfBirth);
        var document = string.IsNullOrWhiteSpace(patient?.DocumentNumber)
            ? null
            : patient!.DocumentNumber!.Trim();

        var identity = name
            + (age is null ? string.Empty : $", {age} años")
            + (document is null ? string.Empty : $", documento {document}");

        var vitals = SosMessageFormatting.VoiceVitals(alert);

        var message =
            $"Alerta de emergencia de Copp Adresd. {identity} activó una alerta SOS y necesita ayuda inmediata. "
            + (vitals is null ? string.Empty : vitals + " ")
            + "Revisa el mensaje de texto con su ubicación. "
            + "Comuníquese de inmediato y, de ser necesario, llame al 911.";

        return $"{message} Repito: {message}";
    }
}
