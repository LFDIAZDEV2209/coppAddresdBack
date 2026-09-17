# ANTARES Biohacking Program Progress — Analytics & CQRS Specification

This document details the analytical pre-aggregation architecture, event contracts, database schemas, and querying mechanics for the **ANTARES Biohacking Program Progress Module** (`app.` schema) in CoppAddresd.

---

## 1. Module Overview & Analytics Requirements

The ANTARES Biohacking program monitors patient adherence across 8 fundamental wellness pillars (Nutrition, Exercise, Hydration, Sleep, Fasting, Supplements, Mindfulness, Biomarkers). The dashboard requires real-time KPIs:
- **Pillar Task Completions**: Aggregate counts of completed tasks per pillar across cohorts.
- **Cohort Adherence Index**: Daily compliance scores calculated without multi-table joins.
- **Active Enrollees vs. Dropouts**: Cohort progression and dropout rate monitoring.

---

## 2. Domain Entity & Schema Design

### Entity: `ProgramDailyMetric`
Defined in `CoppAddresd.Domain.Entities.ProgramProgress.ProgramDailyMetric`.

```csharp
public class ProgramDailyMetric
{
    public DateOnly MetricDate { get; set; }
    public Guid ClinicId { get; set; }
    public string MetricKey { get; set; } = "";    // "tasks_completed", "active_enrollments"
    public string DimensionKey { get; set; } = ""; // "nutrition", "exercise", "hydration", etc.
    public long TotalCount { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
```

### Table: `app.program_daily_metrics`
- **Primary Key**: `(metric_date, clinic_id, metric_key, dimension_key)`

---

## 3. Asynchronous Metric Events

All events implement marker interface `IProgramMetricEvent`:
- `ProgramTaskCompletedMetricEvent(Guid TaskId, Guid PatientId, Guid? ClinicId, DateOnly MetricDate, string PillarCode)`
- `ProgramEnrollmentStatusChangedMetricEvent(Guid EnrollmentId, Guid PatientId, Guid? ClinicId, DateOnly MetricDate, string OldStatus, string NewStatus)`

---

## 4. Background Processor & Atomic Upserts

The `ProgramMetricsProcessorHostedService` processes the `IProgramMetricsQueue` channel asynchronously, updating `app.program_daily_metrics` using atomic `INSERT ... ON CONFLICT DO UPDATE` statements.

---

## 5. Repository Optimization & Fallback

In `ProgramRepository.cs`, dashboard aggregation queries query `app.program_daily_metrics` first ($O(1)$ fast path), delivering sub-2ms response times for ANTARES clinical supervisors.

---

## 6. Biometría — rollup diario `app.biometria_daily_metrics`

Tabla de pre-agregación del dashboard comunitario de Biometría (SPEC §06),
alimentada por `BiometriaMetricsProcessorHostedService` (cola
`IBiometriaMetricsQueue`, evento `BiometriaMeasuredEvent` emitido por
`CompleteTaskCommandHandler` al completar la tarea `vitals`). Clave primaria:
`(metric_date, metric_key, dimension_key)`.

### 6.1 Semántica por métrica (espejo del processor)

| `metric_key` | `dimension_key` | Origen (`app.measurement_metrics.code`) | Categorías |
|---|---|---|---|
| `imc_distribution` | bucket OMS | `bmi` | `<18.5` Bajo peso · `<25` Normal · `<30` Sobrepeso · `<35` Obesidad I · resto Obesidad II-III |
| `grasa_distribution` | `{male\|female}_{categoría}` | `body_fat` | Male: `<=18` Óptimo · `<=24` Normal · `<=29` Alto · resto Obesidad. Female: `<=23` Óptimo · `<=31` Normal · `<=37` Alto · resto Obesidad |
| `glucosa_distribution` | bucket ADA | `glucose_fasting` | `<100` Normal · `<126` Prediabetes · resto Elevada |
| `community_avg` | `imc` / `grasa` / `glucosa` | los tres códigos | `total_count` + `total_value` (suma) → promedio en O(1) |

Las categorías de `grasa_distribution` son las del dashboard comunitario
(`ClassifyGrasa` de `ProgramRepository`), no la escala atlética previa; por eso
el backfill purga (`DELETE`) todas las filas de esa clave antes de recomputarla,
eliminando las categorías huérfanas del naming anterior.

`total_count` = número de mediciones del bucket; `total_value` = suma de los
valores (también en las distribuciones). El processor ignora valores `<= 0`
(`is { } x and > 0`); el backfill aplica el mismo corte (`cm.value > 0`).

Sexo para `grasa_distribution`: sale del perfil del paciente
(`app.patient_profiles.gender`, trim + lower) — `masculino`/`m`/`male` → `male`;
cualquier otro valor o `null` → `female`.

### 6.2 Claves SOLO de evento (no reconstruibles desde OLTP)

- `glucosa_distribution` dimensión **`"Sin dato"`**: el processor la emite por
  cada tarea `vitals` completada SIN glucosa. Las mediciones no registran ese
  "sin dato" (no hay fila), así que el backfill NO la recalcula ni la borra.
- `city_patient_count` (dimensión = `city_id`, heat map): se emite con la ciudad
  del perfil al completar vitals. El backfill tampoco la toca.

### 6.3 Backfill (idempotente)

Implementado en `CoppAddresd.Infrastructure.Metrics.BiometriaRollupSql`
(`RecomputeAllAsync`, set-based: `INSERT ... ON CONFLICT ... DO UPDATE SET
total_count = EXCLUDED.total_count, total_value = EXCLUDED.total_value`). A
diferencia del processor (que acumula deltas por evento), el backfill recompone
cada fila desde `app.clinical_measurements`, por lo que correrlo N veces deja el
mismo estado (idempotente).

Se ejecuta en:

- el seed demo (`BiometriaSeeder`, solo si insertó mediciones),
- el arranque de la API (`MetricsBackfillSeeder.RunBackfillAsync`), y
- `POST /api/v1/dashboard/maintenance/reconcile-metrics`.

**Fecha**: el backfill usa `DATE(observed_at)` (día UTC de la medición). El
processor en vivo usa la fecha local del paciente (`request.LocalDate`), pero la
medición solo guarda `observed_at = MeasuredAt ?? now()` (UTC) y no la fecha
local (reconstruirla exigiría la zona del enrollment), por lo que en el borde de
medianoche ambos días pueden diferir en ±1.

