# Analítica del Dashboard ERP — Comunidad ADRED (Dashboard #5)

> Documento técnico del módulo de analítica de comunidad. Fase 1: **CQRS pre-aggregation con Channel Pattern** sobre el microservicio `CoppAddresd.Community`.

## 1. Objetivo

El dashboard ERP de comunidad necesita métricas agregadas (series temporales de 30 días, distribución por tipo de post, horas pico) **sin escanear las tablas OLTP** (`community.posts`, `community.comments`, `community.likes`, `community.reposts`) en cada consulta. La solución es una tabla rollup pre-agregada por día, alimentada en background con eventos encolados en memoria.

El patrón ya existe en otros módulos del backend (ProgramProgress, HealthTests, Telemedicine): la mutación escribe en OLTP, encola un evento en un `Channel<T>` (no bloquea la request) y un `BackgroundService` hace un **UPSERT atómico** sobre la tabla rollup.

## 2. Diseño de la tabla rollup

### 2.1. Tabla `community.community_daily_metrics`

| Columna | Tipo | Descripción |
|---|---|---|
| `metric_date` | `date` | Día de la métrica (UTC). |
| `metric_key` | `varchar(64)` | Clave de la métrica (ver abajo). |
| `dimension_key` | `varchar(64)` | Dimensión de la métrica (`total`, tipo de post, hora `0`–`23`). |
| `total_count` | `bigint` | Conteo acumulado (default `0`). |
| `last_updated_at` | `timestamp without time zone` | Última escritura (`NOW()`). |

**PK**: `(metric_date, metric_key, dimension_key)` — cada celda del rollup es una fila.
**Índice de lectura**: `ix_community_daily_metrics_key_dim_date (metric_key, dimension_key, metric_date)`.
**Schema**: `community.` (historial de migraciones `community.__EFMigrationsHistory`).

### 2.2. Claves de métrica (`metric_key`)

| `metric_key` | `dimension_key` | Incrementado por |
|---|---|---|
| `posts_count` | `total` y el tipo de post (`texto`, `imagen`, `video`, `logro`, `poll`, `general`) | `PostCreatedMetricEvent` |
| `comments_count` | `total` | `CommentCreatedMetricEvent` |
| `likes_count` | `total` | `LikeAddedMetricEvent` (+1) / `LikeRemovedMetricEvent` (−1, nunca negativo) |
| `reposts_count` | `total` | `RepostCreatedMetricEvent` (+1) / `RepostRemovedMetricEvent` (−1, nunca negativo) |
| `hourly_activity` | hora `0`–`23` (string) | Posts + comentarios + reposts (incrementos y decrementos) |

> Nota de consistencia: `PostType.Encuesta` se normaliza a la dimensión `poll` en **todos** los emisores (`ToDimensionKey` en `CommunityMutation.cs`) y en el fallback OLTP — nunca se escribe la dimensión `encuesta`.

## 3. Eventos de métricas (`CoppAddresd.Community.Metrics`)

Interfaz marcadora `ICommunityMetricEvent` y records inmutables (mismo patrón que `IProgramMetricEvent`):

- `PostCreatedMetricEvent(PostId, MetricDate, PostType, HourOfDay)`
- `CommentCreatedMetricEvent(CommentId, MetricDate, HourOfDay)`
- `LikeAddedMetricEvent(TargetId, MetricDate, HourOfDay)`
- `LikeRemovedMetricEvent(TargetId, MetricDate)` — `MetricDate` = día ORIGINAL del like (cuando se agregó)
- `RepostCreatedMetricEvent(RepostId, MetricDate, HourOfDay)`
- `RepostRemovedMetricEvent(RepostId, MetricDate, HourOfDay)` — `MetricDate`/`HourOfDay` = día/hora ORIGINALES del repost

### 3.1. Emisores (mutaciones de `CommunityMutation.cs`)

Se inyecta `[Service] ICommunityMetricsQueue? metricsQueue = null` (parámetro opcional al final de la firma, guardado con `if (metricsQueue != null)`) y se encola **después** de `await db.SaveChangesAsync(ct)`:

| Mutación | Evento |
|---|---|
| `CreatePost` / `CreateAnnouncement` | `PostCreatedMetricEvent` (tipo = `ToDimensionKey(post.Type)`; `Encuesta → "poll"`) |
| `CreatePollPost` | `PostCreatedMetricEvent` (tipo fijo `"poll"`) |
| `AddComment` / `ReplyToComment` | `CommentCreatedMetricEvent` |
| `LikePost` / `LikeComment` | `LikeAddedMetricEvent` (fecha del propio like) |
| `UnlikePost` / `UnlikeComment` | `LikeRemovedMetricEvent` (día original del like) |
| `RepostPost` | `RepostCreatedMetricEvent` |
| `UnrepostPost` | `RepostRemovedMetricEvent` (día/hora originales del repost) |

**Regla crítica**: `Channel<T>.Writer.WriteAsync` retorna casi de inmediato; el enqueue **nunca bloquea** la request HTTP. No se usa `metricsQueue?.EnqueueAsync(...)` (el guard explícito evita silenciar un fallo de registro).

## 4. Pipeline en background

```
GraphQL Mutation (CommunityMutation)
    │  1. Escribe en OLTP (EF Core → PostgreSQL)
    └─► 2. Enqueue (Channel<T> bounded 10_000, DropOldest, SingleReader)
                ▼
    CommunityMetricsProcessorHostedService (BackgroundService)
        └─► UPSERT atómico → community.community_daily_metrics
             INSERT ... ON CONFLICT (metric_date, metric_key, dimension_key)
             DO UPDATE SET total_count = total_count + 1, last_updated_at = NOW()
```

- `ICommunityMetricsQueue` / `CommunityMetricsQueue`: cola en memoria (`Channel.CreateBounded(10_000)`, `DropOldest`, `SingleReader = true`) — mismo precedente que Patient/HealthTest; las métricas son aditivas, bajo sobrecarga se descartan las más viejas.
- `CommunityMetricsProcessorHostedService`: drena la cola con un scope DI por evento; cada evento se procesa con `ExecuteSqlRawAsync` (SQL PostgreSQL específico, ver `Metrics/CommunityMetricsProcessorHostedService.cs`). Los errores se loguean y **no** tiran el procesador.
- `LikeRemoved` usa `UPDATE ... SET total_count = GREATEST(0, total_count - 1)` — nunca deja el total negativo.
- Los decrementos (`LikeRemovedMetricEvent` y `RepostRemovedMetricEvent`) apuntan siempre al bucket de la fecha/hora ORIGINAL del like/repost (día en que se creó), no al día en que se removió.
- Registro DI en `Program.cs`:
  ```csharp
  builder.Services.AddSingleton<ICommunityMetricsQueue, CommunityMetricsQueue>();
  builder.Services.AddHostedService<CommunityMetricsProcessorHostedService>();
  ```

> Limitación conocida (igual que en los otros módulos): la cola es en memoria y **por réplica** — si el servicio escala horizontalmente, los eventos de una réplica solo alimentan su propio rollup. Si se necesita exactitud multi-replica, migrar a un bus (Redis Streams / SQS). El upsert `ON CONFLICT` hace el proceso idempotente por evento.

## 5. Estrategia de lectura (dashboard ERP)

`CommunityErpAnalyticsQuery.CommunityErpAnalytics(from?, to?)` (extensión del tipo `Query`, policy `Community.View`):

1. **Rollup primero (O(1))**: `SELECT ... FROM community.community_daily_metrics WHERE metric_date BETWEEN @from AND @to` (AsNoTracking). Sin joins ni scans OLTP.
2. **Transformación a DTOs**: serie diaria (`ActivityDay`: dia, posts, comentarios, reacciones), distribución por tipo (`ErpPostTypeStat`: tipo + count) y horas pico (`PeakHour`: hora + count).
3. **Fallback OLTP**: si el rollup no tiene filas en el rango (sistema recién iniciado, el procesador aún no escribió), calcula `COUNT(*)` agrupados sobre `posts`/`comments`/`likes`/`reposts` con la misma forma de DTOs.

Rango por defecto: últimos 30 días (inclusive). El fallback cuenta **todos** los posts/comentarios creados en el rango (incluye soft-deleted) para ser consistente con el rollup, que no decrementa al eliminar.

## 6. Backfill / reconciliación del rollup

La cola de métricas es **en memoria y por réplica**: los eventos perdidos por un reinicio/cambio de instancia no se recuperan solos una vez que el rango consultado ya tiene filas del rollup (el lector solo cae al OLTP cuando el rango tiene **cero** filas). Para cubrir esa pérdida existe una reconstrucción autoritativa desde el OLTP.

### 6.1. Semántica autoritativa (DELETE + INSERT, no upsert de sobrescritura)

`RecomputeAllAsync` (`Metrics/CommunityMetricsBackfillSql.cs`) ejecuta, en **una sola transacción**:

1. `DELETE` de las 5 claves administradas (`posts_count`, `comments_count`, `likes_count`, `reposts_count`, `hourly_activity`) — **todas las fechas/dimensiones**.
2. Re-INSERT del recálculo set-based desde el OLTP con la **misma semántica exacta del processor**:
   - `posts_count/total` + `posts_count/<tipo>` (`Texto→texto`, `Imagen→imagen`, `Video→video`, `Encuesta→poll`, `Logro→logro`, `NULL→general`); **incluye posts soft-deleted** (el rollup no decrementa al eliminar).
   - `comments_count/total` — **incluye comentarios soft-deleted**.
   - `likes_count/total` — solo las **filas actuales** de `community.likes` (el unlike las borra físicamente).
   - `reposts_count/total` — solo las **filas actuales** de `community.reposts`.
   - `hourly_activity/<hora>` = posts + comentarios + reposts por **hora UTC**, dimensión sin padding (`"0"`..`"23"`), igual que `HourOfDay.ToString()` del processor (nunca `"05"`).
   - Día/hora derivados de `(created_at AT TIME ZONE 'UTC')`, la misma fuente (`DateTime.UtcNow`) que usan las mutaciones al emitir los eventos.

El `DELETE` + re-INSERT (en vez de solo `ON CONFLICT DO UPDATE`) es obligatorio porque `likes_count`, `reposts_count` y `hourly_activity` se **decrementan** en vivo: un rebuild que solo sobrescriba dejaría celdas obsoletas de días/horas cuya actividad actual ya no existe. Resultado: la tabla queda **exactamente igual al OLTP** — las celdas con conteo 0 desaparecen y las dimensiones obsoletas (p. ej. `"05"` con padding o `posts_count/encuesta`) se eliminan.

### 6.2. Arranque del servicio

`CommunityMetricsBackfillHostedService` corre el rebuild completo al arrancar (idempotente), **antes** de que `CommunityMetricsProcessorHostedService` empiece a drenar la cola (registro previo en `Program.cs`). Cualquier error se loguea y **nunca tumba el arranque**: el rollup se puede reparar después con el endpoint interno.

### 6.3. Endpoint interno de reconciliación

```
POST /api/v1/community/maintenance/reconcile-metrics[?dryRun=true]
Header: X-Internal-Key
```

- Misma autenticación que los endpoints internos de mensajería: `Community:InternalApiKey` (o env `COMMUNITY_INTERNAL_API_KEY`); si la clave no está configurada → **503**; clave incorrecta → **401**.
- `dryRun=true` corre el rebuild dentro de la transacción y hace **rollback**: no escribe y reporta lo que habría escrito.
- Respuesta JSON (`CommunityMetricsBackfillResult`): `dryRun`, `deletedRows`, `insertedRows`, `rowsPerKey` (filas por `metric_key`), `duration` y `note`.
- **Nota de convergencia**: el rebuild es autoritativo frente al processor, pero si hay tráfico concurrente (otras réplicas procesando eventos) durante la ejecución, **re-ejecutar una vez** converge — la segunda corrida ya incluye esos eventos.

> El rollup es **descartable y rebuildable desde el OLTP**: ante cualquier duda de consistencia, la respuesta es re-ejecutar la reconciliación, no inspeccionar celdas.

## 7. Tests

`tests/CoppAddresd.Community.UnitTests/CommunityMetricsProcessorTests.cs` — los upserts son SQL PostgreSQL específico (`ON CONFLICT ... EXCLUDED`, `NOW()`, `GREATEST`, schema calificado), por lo que **no** se testean con InMemory ni SQLite: se ejercita el pipeline completo (cola + `HostedService` + rollup real) contra una BD aislada (`coppaddresd_comm_metrics_test_*`).

`tests/CoppAddresd.Community.UnitTests/CommunityMetricsBackfillTests.cs` — backfill/reconciliación (mismo gating de PostgreSQL): equivalencia exacta contra el OLTP (incluye soft-deleted), mapeo de tipos (`NULL→general`, `Encuesta→poll`), limpieza de celdas obsoletas/erróneas (valor stale, dimensión legacy, dimensión con padding, fecha sin actividad) e idempotencia (dos corridas = mismo estado), más el `dryRun` del servicio (rollback sin escritura).

- Requieren `COP_TEST_DB_CONNECTION` (misma convención que los tests de integración); si no está definida, se reportan **SKIPPED** (`RequiresPostgresFactAttribute`).
- Scenarios del processor: incremento total + tipo, acumulación mismo día, like add/remove = 0 (sin negativos), hora pico, separación de claves por métrica.
- Scenarios del backfill: equivalencia contra OLTP (soft-deleted incluidos), `NULL→general`/`Encuesta→poll`, limpieza de celdas obsoletas, idempotencia y `dryRun` sin escritura.

## 8. Migración

`AddCommunityDailyMetrics` (schema `community.`): crea `community_daily_metrics` + índice `ix_community_daily_metrics_key_dim_date`.

```powershell
dotnet ef migrations add AddCommunityDailyMetrics `
    --project src/Services/CoppAddresd.Community `
    --startup-project src/Services/CoppAddresd.Community `
    --context CommunityDbContext
```