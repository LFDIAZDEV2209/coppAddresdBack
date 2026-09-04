# Analytics — Dashboard #6: Inventario & Farmacia

> **Estado**: Fase 1 implementada (pre-agregación CQRS con Channel Pattern).
> Dashboard de inventario servido desde una tabla rollup en O(1), sin escanear
> las tablas transaccionales.

---

## 1. Problema

El `GetAnalyticsAsync` original cargaba **todo** en memoria para responder el
dashboard:

1. `Products` completos (`ToListAsync`) — O(n) productos.
2. `InventoryEntries` + `Include(Lines)` — O(n) entradas × líneas.
3. `InventoryExits` + `Include(Lines)` — O(n) salidas × líneas.
4. Cálculos de agregación en C# sobre esas listas.

Con volumen clínico real (farmacia activa), cada request del dashboard degrada
el PostgreSQL y la API.

## 2. Solución: pre-agregación diaria (Channel Pattern)

El mismo patrón ya probado en Program Progress, Pacientes y Tests de Salud:

```
Command Handler (Application Layer)
    │
    ├─► 1. Escribe en la base de datos (EF Core → PostgreSQL OLTP)
    │
    └─► 2. Encola evento en memoria (Channel<T> no bloqueante)
                    │
                    ▼
        BackgroundService (HostedService)
            └─► UPSERT atómico → erp.inventory_daily_metrics
                    INSERT ... ON CONFLICT DO UPDATE
                    SET total_count = total_count + delta
```

**Regla crítica**: el enqueue NO bloquea al usuario. La latencia de escritura
HTTP no se ve afectada (un `ValueTask` sobre un Channel en memoria).

**Puntos de inyección** (append-only, no hay updates de movimientos):

| Handler | Evento |
|---|---|
| `CreateEntryCommandHandler` (`EntryCommands.cs`) | `InventoryEntryCreatedMetricEvent` |
| `CreateExitCommandHandler` (`ExitCommands.cs`) | `InventoryExitCreatedMetricEvent` |

Ambos handlers reciben `IInventoryMetricsQueue? metricsQueue = null` en el
primary constructor (parámetro opcional: los tests y hosts sin la cola no
cambian su comportamiento; el null check es explícito, nunca `?.`).

## 3. Tabla rollup: `erp.inventory_daily_metrics`

Misma convención del módulo: schema **`erp`** (verificado contra
`ProductConfiguration`, `InventoryEntryConfiguration`, `InventoryExitConfiguration`,
`InventoryEntryLineConfiguration`, `InventoryExitLineConfiguration`,
`InventoryMovementConfiguration`, `StoreItemConfiguration` — todas `ToTable(..., "erp")`).

| Columna | Tipo | Descripción |
|---|---|---|
| `metric_date` | `date` | Fecha calendario de la agregación |
| `metric_key` | `varchar(64)` | Categoría de métrica (ver tabla abajo) |
| `dimension_key` | `varchar(64)` | Discriminador: `"total"`, nombre de producto o categoría |
| `total_count` | `bigint DEFAULT 0` | Valor acumulado (monetario → centavos) |
| `last_updated_at` | `timestamp` | Marca de la última actualización |

**PK compuesta**: `(metric_date, metric_key, dimension_key)`.

**Índices**:
- `ix_inventory_daily_metrics_key_dim_date` — `(metric_key, dimension_key, metric_date)` (lecturas por métrica).
- `ix_inventory_daily_metrics_date` — `(metric_date)` (barridos por rango de fechas).

### 3.1. Metric keys

| `metric_key` | `dimension_key` | Semántica |
|---|---|---|
| `entries_count` | `total` | Cantidad de documentos de entrada |
| `exits_count` | `total` | Cantidad de documentos de salida |
| `units_entered` | `total` | Unidades recibidas (suma de cantidades de líneas de entrada) |
| `units_exited` | `total` | Unidades despachadas (suma de cantidades de líneas de salida) |
| `cost_entered` | `total` | Costo de entradas (suma de `TotalCost`), **centavos** |
| `cost_exited` | `total` | Costo de salidas (suma `UnitCost × Quantity`), **centavos** |
| `product_entries` | nombre del producto (≤64) | Unidades de entrada por producto (top moving) |
| `product_exits` | nombre del producto (≤64) | Unidades de salida por producto |
| `category_units` | nombre de categoría | Unidades de entrada por categoría |
| `category_cost` | nombre de categoría | Costo por categoría, **centavos** |
| `category_exits` | nombre de categoría | Unidades de salida por categoría |

### 3.2. Convención money-as-cents

Los costos **nunca** se guardan como `decimal` en el rollup:

- **Escritura** (procesador): `(long)(decimal * 100)` — `100.50` → `10050`.
- **Lectura** (fast-path): `total_count / 100m` — `10050` → `100.50m`.

Evita columnas `numeric` en la tabla de agregación y mantiene `bigint` puro
(compatible con sumas atómicas `total_count + EXCLUDED.total_count`).

## 4. Componentes

| Componente | Archivo | Rol |
|---|---|---|
| Entidad de dominio | `src/CoppAddresd.Domain/Entities/InventoryDailyMetric.cs` | Modelo del rollup |
| Configuración EF | `src/CoppAddresd.Infrastructure/Configurations/InventoryDailyMetricConfiguration.cs` | Schema `erp`, PK, índices |
| DbSet | `AppDbContext.InventoryDailyMetrics` | Acceso EF |
| Eventos | `src/CoppAddresd.Application/Features/Inventory/Events/IInventoryMetricEvent.cs` | Marcador + `InventoryEntryCreatedMetricEvent`, `InventoryExitCreatedMetricEvent`, `InventoryMetricLine` |
| Interfaz de cola | `src/CoppAddresd.Application/Interfaces/IInventoryMetricsQueue.cs` | Contrato en memoria |
| Cola | `src/CoppAddresd.Infrastructure/Metrics/InventoryMetricsQueue.cs` | Channel no delimitado, `SingleReader` |
| Procesador | `src/CoppAddresd.Infrastructure/Metrics/InventoryMetricsProcessorHostedService.cs` | UPSERTs atómicos por evento |
| DI | `DependencyInjection.cs` | `AddSingleton<IInventoryMetricsQueue, InventoryMetricsQueue>` + `AddHostedService<InventoryMetricsProcessorHostedService>` |
| Migración | `AddInventoryDailyMetrics` | Tabla + índices en `erp` |

El evento transporta las líneas **denormalizadas** (`ProductName`, `Category`,
`Quantity`, `UnitCost`) para que el procesador no re-consulte la base. La
categoría se carga en el handler vía `repository.GetProductByIdAsync(productId)`
(fallback `"general"` si el producto no existe).

## 5. Estrategia de lectura: `GetAnalyticsAsync`

El repositorio (`InventoryRepository.GetAnalyticsAsync`) ahora tiene dos caminos:

### 5.1. Fast path (rollup) — con datos en el rollup

1. Lee `inventory_daily_metrics` del rango `[from, to]` (una query filtrada por
   fechas, ayudada por `ix_inventory_daily_metrics_date`).
2. Deriva en memoria (O(1) sobre el resultado):
   - `Entries`/`Exits`/`UnitsEntered`/`UnitsExited` — métricas `*_count`/`units_*` con `dimension_key = "total"`.
   - `MovementSeries` — serie diaria de `units_entered`/`units_exited` por fecha.
   - `TopMoving` — `product_entries` agrupado por producto, top 5 por unidades.
   - `CategoryValue` — `category_cost` agrupado por categoría (centavos → decimal).
   - Los costos del período (`cost_entered`/`cost_exited`) se calculan y quedan
     disponibles para la Fase 2 (el DTO actual no los expone).
3. **No toca** `inventory_entries`/`inventory_exits`.

### 5.2. Fallback OLTP — sin datos en el rollup (sistema recién iniciado)

Se conserva el **código original** de cálculo en memoria (MANTENER SIN CAMBIOS
al editar): carga entradas/salidas con líneas del rango y calcula las mismas
métricas, incluido `CategoryValue` desde el catálogo actual (`products`).
También conserva el agrupamiento semanal/mensual de `BuildMovementSeries`
para rangos > 31 días.

### 5.3. Estado actual del inventario (siempre desde `Products`)

`TotalValue`, `ActiveProducts`, `LowStock`, `OutOfStock`, `ExpiringSoon`,
`Expired` y la lista `Products` son datos **puntuales del catálogo** (stock,
mínimos, vencimientos), no agregaciones de flujo: se calculan siempre con una
query ligera sobre `erp.products`.

> **Criterio de decisión**: la presencia de CUALQUIER fila de rollup en el
> rango activa el fast path. El backfill de datos históricos pre-deploy es
> responsabilidad de una tarea de reconciliación (fuera de Fase 1).

## 6. Escenarios cubiertos por tests

`tests/CoppAddresd.UnitTests/Inventory/InventoryMetricsProcessorTests.cs`
(gate `COP_TEST_DB_CONNECTION`, PostgreSQL real — el upsert usa `ON CONFLICT`
y `NOW()`, incompatibles con InMemory/SQLite):

1. `EntryCreated_UpdatesEntriesCount` — `entries_count/total` +1.
2. `EntryCreated_UpdatesUnitsEntered` — unidades de todas las líneas.
3. `EntryCreated_UpdatesCostEntered` — `100.50` → `10050` centavos.
4. `EntryCreated_UpdatesPerProductAndCategory` — `product_entries/<nombre>` y `category_units/<cat>` + `category_cost`.
5. `ExitCreated_UpdatesExitsCount` — `exits_count/total` +1 (+ unidades y costo).
6. `TwoEntriesSameDay_Accumulates` — dos eventos del mismo día suman (no reemplazan).
7. `GetAnalyticsAsync_WithRollupData_UsesFastPath` — con rollup sembrado y OLTP
   con valores divergentes, el DTO refleja el rollup (prueba que NO lee OLTP).
8. `GetAnalyticsAsync_WithoutRollupData_FallsBackToOltp` — rollup vacío + OLTP
   sembrado → el DTO refleja el cálculo original.

## 7. Notas de implementación

- Los nombres de producto/categoría se truncan a 64 caracteres para
  `dimension_key` (limitación de la columna).
- Categoría vacía → `"general"`.
- `InventoryExit` no tiene `TotalCost`: el procesador calcula
  `Σ(UnitCost × Quantity)` desde las líneas del evento.
- El procesador es resilient: un fallo de upsert se loguea (`LogError`) y no
  tumba el host; el evento se pierde (at-least-once es una mejora futura).
- La migración `AddInventoryDailyMetrics` es **puramente aditiva** sobre el
  estado actual de migraciones.