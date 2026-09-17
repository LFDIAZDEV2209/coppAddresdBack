# Analítica del módulo Tests de Salud (CQRS — pre-agregación)

Cómo se alimenta el dashboard ERP (`/health-tests` en el frontend) y qué rol
juegan las tablas de pre-agregación. Reglas del patrón: skill
`cqrs-preaggregation`.

## Endpoints de datos del dashboard

| Endpoint | Fuente | Contrato |
|---|---|---|
| `GET /api/v1/health-tests/stats?state=IL&cityId=` | **Query set-based por zona** (1 round-trip: CTE `zone` + 5 conteos con EXISTS) + caché TTL stats 30-60 s | `{totalPatients, withPending, completed, highRisk, activeAlerts}` |
| `GET /api/v1/health-tests/master?state=&cityId=` | JOIN por zona en SQL (`ListAssignmentsWithPatientDataForZoneAsync`) + alertas/nombres acotados a la zona | Filas maestras (sin paginación — lo impone el frontend) |
| `GET /api/v1/health-tests/geo` | **Rollup snapshot** `app.health_test_geo_rollups` (O(#ciudades)) + 2 queries acotadas para las alertas top-4; fallback a agregación en memoria si el rollup está vacío | `HealthTestsGeoDto` (ciudades, alertas, totales) |
| `GET /api/v1/health-tests/coverage-trend` | **Rollup diario** `app.health_test_daily_metrics` (`assignments_count/completed` global, últimos 12 meses) | 12 puntos `{label es-CO, coverage, completed}` |
| `GET /api/v1/health-tests/alerts` | OLTP paginado (índices existentes) | Paginado estándar |

`/stats` y `/master` sin filtro geo conservan el alcance global original
(rollup diario global con fallback a OLTP; ViewOwn acota por
`patient_professionals`).

## Tablas de pre-agregación

### `app.health_test_daily_metrics` (serie temporal, Channel Pattern)

PK `(MetricDate, ClinicId, MetricKey, DimensionKey)`. Columnas PascalCase
(legacy — el seeder las cita con comillas). Claves vivas: `assignments_count`
(pending/completed/in_progress), `evaluations_count/completed`,
`test_completions/<code>`, `severity_count/<sev>`, `alerts_count/<status>`.
Alimentada por `IHealthTestMetricsQueue` →
`HealthTestMetricsProcessorHostedService` + backfill al arranque
(`MetricsBackfillSeeder`).

**Ojo con la semántica**: los contadores de "estado actual" (`pending`,
`alerts_count/active`) se **decrementan** por el processor al cambiar de
estado; el backfill histórico los SOBREESCRIBE (`TotalCount = EXCLUDED`) — no
mezclar backfill de esas filas con los counters vivos (drift). Los conteos
globales del `/stats` usan el rollup cuando existe y caen a OLTP cuando no
(fallback `AnyAsync(MetricKey == ...)`).

### `app.health_test_geo_rollups` (snapshot, migración `AddHealthTestGeoRollups`)

PK `city_id`; columnas `state_code, city_name, patients_count, evaluated_count,
high_risk_count, active_alerts_count, avg_score_sum, avg_score_count,
updated_at`. Representa el **estado actual** por ciudad (no una serie): los
KPIs geo son snapshots — la tabla diaria no puede representarlos sin doble
conteo (un paciente pendiente hoy apareció como pending N días atrás).

Definiciones (idénticas a la agregación en memoria que reemplazaron):

- `patients_count`: pacientes activos (`deleted_at IS NULL`) con esa ciudad.
- `evaluated_count`: pacientes con ≥1 resultado `result_type='score'`.
- `high_risk_count`: pacientes cuya PEOR severidad entre sus scores es
  `high` o `critical`.
- `avg_score_sum/count`: suma de promedios de score por paciente y número de
  pacientes evaluados → promedio por ciudad = `sum/count`; el global es
  `SUM(sum)/SUM(count)` sobre todas las ciudades.
- `high_risk_pct` (DTO): `high_risk_count / evaluated_count * 100` — se calcula
  al leer; `null` cuando `evaluated_count = 0` ("Sin datos" en el mapa).

## Estrategia de actualización

1. **Backfill completo** al arranque (`MetricsBackfillSeeder` — 1 statement
   set-based idempotente, borra filas huérfanas) y a demanda
   `POST /api/v1/dashboard/maintenance/reconcile-metrics`.
2. **Incremental por evento**: `HealthTestMetricsProcessorHostedService`
   resuelve la ciudad del paciente del evento (1 lookup) y recomputa ESA fila
   con `HealthTestGeoRollupSql.RecomputeCityAsync` (INSERT ... SELECT ...
   ON CONFLICT, con DELETE de la ciudad si quedó sin pacientes). Fuera del
   request path (BackgroundService).
3. **Caché TTL** 30-60 s con jitter (`CacheKeys.Stats`) en `/stats`,
   `/master`, `/geo`, `/coverage-trend` (double buffering: rollup + TTL).

El rollup es descartable: se reconstruye siempre desde OLTP.

## Rendimiento medido (2026-09-15, BD dev)

- Backfill completo del rollup geo: **152 ms** (148 ciudades).
- Lectura del rollup para `/geo`: **1 ms** vs escaneo total O(pacientes+resultados).
- `/stats?state=IL`: **1 query** con Index Scan `ix_patient_profiles_city_id`
  (plan verificado con `EXPLAIN ANALYZE`; 22 buffers). Antes: materializar
  lista de GUIDs + 5 counts con IN list.
- Equivalencia verificada: la ruta nueva produce exactamente los mismos 5 KPIs
  que la ruta vieja (12/107/38/16/5 en el dataset IL) y el rollup coincide con
  la agregación directa (patients=282, high=165, scoreSum≈1666.93).

## Índices asociados

`ix_patient_profiles_city_id`, `ix_patient_profiles_state_id`,
`ix_health_test_geo_rollups_state_code` — ver
`docs/database/indexes.md`. Nota: NO se duplicó `(severity, result_type)` en
`health_test_results` (redundante con `ix_health_test_results_type_severity`).

## Extensiones futuras

- Nuevo KPI de dashboard del módulo → evaluar claves nuevas en las tablas
  existentes antes de crear tablas nuevas (skill `cqrs-preaggregation`).
- Filtro geo por clínica (`ClinicId` ≠ Guid.Empty) aún no implementado; el
  esquema de la tabla diaria ya lo soporta.
- El trend con filtro geo sigue calculándose en el cliente (master ya filtrado);
  si crece, mover a rollup por estado.
