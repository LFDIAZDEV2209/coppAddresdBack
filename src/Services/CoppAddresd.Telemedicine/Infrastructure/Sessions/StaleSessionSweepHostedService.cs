using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Telemedicine.Infrastructure.Sessions;

/// <summary>
/// Ejecuta el barrido de sesiones estancadas cada <see cref="Interval"/>: las
/// citas <c>InProgress</c> cuyo fin programado pasó (más la gracia de la
/// ventana de sala) se cierran como <c>NoShow</c> (el paciente nunca ingresó) o
/// <c>Completed</c>. Un fallo del barrido nunca tumba el servicio: se registra
/// y el siguiente tick reintenta.
/// </summary>
public sealed class StaleSessionSweepHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<StaleSessionSweepHostedService> logger
) : BackgroundService
{
    internal static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
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
            var sweeper = scope.ServiceProvider.GetRequiredService<StaleSessionSweeper>();
            var closed = await sweeper.SweepAsync(DateTimeOffset.UtcNow, ct);
            if (closed > 0)
            {
                logger.LogInformation(
                    "Barrido de sesiones estancadas: {Closed} citas cerradas.",
                    closed
                );
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Apagado durante el barrido.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el barrido de sesiones estancadas.");
        }
    }
}
