# Skill: cqrs-preaggregation

# Pre-agregación CQRS (Channel Pattern) — Reglas del proyecto

Los dashboards (KPIs, series temporales, mapas, distribuciones) NO deben ejecutar
agregaciones pesadas sobre tablas transaccionales en cada request. El patrón
reutilizable del repo: **evento → cola en memoria → processor hosted service →
tabla rollup**, con backfill idempotente al arranque.

## Referencias vivas (copiar de aquí, no reinventar)

| Pieza | Ejemplo implementado |
|---|---|
| Tabla rollup | `app.health_test_daily_metrics`, `app.patient_daily_metrics`, `erp.inventory_daily_metrics`, `tele.appointment_daily_metrics` |
| Entidad + config | `Domain/Entities/HealthTests/HealthTestDailyMetric.cs` + `Infrastructure/Configurations/HealthTests/HealthTestDailyMetricConfiguration.cs` |
| Cola + evento | `Infrastructure/Metrics/HealthTestMetricsQueue.cs` + `Application/Features/HealthTests/Events/IHealthTestMetricEvent.cs` |
| Processor | `Infrastructure/Metrics/HealthTestMetricsProcessorHostedService.cs` |
| Emisión (desacoplada) | Handlers con `IHealthTestMetricsQueue? metricsQueue = null` (opcional DI → tests sin cola) |
| Backfill idempotente | `src/CoppAddresd.Api/Seeders/MetricsBackfillSeeder.cs` (IHostedService + endpoint `POST /api/v1/dashboard/maintenance/reconcile-metrics`) |

## Cuándo pre-agregar

- Conteos/sumas/AVG/MAX/GROUP BY que se repiten por request de dashboard.
- Series temporales por día/semana/mes.
- Snapshots de "estado actual" por dimensión (ciudad, estado, clínica, tipo).
- `COUNT ... WHERE <filtro de dashboard>` sobre tablas de decenas de miles de filas+.

NO pre-agregar: paginados por PK/fecha (los índices OLTP bastan), tablas
catálogo (< 1k filas), consultas ya resueltas por índice en < 10 ms.

## Elegir semántica del rollup (decisión crítica)

1. **Serie temporal** (creció y no retrocede): altas, completados por día,
   severidad al completar. Clave = `(date, scope, key, dimension)`; el processor
   hace `TotalCount += n` sobre la fila del día del evento.
2. **Snapshot** (estado actual que puede decrecer retroactivamente):
   `pending` actual, alertas activas actuales, promedios, "peor severidad por
   paciente". La tabla diaria NO sirve aquí: sumar filas por día contaría
   doble y los decrementos (`GREATEST(0, n-1)`) solo afectan al día del evento.
   Usar una **tabla snapshot** por dimensión (ej. `app.health_test_geo_rollups`:
   PK por ciudad, recomputo set-based idempotente por ciudad afectada).

**Anti-patrón probado**: mezclar backfill (`DO UPDATE SET TotalCount = EXCLUDED`)
con counters vivos que se decrementan → drift silencioso. Un rollup vivo de
snapshot se mantiene por **recomputo de la fila afectada**, nunca por delta.

## Reglas del processor (incremental)

- El evento trae la identidad mínima (patientId, clinicId, date). El processor
  resuelve la dimensión afectada (ej. ciudad del paciente) con 1 query indexada
  y recomputa esa fila con SQL set-based (`INSERT ... SELECT ... ON CONFLICT
  DO UPDATE`, `WHERE <dimensión> = @afectada`).
- Nada de esto corre en el request path (BackgroundService, scope nuevo).
- Fallo del processor = warning en log + fallback del lector al dato fuente;
  nunca rompe la request.

## Reglas del lector (query de dashboard)

- El rollup es fast path; siempre definir fallback al dato fuente cuando el
  rollup esté vacío (backfill pendiente) — patrón `AnyAsync(MetricKey == ...)` del
  `DashboardController`.
- Snapshot geo: el lector solo lee la tabla rollup + catálogos mínimos.
- Resultados: **exactamente** los mismos números y shapes que la agregación
  directa. Documentar en el módulo la definición de cada métrica (qué fila,
  qué dimensión) y cubrir con test de equivalencia.
- Caché (TTL stats 30-60 s con jitter, `CacheKeys.Stats`) encima del rollup.
- Filtros dinámicos (union de estados, ciudad) del filtro geo acotan con JOIN
  en SQL; nunca materializar listas de GUIDs y `Contains` sobre tablas grandes.

## Índices

- Rollup: la PK compuesta ya indexa `(date, scope, key, dimension)`; añadir
  índice por dimensión de lectura si el lector filtra por ella (ej.
  `state_code`).
- Snapshot: PK por dimensión + índice por atributo de agrupación.
- Las FKs de las tablas OLTP que participan en los filtros del dashboard
  (`city_id`, `state_id`, `severity+result_type`, ...) deben tener índice:
  PostgreSQL no crea índices en FKs automáticamente.

## Backfill y consistencia

- Backfill idempotente al arranque (`MetricsBackfillSeeder`) + endpoint
  `reconcile-metrics` bajo demanda. SQL set-based, un statement por tabla.
- Snapshot completo se puede reconstruir siempre desde OLTP: es la única
  fuente de verdad; el rollup es descartable.
- Delete de filas huérfanas (dimensión que ya no existe) en el backfill.

## Checklist antes de dar por terminada una pre-agregación

- [ ] Semántica (serie vs snapshot) elegida y documentada.
- [ ] Evento emitido en TODAS las mutaciones que cambian el dato (y solo ahí).
- [ ] Processor actualiza la fila/día correcto (incremental) o recomputa.
- [ ] Backfill idempotente (ON CONFLICT) + endpoint reconcile actualizado.
- [ ] Lector con fallback + caché TTL.
- [ ] `EXPLAIN (ANALYZE, BUFFERS)` del lector: sin seq scan, sin IN list gigante.
- [ ] Test de equivalencia rollup vs OLTP.
- [ ] Docs del módulo actualizadas (español).
