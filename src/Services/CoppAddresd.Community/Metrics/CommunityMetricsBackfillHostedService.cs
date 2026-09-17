using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Community.Metrics;

/// <summary>
/// Reconciliación del rollup de métricas al arrancar el servicio: reconstruye
/// las 5 claves de <c>community.community_daily_metrics</c> desde el OLTP antes
/// de que el processor de eventos empiece a drenar la cola (se registra antes
/// que <see cref="CommunityMetricsProcessorHostedService"/> en Program.cs).
///
/// Es idempotente (si el rollup ya está al día, el resultado es el mismo) y
/// cubre la pérdida de eventos en memoria por reinicios/cambios de instancia.
/// Cualquier error se loguea y NUNCA tumba el arranque: el rollup se puede
/// reparar después con el endpoint interno
/// <c>POST /api/v1/community/maintenance/reconcile-metrics</c>.
/// </summary>
public sealed class CommunityMetricsBackfillHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<CommunityMetricsBackfillHostedService> logger) : IHostedService
{
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var backfill = scope.ServiceProvider.GetRequiredService<ICommunityMetricsBackfillService>();
            var result = await backfill.BackfillAsync(dryRun: false, cancellationToken);

            logger.LogInformation(
                "Backfill de arranque de métricas de Comunidad completado: {Deleted} filas reemplazadas por {Inserted} en {Elapsed}.",
                result.DeletedRows,
                result.InsertedRows,
                result.Duration);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Backfill de arranque de métricas de Comunidad cancelado.");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error en el backfill de arranque de métricas de Comunidad; el servicio continúa (el rollup es reparable vía reconcile-metrics).");
        }
    }
}
