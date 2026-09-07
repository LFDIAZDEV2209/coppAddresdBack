# Plan de Implementación — Dashboard #6: Inventario & Farmacia
## CQRS Pre-aggregation con Channel Pattern (Fase 1)

> **Contexto crítico para el agente ejecutor**: Este plan es autosuficiente. No se necesita ningún contexto externo. Leer cada sección en orden antes de escribir una sola línea de código.

---

## 0. Arquitectura de referencia — El patrón en este proyecto

El proyecto ya implementa este patrón en 4 módulos. El flujo es siempre el mismo:

```
Command Handler (Application Layer)
    │
    ├─► 1. Escribe en la base de datos (EF Core → PostgreSQL OLTP)
    │
    └─► 2. Encola evento en memoria (IAsyncEnumerable<T> via Channel<T>)
                    │
                    ▼
        BackgroundService (HostedService)
            └─► Atomic UPSERT → PostgreSQL rollup table
                    INSERT ... ON CONFLICT DO UPDATE SET total_count = total_count + delta
```

**Regla crítica**: El enqueue NO bloquea al usuario. La latencia de escritura HTTP no se ve afectada.
**Regla crítica**: El `GetAnalyticsAsync` actual en `InventoryRepository.cs` hace full-table-scan cargando TODO en memoria. Este plan lo reemplaza con lecturas O(1) sobre la tabla rollup.

---

## 1. Contexto del módulo — Inventory

### 1.1. Proyectos involucrados
- **Application**: `src/CoppAddresd.Application/Features/Inventory/` — Handlers MediatR (no HotChocolate).
- **Infrastructure**: `src/CoppAddresd.Infrastructure/Repositories/InventoryRepository.cs` — implementación del repositorio.
- **Domain Entities**: `src/CoppAddresd.Domain/Entities/` — `InventoryEntry.cs`, `InventoryExit.cs`, `InventoryEntryLine.cs`, `InventoryExitLine.cs`, `InventoryMovement.cs`, `Product.cs`.
- **Interface del repositorio**: `src/CoppAddresd.Application/Interfaces/IInventoryRepository.cs`.
- **DbContext**: `AppDbContext` en `src/CoppAddresd.Infrastructure/Persistence/AppDbContext.cs` — schema `erp.` (confirmar que Products/Entries/Exits están en `erp.`).

### 1.2. Handlers de escritura EXISTENTES (puntos de inyección)
Todos están en `src/CoppAddresd.Application/Features/Inventory/`:

| Handler | Archivo | Acción |
|---|---|---|
| `CreateEntryCommandHandler` | `EntryCommands.cs` | Crea `InventoryEntry` + `InventoryEntryLine[]`, guarda con `repository.CreateEntryAsync(entry, ct)` |
| `CreateExitCommandHandler` | `ExitCommands.cs` | Crea `InventoryExit` + `InventoryExitLine[]`, guarda con `repository.CreateExitAsync(exit, ct)` |

> **Nota**: No existe `UpdateEntryCommandHandler` ni `DeleteEntryCommandHandler`. Los movimientos de stock son siempre append-only.

### 1.3. GetAnalyticsAsync — Problema actual
**Ubicación**: `InventoryRepository.cs` líneas 203–265.

El problema es que:
1. Carga TODOS los `Products` con `ToListAsync()` — O(n) productos.
2. Carga TODAS las `InventoryEntries` con `Include(e => e.Lines)` — O(n) entradas × líneas.
3. Carga TODAS las `InventoryExits` con `Include(e => e.Lines)` — O(n) salidas × líneas.
4. Hace los cálculos en memoria en C#.

Con el rollup, los campos del `InventoryAnalyticsDto` que vienen de aggregations (totales de entradas, salidas, unidades, series temporales) se leerán en O(1). Los campos de estado del inventario actual (`TotalValue`, `LowStock`, `OutOfStock`, `ExpiringSoon`, `Expired`) requieren estado puntual del producto y se mantienen como queries ligeras sobre `Products`.

### 1.4. Estructura del InventoryAnalyticsDto existente
```csharp
public record InventoryAnalyticsDto(
    decimal TotalValue,         // Calculado sobre productos (estado actual, no rollup)
    int ActiveProducts,         // Calculado sobre productos (estado actual)
    int LowStock,               // Calculado sobre productos (estado actual)
    int OutOfStock,             // Calculado sobre productos (estado actual)
    int ExpiringSoon,           // Calculado sobre productos (estado actual)
    int Expired,                // Calculado sobre productos (estado actual)
    int Entries,                // → ROLLUP: inventory_daily_metrics / "entries_count" / "total"
    int Exits,                  // → ROLLUP: inventory_daily_metrics / "exits_count" / "total"
    int UnitsEntered,           // → ROLLUP: inventory_daily_metrics / "units_entered" / "total"
    int UnitsExited,            // → ROLLUP: inventory_daily_metrics / "units_exited" / "total"
    IReadOnlyList<MovementSeriesPoint> MovementSeries,  // → ROLLUP: por fecha
    IReadOnlyList<TopMovingProduct> TopMoving,          // → ROLLUP: por producto (metric_key="top_product")
    IReadOnlyList<CategoryValue> CategoryValue,         // → ROLLUP: por categoría (metric_key="category_value")
    IReadOnlyList<ProductListItemDto> Products          // → Se mantiene como query a Products table
);
```

---

## 2. Diseño de la tabla rollup

### 2.1. Entidad de dominio: `InventoryDailyMetric`
**Ubicación**: `src/CoppAddresd.Domain/Entities/InventoryDailyMetric.cs`

```csharp
namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Pre-aggregated daily rollup for ERP Inventory Dashboard analytics.
/// Keyed by (metric_date, metric_key, dimension_key).
/// </summary>
public class InventoryDailyMetric
{
    /// <summary>Calendar date of the aggregation.</summary>
    public DateOnly MetricDate { get; set; }

    /// <summary>
    /// Metric category. Known values:
    /// "entries_count"   — number of inventory entry documents
    /// "exits_count"     — number of inventory exit documents
    /// "units_entered"   — total units received (sum of entry lines quantities)
    /// "units_exited"    — total units dispatched (sum of exit lines quantities)
    /// "cost_entered"    — total cost of entries (sum of TotalCost)
    /// "cost_exited"     — total cost of exits (sum of exit lines UnitCost * Quantity)
    /// "product_entries" — per-product unit entries (dimension_key = product name, truncated to 64 chars)
    /// "product_exits"   — per-product unit exits (dimension_key = product name)
    /// "category_units"  — units per product category (dimension_key = category name)
    /// </summary>
    public string MetricKey { get; set; } = "";

    /// <summary>
    /// Discriminator for the metric. Values depend on MetricKey:
    /// For "entries_count" / "exits_count" / "units_entered" / "units_exited" / "cost_entered" / "cost_exited": always "total".
    /// For "product_entries" / "product_exits": product name (≤64 chars).
    /// For "category_units": category name (e.g. "Suplementos", "Medicamentos").
    /// </summary>
    public string DimensionKey { get; set; } = "";

    /// <summary>Accumulated value. For monetary metrics (cost_*), stored as cents (long) to avoid decimal precision issues.</summary>
    public long TotalCount { get; set; }

    public DateTime LastUpdatedAt { get; set; }
}
```

> **Nota sobre dinero**: Para costos (`cost_entered`, `cost_exited`), multiplicar el valor `decimal` por 100 antes de guardar como `long` (centavos). Al leer, dividir por 100m para recuperar el decimal. Esto evita columnas numéricas en la tabla de rollup.

### 2.2. EF Core Configuration
**Ubicación**: `src/CoppAddresd.Infrastructure/Configurations/InventoryDailyMetricConfiguration.cs`

```csharp
using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

public class InventoryDailyMetricConfiguration : IEntityTypeConfiguration<InventoryDailyMetric>
{
    public void Configure(EntityTypeBuilder<InventoryDailyMetric> builder)
    {
        // CRÍTICO: Verificar el schema real de Products/Entries en AppDbContext.
        // Si Products está en schema "erp.", usar "erp" aquí también.
        // Si está en schema "app.", ajustar.
        builder.ToTable("inventory_daily_metrics", "erp");

        builder.HasKey(x => new { x.MetricDate, x.MetricKey, x.DimensionKey });

        builder.Property(x => x.MetricDate).HasColumnName("metric_date");
        builder.Property(x => x.MetricKey).HasColumnName("metric_key").HasMaxLength(64).IsRequired();
        builder.Property(x => x.DimensionKey).HasColumnName("dimension_key").HasMaxLength(64).IsRequired();
        builder.Property(x => x.TotalCount).HasColumnName("total_count");
        builder.Property(x => x.LastUpdatedAt).HasColumnName("last_updated_at");

        builder.HasIndex(x => new { x.MetricKey, x.DimensionKey, x.MetricDate })
            .HasDatabaseName("ix_inventory_daily_metrics_key_dim_date");
        builder.HasIndex(x => x.MetricDate)
            .HasDatabaseName("ix_inventory_daily_metrics_date");
    }
}
```

> **⚠️ ACCIÓN REQUERIDA ANTES DE CONTINUAR**: Abrir `AppDbContext.cs` y confirmar en qué schema se mapean `Products`, `InventoryEntries`, `InventoryExits`. Buscar `ToTable(` en las configuraciones de EF de esos entities. Usar el mismo schema para `inventory_daily_metrics`.

### 2.3. Registrar el DbSet en AppDbContext
**Ubicación**: `src/CoppAddresd.Infrastructure/Persistence/AppDbContext.cs`

Agregar:
```csharp
public DbSet<InventoryDailyMetric> InventoryDailyMetrics => Set<InventoryDailyMetric>();
```

### 2.4. SQL del upsert atómico (referencia)
```sql
-- Para métricas simples (entradas, salidas, unidades, costos):
INSERT INTO erp.inventory_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
VALUES (@date, @metricKey, 'total', @increment, NOW())
ON CONFLICT (metric_date, metric_key, dimension_key)
DO UPDATE SET
    total_count = erp.inventory_daily_metrics.total_count + EXCLUDED.total_count,
    last_updated_at = NOW();

-- Para métricas por producto (top moving):
INSERT INTO erp.inventory_daily_metrics (metric_date, metric_key, dimension_key, total_count, last_updated_at)
VALUES (@date, 'product_entries', @productName, @unitsQuantity, NOW())
ON CONFLICT (metric_date, metric_key, dimension_key)
DO UPDATE SET
    total_count = erp.inventory_daily_metrics.total_count + EXCLUDED.total_count,
    last_updated_at = NOW();
```

---

## 3. Eventos de métricas

### 3.1. Interfaz marcadora e implementaciones
**Ubicación**: `src/CoppAddresd.Application/Features/Inventory/Events/IInventoryMetricEvent.cs`

```csharp
namespace CoppAddresd.Application.Features.Inventory.Events;

/// <summary>Marker interface for all Inventory metric events.</summary>
public interface IInventoryMetricEvent;

/// <summary>
/// Fired when an InventoryEntry (stock-in document) is created.
/// Carries all line details needed to update rollup without re-querying DB.
/// </summary>
public sealed record InventoryEntryCreatedMetricEvent(
    Guid EntryId,
    DateOnly MetricDate,
    decimal TotalCost,
    IReadOnlyList<InventoryMetricLine> Lines
) : IInventoryMetricEvent;

/// <summary>
/// Fired when an InventoryExit (stock-out document) is created.
/// </summary>
public sealed record InventoryExitCreatedMetricEvent(
    Guid ExitId,
    DateOnly MetricDate,
    IReadOnlyList<InventoryMetricLine> Lines
) : IInventoryMetricEvent;

/// <summary>Line detail used in metric events (denormalized to avoid re-querying).</summary>
public sealed record InventoryMetricLine(
    string ProductName,   // Truncated to 64 chars for DimensionKey
    string Category,      // Product category name (needs to be loaded from Product entity)
    int Quantity,
    decimal UnitCost
);
```

> **CRÍTICO**: `InventoryMetricLine.Category` requiere que el handler cargue la categoría del producto. El `CreateEntryCommandHandler` actualmente NO carga el `Product` entity (solo almacena `ProductName` como string). Hay dos opciones:
> - **Opción A (recomendada)**: Cargar el `Product` desde el repositorio dentro del handler antes de crear las lines (ya tiene `IInventoryRepository` inyectado).
> - **Opción B**: Omitir `category` en el evento y registrar `category_units` como `"unknown"`, luego hacer reconciliación nocturna. Más simple, menos preciso.

---

## 4. Cola en memoria

### 4.1. Interface
**Ubicación**: `src/CoppAddresd.Application/Interfaces/IInventoryMetricsQueue.cs`

```csharp
using CoppAddresd.Application.Features.Inventory.Events;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// In-memory channel queue for Inventory metric events.
/// Non-blocking enqueue; background worker drains the channel.
/// </summary>
public interface IInventoryMetricsQueue
{
    ValueTask EnqueueAsync(IInventoryMetricEvent metricEvent, CancellationToken ct = default);
    IAsyncEnumerable<IInventoryMetricEvent> ReadAllAsync(CancellationToken ct = default);
}
```

### 4.2. Implementación
**Ubicación**: `src/CoppAddresd.Infrastructure/Metrics/InventoryMetricsQueue.cs`

```csharp
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CoppAddresd.Application.Features.Inventory.Events;
using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Infrastructure.Metrics;

public sealed class InventoryMetricsQueue : IInventoryMetricsQueue
{
    private readonly Channel<IInventoryMetricEvent> _channel =
        Channel.CreateUnbounded<IInventoryMetricEvent>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(IInventoryMetricEvent metricEvent, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(metricEvent, ct);

    public async IAsyncEnumerable<IInventoryMetricEvent> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var e in _channel.Reader.ReadAllAsync(ct))
            yield return e;
    }
}
```

---

## 5. Background Processor (HostedService)

**Ubicación**: `src/CoppAddresd.Infrastructure/Metrics/InventoryMetricsProcessorHostedService.cs`

```csharp
using CoppAddresd.Application.Features.Inventory.Events;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Infrastructure.Metrics;

public sealed class InventoryMetricsProcessorHostedService(
    IInventoryMetricsQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<InventoryMetricsProcessorHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Inventory metrics background processor started.");

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
                logger.LogError(ex, "Error processing inventory metric event: {@Event}", evt);
            }
        }
    }

    private static async Task ProcessEntryCreatedAsync(AppDbContext db, InventoryEntryCreatedMetricEvent e, CancellationToken ct)
    {
        var totalUnits = e.Lines.Sum(l => l.Quantity);
        var totalCostCents = (long)(e.TotalCost * 100);

        // 1. Global metrics (entries count, units, cost)
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

        // 2. Per-product and per-category metrics
        foreach (var line in e.Lines)
        {
            var productKey = line.ProductName.Length > 64 ? line.ProductName[..64] : line.ProductName;
            var categoryKey = string.IsNullOrWhiteSpace(line.Category) ? "general" : line.Category;
            var lineCostCents = (long)(line.UnitCost * line.Quantity * 100);

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
        var totalCostCents = e.Lines.Sum(l => (long)(l.UnitCost * l.Quantity * 100));

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

        foreach (var line in e.Lines)
        {
            var productKey = line.ProductName.Length > 64 ? line.ProductName[..64] : line.ProductName;
            var categoryKey = string.IsNullOrWhiteSpace(line.Category) ? "general" : line.Category;

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
}
```

---

## 6. Puntos de inyección en los Command Handlers

### 6.1. `CreateEntryCommandHandler` — `EntryCommands.cs`

Cambios requeridos:
1. Agregar `IInventoryMetricsQueue? metricsQueue = null` como parámetro del primary constructor.
2. DESPUÉS de `repository.CreateEntryAsync(entry, ct)`, cargar los productos para obtener la categoría (si se eligió Opción A en sección 3).
3. Enqueue del evento.

```csharp
public sealed class CreateEntryCommandHandler(
    IInventoryRepository repository,
    IInventoryMetricsQueue? metricsQueue = null
) : IRequestHandler<CreateEntryCommand, InventoryEntryDto>
{
    public async Task<InventoryEntryDto> Handle(CreateEntryCommand request, CancellationToken ct)
    {
        var r = request.Request;
        // ... (código existente de creación sin cambios) ...
        await repository.CreateEntryAsync(entry, ct);

        // CQRS Pre-aggregation (0ms overhead — non-blocking)
        if (metricsQueue != null)
        {
            // Cargar categorías de productos para enriquecer el evento
            var metricLines = await BuildMetricLinesAsync(r.Lines, repository, ct);

            await metricsQueue.EnqueueAsync(new InventoryEntryCreatedMetricEvent(
                entry.Id,
                DateOnly.FromDateTime(entry.Date),
                entry.TotalCost,
                metricLines
            ), ct);
        }

        return InventoryEntryDto.FromEntity(entry);
    }

    private static async Task<IReadOnlyList<InventoryMetricLine>> BuildMetricLinesAsync(
        IReadOnlyList<InventoryEntryLineInput> lines,
        IInventoryRepository repository,
        CancellationToken ct)
    {
        var result = new List<InventoryMetricLine>();
        foreach (var l in lines)
        {
            var product = await repository.GetProductByIdAsync(l.ProductId, ct);
            result.Add(new InventoryMetricLine(
                l.ProductName,
                product?.Category ?? "general",
                l.Quantity,
                l.UnitCost
            ));
        }
        return result;
    }
}
```

### 6.2. `CreateExitCommandHandler` — `ExitCommands.cs`

Misma lógica. El `InventoryExit` no tiene `TotalCost` directo, calcularlo desde las líneas:
```csharp
// Enqueue:
if (metricsQueue != null)
{
    var metricLines = await BuildMetricLinesAsync(r.Lines, repository, ct);
    await metricsQueue.EnqueueAsync(new InventoryExitCreatedMetricEvent(
        exit.Id,
        DateOnly.FromDateTime(exit.Date),
        metricLines
    ), ct);
}
```

---

## 7. Optimización de GetAnalyticsAsync en InventoryRepository

**Ubicación**: `src/CoppAddresd.Infrastructure/Repositories/InventoryRepository.cs`, método `GetAnalyticsAsync` (líneas 203–265).

Reemplazar la implementación actual con:

```csharp
public async Task<InventoryAnalyticsDto> GetAnalyticsAsync(
    DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default)
{
    var to = dateTo ?? DateOnly.FromDateTime(DateTime.Today);
    var from = dateFrom ?? to.AddDays(-29);

    // ── FAST PATH: Lee desde rollup O(1) ───────────────────────────────────
    var rollupMetrics = await dbContext.InventoryDailyMetrics.AsNoTracking()
        .Where(x => x.MetricDate >= from && x.MetricDate <= to)
        .ToListAsync(ct);

    int entries, exits, unitsEntered, unitsExited;
    decimal costEntered, costExited;
    IReadOnlyList<MovementSeriesPoint> series;
    IReadOnlyList<TopMovingProduct> topMoving;
    IReadOnlyList<CategoryValue> categoryValue;

    if (rollupMetrics.Count > 0)
    {
        // Métricas de totales del período
        entries = (int)(rollupMetrics
            .Where(x => x.MetricKey == "entries_count" && x.DimensionKey == "total")
            .Sum(x => x.TotalCount));
        exits = (int)(rollupMetrics
            .Where(x => x.MetricKey == "exits_count" && x.DimensionKey == "total")
            .Sum(x => x.TotalCount));
        unitsEntered = (int)(rollupMetrics
            .Where(x => x.MetricKey == "units_entered" && x.DimensionKey == "total")
            .Sum(x => x.TotalCount));
        unitsExited = (int)(rollupMetrics
            .Where(x => x.MetricKey == "units_exited" && x.DimensionKey == "total")
            .Sum(x => x.TotalCount));
        costEntered = rollupMetrics
            .Where(x => x.MetricKey == "cost_entered" && x.DimensionKey == "total")
            .Sum(x => x.TotalCount) / 100m;
        costExited = rollupMetrics
            .Where(x => x.MetricKey == "cost_exited" && x.DimensionKey == "total")
            .Sum(x => x.TotalCount) / 100m;

        // Serie temporal diaria desde rollup
        var rangeDays = to.DayNumber - from.DayNumber + 1;
        series = Enumerable.Range(0, rangeDays).Select(i =>
        {
            var day = from.AddDays(i);
            var daysEntries = rollupMetrics
                .Where(x => x.MetricDate == day && x.MetricKey == "units_entered" && x.DimensionKey == "total")
                .Sum(x => (int)x.TotalCount);
            var daysExits = rollupMetrics
                .Where(x => x.MetricDate == day && x.MetricKey == "units_exited" && x.DimensionKey == "total")
                .Sum(x => (int)x.TotalCount);
            return new MovementSeriesPoint(day.ToString("MMM d"), daysEntries, daysExits);
        }).ToList();

        // Top moving products
        topMoving = rollupMetrics
            .Where(x => x.MetricKey == "product_entries")
            .GroupBy(x => x.DimensionKey)
            .Select(g => new TopMovingProduct(g.Key, (int)g.Sum(x => x.TotalCount)))
            .OrderByDescending(t => t.Quantity)
            .Take(5)
            .ToList();

        // Category value (from rollup: category_cost = cost in cents)
        categoryValue = rollupMetrics
            .Where(x => x.MetricKey == "category_cost")
            .GroupBy(x => x.DimensionKey)
            .Select(g => new CategoryValue(g.Key, g.Sum(x => x.TotalCount) / 100m))
            .OrderByDescending(c => c.Value)
            .ToList();
    }
    else
    {
        // ── FALLBACK OLTP (sistema recién iniciado, sin datos en rollup) ────
        // (código original de cálculo en memoria — MANTENER SIN CAMBIOS)
        var fromDate = from.ToDateTime(TimeOnly.MinValue);
        var toDate = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var entriesList = await dbContext.InventoryEntries.AsNoTracking()
            .Include(e => e.Lines)
            .Where(e => e.Date >= fromDate && e.Date < toDate)
            .ToListAsync(ct);
        var exitsList = await dbContext.InventoryExits.AsNoTracking()
            .Include(e => e.Lines)
            .Where(e => e.Date >= fromDate && e.Date < toDate)
            .ToListAsync(ct);

        entries = entriesList.Count;
        exits = exitsList.Count;
        unitsEntered = entriesList.Sum(e => e.Lines.Sum(l => l.Quantity));
        unitsExited = exitsList.Sum(e => e.Lines.Sum(l => l.Quantity));
        costEntered = entriesList.Sum(e => e.TotalCost);
        costExited = exitsList.Sum(e => e.Lines.Sum(l => l.UnitCost * l.Quantity));

        var rangeDays = to.DayNumber - from.DayNumber + 1;
        series = BuildMovementSeries(entriesList, exitsList, from, rangeDays);

        topMoving = entriesList.SelectMany(e => e.Lines)
            .Select(l => (Name: l.ProductName, Quantity: l.Quantity))
            .Concat(exitsList.SelectMany(e => e.Lines).Select(l => (Name: l.ProductName, Quantity: l.Quantity)))
            .GroupBy(m => m.Name)
            .Select(g => new TopMovingProduct(g.Key, g.Sum(m => m.Quantity)))
            .OrderByDescending(t => t.Quantity)
            .Take(5)
            .ToList();

        categoryValue = []; // No disponible sin Product.Category — simplificado en fallback
    }

    // ── Estado actual del inventario (siempre desde Products, son datos puntales) ──
    var products = await dbContext.Products.AsNoTracking().ToListAsync(ct);
    var today = DateOnly.FromDateTime(DateTime.Today);

    return new InventoryAnalyticsDto(
        TotalValue: products.Sum(p => p.Stock * p.UnitCost),
        ActiveProducts: products.Count(p => p.Status == "Activo"),
        LowStock: products.Count(p => p.Stock > 0 && p.Stock <= p.MinimumStock),
        OutOfStock: products.Count(p => p.Stock == 0),
        ExpiringSoon: products.Count(p => p.ExpirationDate.HasValue &&
            DateOnly.FromDateTime(p.ExpirationDate.Value) <= today.AddDays(90) &&
            DateOnly.FromDateTime(p.ExpirationDate.Value) > today),
        Expired: products.Count(p => p.ExpirationDate.HasValue &&
            DateOnly.FromDateTime(p.ExpirationDate.Value) < today),
        Entries: entries,
        Exits: exits,
        UnitsEntered: unitsEntered,
        UnitsExited: unitsExited,
        MovementSeries: series,
        TopMoving: topMoving,
        CategoryValue: categoryValue,
        Products: products.Select(ProductListItemDto.FromEntity).ToList()
    );
}
```

---

## 8. DI Registration

En `src/CoppAddresd.Infrastructure/DependencyInjection.cs`, dentro del método de extensión existente (buscar la sección donde se registran los otros queues como `IPatientMetricsQueue`, `IProgramMetricsQueue`, etc.):

```csharp
// Inventory Metrics
services.AddSingleton<IInventoryMetricsQueue, InventoryMetricsQueue>();
services.AddHostedService<InventoryMetricsProcessorHostedService>();
```

---

## 9. Migración EF Core

La migración debe generarse sobre el `AppDbContext` (no es un microservicio separado como Community):

```powershell
# Desde la raíz coppAddresdBack
dotnet ef migrations add AddInventoryDailyMetrics `
    --project src/CoppAddresd.Infrastructure `
    --startup-project src/CoppAddresd.Api `
    --context AppDbContext
```

La migración debe generar:
```sql
CREATE TABLE erp.inventory_daily_metrics (
    metric_date date NOT NULL,
    metric_key character varying(64) NOT NULL,
    dimension_key character varying(64) NOT NULL,
    total_count bigint NOT NULL DEFAULT 0,
    last_updated_at timestamp without time zone NOT NULL,
    CONSTRAINT pk_inventory_daily_metrics PRIMARY KEY (metric_date, metric_key, dimension_key)
);
CREATE INDEX ix_inventory_daily_metrics_key_dim_date
    ON erp.inventory_daily_metrics (metric_key, dimension_key, metric_date);
CREATE INDEX ix_inventory_daily_metrics_date
    ON erp.inventory_daily_metrics (metric_date);
```

---

## 10. Tests unitarios a crear

Archivo: `tests/CoppAddresd.UnitTests/Inventory/InventoryMetricsProcessorTests.cs`

Scenarios:
1. `EntryCreated_UpdatesEntriesCount` — `entries_count/total` incrementa en 1.
2. `EntryCreated_UpdatesUnitsEntered` — `units_entered/total` suma las unidades de todas las líneas.
3. `EntryCreated_UpdatesCostEntered` — `cost_entered/total` guarda en centavos (100.50 → 10050).
4. `EntryCreated_UpdatesPerProductAndCategory` — `product_entries/<nombre>` y `category_units/<cat>` incrementan.
5. `ExitCreated_UpdatesExitsCount` — `exits_count/total` incrementa en 1.
6. `TwoEntriesSameDay_Accumulates` — dos eventos del mismo día suman correctamente (no reemplazan).
7. `GetAnalyticsAsync_WithRollupData_UsesFastPath` — con datos en rollup, no llama a `InventoryEntries` OLTP.
8. `GetAnalyticsAsync_WithoutRollupData_FallsBackToOltp` — sin datos en rollup, usa el código original.

---

## 11. Verificación del schema

Antes de generar la migración, ejecutar en psql:
```sql
-- Verificar en qué schema están las tablas de inventario
SELECT table_schema, table_name
FROM information_schema.tables
WHERE table_name IN ('products', 'inventory_entries', 'inventory_exits', 'inventory_movements');
```

Si el resultado muestra `app` en lugar de `erp`, cambiar el schema en `InventoryDailyMetricConfiguration.cs` y en los SQLs del HostedService.

---

## 12. Documentación a crear/actualizar

- Crear `coppAddresdBack/docs/modules/inventory/analytics.md` con el diseño completo.
- Actualizar `coppAddresdBack/docs/architecture/README.md` con ADR para Dashboard #6.
