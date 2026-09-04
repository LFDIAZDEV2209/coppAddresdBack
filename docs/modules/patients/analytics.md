# Patient Directory & ERP General — Analytics & CQRS Specification

This document details the analytical pre-aggregation architecture, event contracts, database schemas, and querying mechanics for the **Patient Directory & General Clinical ERP Module** (`app.` schema) in CoppAddresd.

---

## 1. Module Overview & Analytics Requirements

The Patient Directory module provides the core master data for clinical management. The executive ERP dashboard requires instant metrics for:
- **Total Patients by Status**: `active`, `inactive`, `pending`, `suspended`.
- **Risk Stratification**: Proportions of patient population across `low`, `moderate`, `high`, `critical` clinical risk levels.
- **Insurer Distribution**: Enrollment volume per health insurance carrier.
- **Gender & Demographics**: Breakdown of registered active patients.

---

## 2. Domain Entity & Schema Design

### Entity: `PatientDailyMetric`
Defined in `CoppAddresd.Domain.Entities.Patients.PatientDailyMetric`.

```csharp
public class PatientDailyMetric
{
    public DateOnly MetricDate { get; set; }
    public Guid ClinicId { get; set; }
    public string MetricKey { get; set; } = "";    // "status", "risk_level", "insurer", "gender"
    public string DimensionKey { get; set; } = ""; // "active", "high", "cigna", "male"
    public long TotalCount { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
```

### Table: `app.patient_daily_metrics`
- **Primary Key**: `(metric_date, clinic_id, metric_key, dimension_key)`

---

## 3. Asynchronous Metric Events

All events implement marker interface `IPatientMetricEvent`:
- `PatientCreatedMetricEvent(Guid PatientId, Guid? ClinicId, DateOnly MetricDate, string Status, string? RiskLevel, string? Insurer, string? Gender)`
- `PatientUpdatedMetricEvent(Guid PatientId, Guid? ClinicId, DateOnly MetricDate, string OldStatus, string NewStatus, string? OldRisk, string? NewRisk, string? OldInsurer, string? NewInsurer)`

---

## 4. Background Processor & Atomic Upserts

The `PatientMetricsProcessorHostedService` processes the `IPatientMetricsQueue` channel asynchronously, maintaining real-time daily rollups via atomic PostgreSQL upserts.

---

## 5. Repository Optimization & Fallback

In `PatientRepository.cs`, `GetStatsAsync` queries `app.patient_daily_metrics` in $O(1)$ time, bypassing table scans across `app.patient_profiles` with a graceful OLTP fallback.
