using CoppAddresd.Application.Features.Inventory.Events;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Infrastructure.Metrics;

/// <summary>
/// Procesa en segundo plano los eventos de métricas de Inventario &amp; Farmacia
/// (Dashboard #6, Fase 1 CQRS): drena la cola y ejecuta upserts atómicos
/// (INSERT ... ON CONFLICT DO UPDATE) sobre el rollup <c>erp.inventory_daily_metrics</c>.
/// Los costos se almacenan en centavos (long) para evitar precisión decimal.
/// </summary>
public sealed class InventoryMetricsProcessorHostedService(
    IInventoryMetricsQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<InventoryMetricsProcessorHostedService> logger) : BackgroundService
{
    /// <summary>Límite de la columna <c>dimension_key</c> (varchar(64)) en el rollup.</summary>
    private const int MaxDimensionKeyLength = 64;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Iniciando procesador en segundo plano de métricas de Inventario.");

        await foreach (var evt in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                switch (evt)
                {
                    case InventoryEntryCreatedMetricEvent e:
                        await ProcessEntryCreatedAsync(db, e, stoppingToken);
                        break;
                    case InventoryExitCreatedMetricEvent e:
                        await ProcessExitCreatedAsync(db, e, stoppingToken);
                        break;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error procesando evento de métrica de inventario: {@Event}", evt);
            }
        }
    }

    private static async Task ProcessEntryCreatedAsync(AppDbContext db, InventoryEntryCreatedMetricEvent e, CancellationToken ct)
    {
        var totalUnits = e.Lines.Sum(l => l.Quantity);

        // Centavos redondeados POR LÍNEA (AwayFromZero): el total es la suma de
        // los redondeos individuales para que cost_entered == Σ category_cost
        // (sin drift de centavos entre el documento y sus categorías).
        var totalCostCents = e.Lines.Sum(l => RoundCostCents(l.UnitCost, l.Quantity));

        // 1. Métricas globales (cantidad de entradas, unidades y costo)
        const string sqlGlobal = """
            INSERT INTO erp.inventory_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
            VALUES
                (@p0, 'entries_count', 'total', 1, NOW()),
                (@p0, 'units_entered', 'total', @p1, NOW()),
                (@p0, 'cost_entered',  'total', @p2, NOW())
            ON CONFLICT (metric_date, metric_key, dimension_key)
            DO UPDATE SET
                total_count = erp.inventory_daily_metrics.total_count + EXCLUDED.total_count,
                last_updated_at = NOW();
            """;

        await db.Database.ExecuteSqlRawAsync(sqlGlobal, [e.MetricDate, totalUnits, totalCostCents], ct);

        // 2. Métricas por producto y por categoría
        foreach (var line in e.Lines)
        {
            var productKey = TruncateDimensionKey(line.ProductName);
            var categoryKey = TruncateDimensionKey(
                string.IsNullOrWhiteSpace(line.Category) ? "general" : line.Category);
            var lineCostCents = RoundCostCents(line.UnitCost, line.Quantity);

            const string sqlLine = """
                INSERT INTO erp.inventory_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
                VALUES
                    (@p0, 'product_entries',  @p1, @p2, NOW()),
                    (@p0, 'category_units',   @p3, @p2, NOW()),
                    (@p0, 'category_cost',    @p3, @p4, NOW())
                ON CONFLICT (metric_date, metric_key, dimension_key)
                DO UPDATE SET
                    total_count = erp.inventory_daily_metrics.total_count + EXCLUDED.total_count,
                    last_updated_at = NOW();
                """;

            await db.Database.ExecuteSqlRawAsync(sqlLine,
                [e.MetricDate, productKey, line.Quantity, categoryKey, lineCostCents], ct);
        }
    }

    private static async Task ProcessExitCreatedAsync(AppDbContext db, InventoryExitCreatedMetricEvent e, CancellationToken ct)
    {
        var totalUnits = e.Lines.Sum(l => l.Quantity);

        // Mismo redondeo por línea que en entradas: cost_exited es la suma de los
        // redondeos individuales (coherente con el costo por línea del documento).
        var totalCostCents = e.Lines.Sum(l => RoundCostCents(l.UnitCost, l.Quantity));

        // 1. Métricas globales (cantidad de salidas, unidades y costo)
        const string sqlGlobal = """
            INSERT INTO erp.inventory_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
            VALUES
                (@p0, 'exits_count',  'total', 1, NOW()),
                (@p0, 'units_exited', 'total', @p1, NOW()),
                (@p0, 'cost_exited',  'total', @p2, NOW())
            ON CONFLICT (metric_date, metric_key, dimension_key)
            DO UPDATE SET
                total_count = erp.inventory_daily_metrics.total_count + EXCLUDED.total_count,
                last_updated_at = NOW();
            """;

        await db.Database.ExecuteSqlRawAsync(sqlGlobal, [e.MetricDate, totalUnits, totalCostCents], ct);

        // 2. Métricas por producto y por categoría (unidades de salida)
        foreach (var line in e.Lines)
        {
            var productKey = TruncateDimensionKey(line.ProductName);
            var categoryKey = TruncateDimensionKey(
                string.IsNullOrWhiteSpace(line.Category) ? "general" : line.Category);

            const string sqlLine = """
                INSERT INTO erp.inventory_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
                VALUES
                    (@p0, 'product_exits',  @p1, @p2, NOW()),
                    (@p0, 'category_exits', @p3, @p2, NOW())
                ON CONFLICT (metric_date, metric_key, dimension_key)
                DO UPDATE SET
                    total_count = erp.inventory_daily_metrics.total_count + EXCLUDED.total_count,
                    last_updated_at = NOW();
                """;

            await db.Database.ExecuteSqlRawAsync(sqlLine,
                [e.MetricDate, productKey, line.Quantity, categoryKey], ct);
        }
    }

    /// <summary>
    /// Trunca la clave de dimensión al límite de la columna <c>dimension_key</c>
    /// (varchar(64)) del rollup. Sin esto, una categoría de hasta 100 chars
    /// (Product.Category) dispara el error PostgreSQL 22001 a mitad del loop y
    /// deja datos parciales tras el upsert global.
    /// </summary>
    private static string TruncateDimensionKey(string value)
        => value.Length > MaxDimensionKeyLength ? value[..MaxDimensionKeyLength] : value;

    /// <summary>
    /// Costo de línea en centavos con redondeo AwayFromZero. Aplicado POR LÍNEA
    /// (nunca sobre totales) para que el costo del documento sea exactamente la
    /// suma de los costos por categoría, en ambos paths (rollup y fallback OLTP).
    /// </summary>
    private static long RoundCostCents(decimal unitCost, int quantity)
        => (long)Math.Round(unitCost * quantity * 100, MidpointRounding.AwayFromZero);
}