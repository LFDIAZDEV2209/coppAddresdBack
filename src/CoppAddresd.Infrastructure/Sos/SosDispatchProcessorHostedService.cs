using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Infrastructure.Sos;

/// <summary>
/// Procesador del despacho de canales SOS (SMS + voz + correo + push) en segundo plano:
/// la respuesta al paciente (201) jamás espera a Twilio/FCM (D3/D4). Dos
/// fuentes de trabajo:
/// <list type="bullet">
/// <item>Cola caliente (<see cref="ISosDispatchQueue"/>) para alertas recién
/// creadas en esta réplica.</item>
/// <item>Sweep periódico (outbox durable): re-encola alertas con canales
/// <c>Pendiente</c> en <c>app.notification_dedupe_keys</c> — recupera las
/// que quedaron sin procesar por caída del proceso o drop de la cola.</item>
/// </list>
/// Toda la idempotencia vive en las filas de dedupe: los reintentos jamás
/// reenvían un canal ya enviado.
/// </summary>
public sealed class SosDispatchProcessorHostedService(
    ISosDispatchQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<SosDispatchProcessorHostedService> logger
) : BackgroundService
{
    /// <summary>Intervalo del sweep del outbox durable.</summary>
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    /// <summary>Edad mínima para re-encolar (evita competir con la cola caliente).</summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Iniciando procesador de despacho SOS (SMS + voz + correo + push).");

        var lastSweep = DateTimeOffset.UtcNow - SweepInterval;

        await foreach (var alertId in queue.ReadAllAsync(stoppingToken))
        {
            // Sweep intercalado con el consumo de la cola: el outbox se
            // recupera aunque la cola esté ocupada o vacía.
            if (DateTimeOffset.UtcNow - lastSweep >= SweepInterval)
            {
                lastSweep = DateTimeOffset.UtcNow;
                await SweepStaleAlertsAsync(stoppingToken);
            }

            try
            {
                await DispatchAsync(alertId, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error despachando canales SOS: alertId={AlertId}.", alertId);
            }
        }
    }

    /// <summary>Despacha SMS + voz + correo + push de una alerta (cada canal con su outbox).</summary>
    private async Task DispatchAsync(Guid alertId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISosAlertRepository>();
        var smsDispatcher = scope.ServiceProvider.GetRequiredService<ISosSmsDispatcher>();
        var voiceDispatcher = scope.ServiceProvider.GetRequiredService<ISosVoiceDispatcher>();
        var emailDispatcher = scope.ServiceProvider.GetRequiredService<ISosEmailDispatcher>();
        var pushDispatcher = scope.ServiceProvider.GetRequiredService<ISosPushDispatcher>();

        var alert = await repository.GetByIdAsync(alertId, ct);
        if (alert is null)
        {
            logger.LogWarning("SOS dispatch: alerta inexistente alertId={AlertId}.", alertId);
            return;
        }

        // Los canales corren en serie con su propio presupuesto de error:
        // el fallo de uno no aborta a los otros (el estado final se registra
        // en la alerta y en el outbox).
        await smsDispatcher.DispatchAsync(alert, ct);
        await voiceDispatcher.DispatchAsync(alert, ct);
        await emailDispatcher.DispatchAsync(alert, ct);
        await pushDispatcher.DispatchAsync(alert, ct);
    }

    /// <summary>
    /// Outbox durable: re-encola alertas cuya dedupe fila de canal (SMS, voz o correo)
    /// sigue <c>pendiente</c> y fueron creadas hace &gt; 2 min (el drop de la
    /// cola o una caída del proceso las habría saltado). Idempotente.
    /// </summary>
    private async Task SweepStaleAlertsAsync(CancellationToken ct)
    {
        try
        {
            var threshold = DateTimeOffset.UtcNow - StaleAfter;
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var staleKeys = await (
                from dedupe in dbContext.NotificationDedupeKeys.AsNoTracking()
                where
                    (
                        (
                            dedupe.DedupeKey.StartsWith("sos:sms:")
                            && dedupe.SmsStatus == nameof(SosChannelStatus.Pendiente).ToLowerInvariant()
                        )
                        || (
                            dedupe.DedupeKey.StartsWith("sos:voice:")
                            && dedupe.VoiceStatus == nameof(SosChannelStatus.Pendiente).ToLowerInvariant()
                        )
                        || (
                            dedupe.DedupeKey.StartsWith("sos:email:")
                            && dedupe.EmailStatus == nameof(SosChannelStatus.Pendiente).ToLowerInvariant()
                        )
                    )
                    && dedupe.CreatedAt < threshold
                select dedupe.DedupeKey
            )
                .Take(100)
                .ToListAsync(ct);

            foreach (var key in staleKeys)
            {
                var alertIdText = key.StartsWith("sos:sms:", StringComparison.Ordinal)
                    ? key["sos:sms:".Length..]
                    : key.StartsWith("sos:voice:", StringComparison.Ordinal)
                        ? key["sos:voice:".Length..]
                        : key["sos:email:".Length..];

                if (Guid.TryParse(alertIdText, out var alertId))
                {
                    await queue.EnqueueAsync(alertId, ct);
                }
            }

            if (staleKeys.Count > 0)
            {
                logger.LogInformation(
                    "SOS sweep: {Count} alertas con outbox pendiente re-encoladas.",
                    staleKeys.Count
                );
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(
                ex,
                "SOS sweep falló (best-effort): se reintenta en el próximo ciclo."
            );
        }
    }
}
