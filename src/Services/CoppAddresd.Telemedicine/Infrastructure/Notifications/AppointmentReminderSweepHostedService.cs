using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Telemedicine.Infrastructure.Notifications;

/// <summary>
/// Ejecuta el barrido de recordatorios de citas F2 cada <see cref="Interval"/>
/// (delay inicial <see cref="InitialDelay"/>): las citas <c>Confirmed</c> que
/// entran a la ventana de 24 h o de 1 h reciben push/SMS del paciente y push del
/// profesional, con deduplicación en <c>tele.notification_dispatch</c>. Un fallo
/// del barrido nunca tumba el servicio: se registra y el siguiente tick
/// reintenta.
/// </summary>
public sealed class AppointmentReminderSweepHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<AppointmentReminderSweepHostedService> logger
) : BackgroundService
{
    internal static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    internal static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
            do
            {
                await RunSweepAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Apagado normal del servicio.
        }
    }

    private async Task RunSweepAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sweeper = scope.ServiceProvider.GetRequiredService<AppointmentReminderSweeper>();
            var sent = await sweeper.SweepAsync(DateTimeOffset.UtcNow, ct);
            if (sent > 0)
            {
                logger.LogInformation(
                    "Barrido de recordatorios: {Sent} notificaciones despachadas.",
                    sent
                );
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Apagado durante el barrido.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el barrido de recordatorios de citas.");
        }
    }
}
