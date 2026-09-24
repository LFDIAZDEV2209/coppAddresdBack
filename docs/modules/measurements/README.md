# Módulo: Mediciones self-service del móvil (Fase 7)

Lecturas propias del paciente para la APP móvil (`aud=app`): mediciones
paginadas por cursor y serie diaria por métrica. El paciente se deriva SIEMPRE
del JWT (`ICurrentContext.UserId` → `patient_profiles.user_id` en el handler,
anti-IDOR): sin perfil → 404, nunca datos ajenos ni 403 que confirme
existencias. Sin permisos ERP (convención `me/*`).

## Endpoints

| Método y ruta                    | Query params                                                                                                    | Respuesta                                                                                                                                                                                                                                                        |
| -------------------------------- | --------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET /api/v1/me/measurements`    | `pageSize?` (default 20, rango 1–100), `cursor?` (opaco), `codes?` (CSV de códigos canónicos, case-insensitive) | `200` con `CursorPagedResult<MeasurementItemDto>` (`items`, `nextCursor`, `hasNextPage`); `400` PageSize/códigos/cursor inválidos; `401` sin JWT; `404` sin perfil                                                                                               |
| `GET /api/v1/me/metrics-history` | `codes?` (CSV, vacío = defaults `bmi,hba1c,body_fat`), `days?` (default 180, rango 7–365 estricto)              | `200` con `MetricsHistoryResponseDto` (misma forma que `GET /api/v1/program/me/metrics-history`); `400` códigos desconocidos (`METRICS_UNKNOWN`) o días fuera de rango; `401` sin JWT; `404` sin perfil. NUNCA 404 `NO_ACTIVE_ENROLLMENT` (no exige inscripción) |

`MeasurementItemDto` es mínimo a propósito: `id, metricCode, metricName,
value, unitCode, unitSymbol, observedAt, source`. Sin notas clínicas,
`createdBy`, `encounterId`, `batchId` ni `sourceKey` (minimización de PHI).

## Queries

Paginada (una sola query set-based, `AsNoTracking`, sin N+1):

```sql
SELECT cm.id, mm.code, mm.name, cm.value, u.code, u.symbol, cm.observed_at, cm.source
FROM app.clinical_measurements cm
JOIN app.measurement_metrics mm ON cm.metric_id = mm.id
JOIN app.unit_of_measures u ON cm.unit_id = u.id
WHERE cm.patient_id = @patientId
[-- AND lower(mm.code) IN (@codes)]
[-- AND (cm.observed_at, cm.id) < (@observedAt, @cursorId)]  -- keyset
ORDER BY cm.observed_at DESC, cm.id DESC
LIMIT @pageSize + 1;  -- peek para HasNextPage, sin COUNT
```

Cursor opaco: `Base64(observedAt ISO-8601 round-trip + '|' + id)`.
Serie diaria: reutiliza la proyección de `MetricsHistoryRepository`
(`BuildCachePayload` + `TrimToRequest`, ventana UTC de 365d sin inscripción).

## Índices

Índice actual: `ix_clinical_measurements_patient_observed` en
`(patient_id, observed_at)` (ver `ClinicalMeasurementConfiguration`).
Volumen medido: 1289 filas totales; paciente demo con 168 filas.

### EXPLAIN (ANALYZE, BUFFERS) — 2026-09-24, PostgreSQL local, paciente demo (168 filas)

Base (`LIMIT 21`, sin filtro):

- `Index Scan using ix_clinical_measurements_patient_observed`
  (`Index Cond: patient_id = ...`, 1 búsqueda) — SIN `Seq Scan`.
- `Incremental Sort` (`observed_at DESC, id DESC`, `Presorted Key:
observed_at`, quicksort 28kB) + `Nested Loop` con `Memoize` en los PK de
  métrica y unidad (15+21 hits de caché).
- Short-circuit por `LIMIT`: leyó 29 filas del grupo (mismo `observed_at` de
  lote) para emitir 21.
- `Buffers: shared hit=78 read=5` · ejecución 2,7 ms (planificación 5,0 ms).

Con filtro `lower(mm.code) IN ('weight','bmi')`:

- `Bitmap Index Scan` sobre el MISMO índice (168 filas, 7 bloques exactos) +
  `Sort` quicksort 28kB; el filtro corre sobre 14 métricas cacheadas
  (`Memoize`, 154 hits), nunca sobre las mediciones.
- `Buffers: shared hit=91` · ejecución 0,66 ms. El planner subestima
  (`rows=3` vs 24 reales con `ANY` sobre función) — sin impacto a esta escala.

Veredicto: el índice existente cubre ambas variantes. **Sin migración.**

### Índice adicional evaluado y diferido

Un compuesto `(patient_id, metric_id, observed_at DESC)` solo tendría sentido
si el filtro por código dejara de resolverse en las ~14 filas del catálogo
(escala de millones de filas por paciente o p95 > 100 ms). **NO crear sin
aprobación explícita**: re-medir con `EXPLAIN (ANALYZE, BUFFERS)` cuando un
paciente supere ~100k filas.

## Caché

- `GET /me/metrics-history`: contexto completo por paciente
  (`my-metrics-history:{patientId}:v1`, TTL 5 min, fail-open). Clave SEPARADA
  de `metrics-history:{patientId}:v1` (programa) porque las ventanas difieren
  (UTC vs zona de la inscripción).
- `GET /me/measurements`: sin caché (keyset puro O(log n); el cursor ya evita
  `OFFSET`/`COUNT`).

## Archivos

- `src/CoppAddresd.Application/Features/Measurements/Queries/GetMyMeasurements/`
  (query, DTO mínimo, `CursorPagedResult`)
- `src/CoppAddresd.Application/Features/Measurements/Queries/GetMyMetricsHistory/`
- `src/CoppAddresd.Infrastructure/Repositories/PatientMeasurementRepository.cs`
- `GET /api/v1/me/*` en `src/CoppAddresd.Api/Controllers/MyPatientProfileController.cs`
