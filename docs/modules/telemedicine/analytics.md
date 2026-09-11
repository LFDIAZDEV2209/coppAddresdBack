# Telemedicine & Appointments — Analytics & CQRS Specification

This document details the analytical pre-aggregation architecture, event contracts, database schemas, and querying mechanics for the **Telemedicine & Appointments Module** (`tele.` schema) in CoppAddresd.

---

## 1. Module Overview & Analytics Requirements

The Telemedicine module manages physical consultations and virtual WebRTC appointments. The dashboard requires real-time KPIs:

- **Consultation Volumes**: Daily totals of `scheduled`, `in_progress`, `completed`, `cancelled`, and `no_show` appointments.
- **Clinician Productivity**: Consultation counts per professional and clinic.
- **Attendance & Adherence Rates**: No-show vs. completion percentages.

---

## 2. Domain Entity & Schema Design

### Entity: `AppointmentDailyMetric`

Defined in `CoppAddresd.Domain.Entities.Telemedicine.AppointmentDailyMetric`.

```csharp
public class AppointmentDailyMetric
{
    public DateOnly MetricDate { get; set; }
    public Guid ClinicId { get; set; }
    public Guid ProfessionalId { get; set; }
    public string Status { get; set; } = "";
    public long TotalCount { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
```

### Table: `tele.appointment_daily_metrics`

- **Primary Key**: `(metric_date, clinic_id, professional_id, status)`
- **Isolated Migrations Table**: `tele.__ef_migrations_history` (ensuring no collision with other schemas).

---

## 3. Asynchronous Metric Events

All events implement marker interface `IAppointmentMetricEvent`:

- `AppointmentCreatedMetricEvent(Guid AppointmentId, Guid ClinicId, Guid ProfessionalId, DateOnly MetricDate, string Status)`
- `AppointmentStatusChangedMetricEvent(Guid AppointmentId, Guid ClinicId, Guid ProfessionalId, DateOnly MetricDate, string OldStatus, string NewStatus)`

---

## 4. Background Processor & Atomic Upserts

The `AppointmentMetricsProcessorHostedService` processes the `IAppointmentMetricsQueue` channel asynchronously with zero write latency overhead.

### Upsert Logic

```sql
INSERT INTO tele.appointment_daily_metrics (metric_date, clinic_id, professional_id, status, total_count, last_updated_at)
VALUES
    (@date, @globalId, @globalId, @status, 1, NOW()),
    (@date, @clinicId, @professionalId, @status, 1, NOW())
ON CONFLICT (metric_date, clinic_id, professional_id, status)
DO UPDATE SET
    total_count = tele.appointment_daily_metrics.total_count + 1,
    last_updated_at = NOW();
```

---

## 5. Repository Optimization & Fallback

In `AppointmentRepository.cs`, `GetAppointmentStatsAsync` queries `tele.appointment_daily_metrics` directly, delivering instant $O(1)$ response times while falling back to `tele.appointments` if rollups are not yet present.

---

## 6. Backfill de métricas (recálculo autoritativo)

Endpoint admin: `POST /api/v1/telemedicine/admin/analytics/backfill`
(permiso `Appointments.AdminView`). Comando
`BackfillMetricsCommand(From?, To?, ClinicId?, DryRun=false)` →
`BackfillMetricsResult`.

- **Cuándo**: carga inicial tras crear las tablas, reparación de deriva (el
  processor incremental no descuenta reprogramaciones entre días ni recupera
  eventos perdidos) y recálculo tras cambios de reglas.
- **Semántica**: agrega `tele.appointments` por día de agenda (fecha UTC de
  `scheduled_start`) con las mismas claves/dimensiones del processor
  (`TelemedicineMetricKeys`: `daily_total`, `status_count`,
  `hourly_count/Hour_HH`, espejo global `Guid.Empty`) y **sobrescribe**
  (no suma). Idempotente y re-ejecutable.
- **Concurrencia**: si llegan eventos del processor durante la ejecución para
  días ya recalculados, el recálculo los pisa; **re-ejecutar una vez
  converge**. Correr en horario de bajo tráfico. Implementación en
  `Infrastructure/Metrics/MetricsBackfillService.cs` (SQL set-based, 2
  sentencias, transacción explícita corta vía `CreateExecutionStrategy`;
  `DryRun` hace rollback y solo reporta).
- **Rango**: `From`/`To` opcionales (default: historia completa → hoy),
  `ClinicId` opcional, validados por FluentValidation (rango ≤ 10 años).
