namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Pre-agregación diaria (rollup) para el Dashboard de Inventario &amp; Farmacia
/// (schema erp). Permite consultas O(1) sobre el período sin escanear las tablas
/// transaccionales (inventory_entries/inventory_exits y sus líneas).
/// Clave compuesta: (metric_date, metric_key, dimension_key).
/// </summary>
public sealed class InventoryDailyMetric
{
    /// <summary>Fecha calendario de la agregación.</summary>
    public DateOnly MetricDate { get; set; }

    /// <summary>
    /// Categoría de la métrica. Valores conocidos:
    /// "entries_count"   — cantidad de documentos de entrada de inventario
    /// "exits_count"     — cantidad de documentos de salida de inventario
    /// "units_entered"   — unidades totales recibidas (suma de cantidades de líneas de entrada)
    /// "units_exited"    — unidades totales despachadas (suma de cantidades de líneas de salida)
    /// "cost_entered"    — costo total de entradas (suma de TotalCost), en centavos
    /// "cost_exited"     — costo total de salidas (suma de UnitCost * Quantity), en centavos
    /// "product_entries" — unidades de entrada por producto (dimension_key = nombre del producto, ≤64 chars)
    /// "product_exits"   — unidades de salida por producto (dimension_key = nombre del producto)
    /// "category_units"  — unidades por categoría de producto (dimension_key = nombre de categoría)
    /// "category_cost"   — costo por categoría de producto, en centavos
    /// "category_exits"  — unidades de salida por categoría de producto
    /// </summary>
    public string MetricKey { get; set; } = "";

    /// <summary>
    /// Discriminador de la métrica. Depende de <see cref="MetricKey"/>:
    /// para "entries_count"/"exits_count"/"units_entered"/"units_exited"/
    /// "cost_entered"/"cost_exited" siempre es "total";
    /// para "product_entries"/"product_exits" es el nombre del producto (≤64 chars);
    /// para "category_units"/"category_cost"/"category_exits" es el nombre de la categoría.
    /// </summary>
    public string DimensionKey { get; set; } = "";

    /// <summary>
    /// Valor acumulado. Para métricas monetarias (cost_*), se almacena en
    /// centavos (long) para evitar problemas de precisión decimal.
    /// </summary>
    public long TotalCount { get; set; }

    public DateTime LastUpdatedAt { get; set; }
}