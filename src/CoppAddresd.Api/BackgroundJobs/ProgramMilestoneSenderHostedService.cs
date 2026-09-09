using CoppAddresd.Application.Common;
using CoppAddresd.Application.Services.ProgramProgress;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Api.BackgroundJobs;

/// <summary>
/// Envío periódico de recordatorios proactivos de hito del programa (días 7,
/// 14, 21, 45, 60, 90): cada <c>Program:MilestoneSender:TickMinutes</c> (60)
/// corre <see cref="ProgramMilestoneSenderJob"/> — push FCM + mensaje
/// proactivo en el chat para inscripciones activas con hito en fecha y dentro
/// de la ventana local 9–21 del paciente. Apagable con
/// <c>Program:MilestoneSender:Enabled=false</c> (default true). Idempotente:
/// cada (inscripción, día) se notifica como máximo una vez
/// (<c>program_milestone_sends</c>).
/// </summary>
public sealed class ProgramMilestoneSenderHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<ProgramMilestoneSenderSettings> options,
    ILogger<ProgramMilestoneSenderHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation(
                "Recordatorios de hitos del programa deshabilitados (Program:MilestoneSender:Enabled=false).");
            return;
        }

        // Ventana 9–21 local del paciente: primer tick inmediato (idempotente,
        // barato sin candidatos) y luego TickMinutes entre pasadas. Un tick de
        // 0 o negativo no debe convertir el loop en un spin.
        var interval = TimeSpan.FromMinutes(Math.Clamp(settings.TickMinutes, 1, 1440));
        logger.LogInformation(
            "Recordatorios de hitos del programa activos: cada {Interval} minutos (ventana local {StartLocalHour}:00–{EndLocalHour}:00).",
            interval.TotalMinutes, settings.StartLocalHour, settings.EndLocalHour);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var job = scope.ServiceProvider.GetRequiredService<ProgramMilestoneSenderJob>();
                await job.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // El job nunca tira abajo el host: la próxima pasada reintenta.
                logger.LogError(ex, "Falló la pasada de recordatorios de hitos del programa.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
