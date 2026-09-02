using CoppAddresd.Application.Features.ProgramProgress.Commands.ReconcileStreaks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Api.BackgroundJobs;

/// <summary>
/// Reconciliación nocturna de rachas (B12, T-28): job diario que recorre las
/// inscripciones activas y recalcula <c>streak_states</c> desde la fuente de
/// verdad (idempotente, solo corrige desviaciones). Hora de ejecución en UTC
/// configurable (<c>Program:Reconciliation:HourUtc</c>, default 3) y apagable
/// (<c>Program:Reconciliation:Enabled</c>, default true). El disparo manual
/// vive en <c>POST /program/maintenance/reconcile-streaks</c> (mismo camino).
/// </summary>
public sealed class ReconcileStreakHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ReconcileStreakHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = configuration.GetValue("Program:Reconciliation:Enabled", true);
        if (!enabled)
        {
            logger.LogInformation("Reconciliación de rachas deshabilitada (Program:Reconciliation:Enabled=false).");
            return;
        }

        var hourUtc = Math.Clamp(configuration.GetValue("Program:Reconciliation:HourUtc", 3), 0, 23);
        logger.LogInformation(
            "Reconciliación nocturna de rachas programada a las {Hour:00}:00 UTC.", hourUtc);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddHours(hourUtc);
            if (nextRun <= now)
            {
                nextRun = nextRun.AddDays(1);
            }

            await Task.Delay(nextRun - now, stoppingToken);

            try
            {
                using var scope = scopeFactory.CreateScope();
                var job = scope.ServiceProvider.GetRequiredService<ReconcileStreakJob>();
                var summary = await job.RunAsync(stoppingToken);
                logger.LogInformation(
                    "Reconciliación nocturna completada: scanned={Scanned} fixed={Fixed}",
                    summary.Scanned, summary.Fixed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // El job nunca tira abajo el host: la próxima pasada reintenta.
                logger.LogError(ex, "Fallo la reconciliación nocturna de rachas.");
            }
        }
    }
}
