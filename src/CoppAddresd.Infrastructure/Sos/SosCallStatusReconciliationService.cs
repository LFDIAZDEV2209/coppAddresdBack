using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio.Clients;
using Twilio.Rest.Api.V2010.Account;

namespace CoppAddresd.Infrastructure.Sos;

/// <summary>
/// Reconciliador del canal de voz de SOS: si un callback de estado de Twilio
/// se pierde (túnel/red), la alerta quedaría "en curso" para siempre. Cada
/// minuto consulta Twilio por las llamadas no finales y persiste el estado
/// real (completed / no-answer / busy / failed / canceled) + duración.
/// Best-effort: nunca tumba el host ni bloquea el despacho.
/// </summary>
public sealed class SosCallStatusReconciliationService(
    IServiceScopeFactory scopeFactory,
    IOptions<VoiceSettings> voiceSettings,
    ILogger<SosCallStatusReconciliationService> logger
) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Reconciliación de llamadas SOS falló (best-effort).");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        var settings = voiceSettings.Value;
        if (!settings.IsConfigured)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISosAlertRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var threshold = DateTime.UtcNow - StaleAfter;
        var stuck = await repository.ListStuckVoiceCallsAsync(threshold, ct);
        if (stuck.Count == 0)
        {
            return;
        }

        var client = new TwilioRestClient(settings.AccountSid, settings.AuthToken);
        var reconciled = 0;

        foreach (var alert in stuck)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var call = await CallResource.FetchAsync(
                    pathSid: alert.VoiceProviderCallId!,
                    client: client
                );
                var status = call.Status?.ToString()?.ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(status))
                {
                    continue;
                }

                var tracked = await dbContext.SosAlerts.FirstOrDefaultAsync(
                    x => x.Id == alert.Id,
                    ct
                );
                if (tracked is null)
                {
                    continue;
                }

                tracked.VoiceCallStatus = status;
                if (int.TryParse(call.Duration, out var duration))
                {
                    tracked.VoiceDurationSeconds = duration;
                }
                tracked.VoiceUpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(ct);
                reconciled++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "No se pudo reconciliar la llamada SOS: alertId={AlertId}.",
                    alert.Id
                );
            }
        }

        if (reconciled > 0)
        {
            logger.LogInformation(
                "Reconciliación SOS: {Count} llamadas actualizadas desde Twilio.",
                reconciled
            );
        }
    }
}
