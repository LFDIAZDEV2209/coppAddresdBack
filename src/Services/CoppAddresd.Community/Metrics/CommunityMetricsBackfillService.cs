using System.Diagnostics;
using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Community.Metrics;

/// <summary>Resumen de una reconciliación del rollup de métricas de Comunidad.</summary>
/// <param name="DryRun">Verdadero si fue una simulación (no se escribió nada).</param>
/// <param name="DeletedRows">Filas de las 5 claves reemplazadas (conteo previo al rebuild).</param>
/// <param name="InsertedRows">Filas recomputadas desde el OLTP (suma de <paramref name="RowsPerKey"/>).</param>
/// <param name="RowsPerKey">Filas resultantes por <c>metric_key</c>.</param>
/// <param name="Duration">Tiempo total de la ejecución.</param>
/// <param name="Note">Nota de convergencia o de simulación.</param>
public sealed record CommunityMetricsBackfillResult(
    bool DryRun,
    long DeletedRows,
    long InsertedRows,
    IReadOnlyDictionary<string, long> RowsPerKey,
    TimeSpan Duration,
    string Note
);

/// <summary>
/// Reconstruye el rollup pre-agregado de métricas de Comunidad
/// (<c>community.community_daily_metrics</c>) desde el OLTP
/// (<c>community.posts/comments/likes/reposts</c>). Casos de uso: carga inicial,
/// reparación de deriva y recuperación de eventos perdidos por reinicios (la
/// cola de métricas es en memoria y por réplica). Idempotente y re-ejecutable:
/// cada corrida reemplaza el contenido de las 5 claves con el recálculo
/// autoritativo, nunca suma.
/// </summary>
public interface ICommunityMetricsBackfillService
{
    /// <summary>
    /// Recalcula y reemplaza las 5 claves del rollup desde el OLTP. Con
    /// <paramref name="dryRun"/> corre el rebuild dentro de una transacción y
    /// hace rollback: no escribe y reporta lo que habría escrito.
    /// </summary>
    Task<CommunityMetricsBackfillResult> BackfillAsync(bool dryRun, CancellationToken ct);
}

/// <summary>
/// Implementación set-based (<see cref="CommunityMetricsBackfillSql"/>).
/// Semántica autoritativa (overwrite completo por DELETE + INSERT): el
/// recálculo gana sobre los incrementos/decrementos del processor. Si llega
/// tráfico concurrente durante la ejecución (otras réplicas procesando
/// eventos), re-ejecutar converge — el segundo recálculo ya incluye esos
/// eventos.
///
/// Transacción explícita corta envuelta en <c>CreateExecutionStrategy</c>
/// (obligatorio si algún día se habilita <c>EnableRetryOnFailure</c>; el
/// registro de producción no configura retries hoy). Con <c>dryRun</c> hace
/// rollback y solo reporta conteos.
/// </summary>
public sealed class CommunityMetricsBackfillService(
    CommunityDbContext dbContext,
    ILogger<CommunityMetricsBackfillService> logger
) : ICommunityMetricsBackfillService
{
    public async Task<CommunityMetricsBackfillResult> BackfillAsync(bool dryRun, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        logger.LogInformation(
            "Iniciando reconciliación del rollup de métricas de Comunidad (dryRun: {DryRun}).",
            dryRun);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<CommunityMetricsBackfillResult>(
            async operationCt =>
            {
                await using var tx = await dbContext.Database.BeginTransactionAsync(operationCt);
                try
                {
                    var deletedRows = await dbContext.CommunityDailyMetrics
                        .CountAsync(
                            x => CommunityMetricsBackfillSql.MetricKeys.Contains(x.MetricKey),
                            operationCt);

                    await CommunityMetricsBackfillSql.RecomputeAllAsync(dbContext, operationCt);

                    var rowsPerKey = await dbContext.CommunityDailyMetrics.AsNoTracking()
                        .Where(x => CommunityMetricsBackfillSql.MetricKeys.Contains(x.MetricKey))
                        .GroupBy(x => x.MetricKey)
                        .Select(g => new { MetricKey = g.Key, RowCount = g.LongCount() })
                        .ToDictionaryAsync(x => x.MetricKey, x => x.RowCount, operationCt);

                    if (dryRun)
                    {
                        await tx.RollbackAsync(operationCt);
                    }
                    else
                    {
                        await tx.CommitAsync(operationCt);
                    }

                    sw.Stop();
                    var insertedRows = rowsPerKey.Values.Sum();

                    logger.LogInformation(
                        "Reconciliación del rollup de métricas de Comunidad terminada (dryRun: {DryRun}): {Deleted} filas reemplazadas por {Inserted} ({Detail}) en {Elapsed}.",
                        dryRun,
                        deletedRows,
                        insertedRows,
                        string.Join(
                            ", ",
                            rowsPerKey.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}")),
                        sw.Elapsed);

                    return new CommunityMetricsBackfillResult(
                        dryRun,
                        deletedRows,
                        insertedRows,
                        rowsPerKey,
                        sw.Elapsed,
                        dryRun
                            ? "Simulación: no se escribió nada."
                            : "Recálculo autoritativo desde OLTP. Si hubo tráfico concurrente, re-ejecutar una vez para converger.");
                }
                catch
                {
                    await tx.RollbackAsync(operationCt);
                    throw;
                }
            },
            ct);
    }
}
