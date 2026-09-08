# Health Tests & Clinical Batteries — Analytics & CQRS Specification

This document details the analytical pre-aggregation architecture, event contracts, database schemas, and querying mechanics for the **Health Tests & Clinical Batteries Module** (`app.` schema) in CoppAddresd.

---

## 1. Module Overview & Analytics Requirements

The Health Tests module governs psychometric and clinical evaluation batteries (e.g. ORP, DASS-21, WHOQOL, GAD-7, PHQ-9). The ERP dashboard requires instant $O(1)$ metrics for:
- **Assignment Funnel**: Pending, In Progress, Completed, and Cancelled tests.
- **Severity Distribution**: Proportions of test results categorized as `none`, `mild`, `moderate`, `severe`, `critical`.
- **Clinical Alerts Status**: Active, Reviewing, Resolved, and Closed risk alerts.
- **Instrument Throughput**: Volume of completed evaluations per test code.

---

## 2. Domain Entity & Schema Design

### Entity: `HealthTestDailyMetric`
Defined in `CoppAddresd.Domain.Entities.HealthTests.HealthTestDailyMetric`.

```csharp
public class HealthTestDailyMetric
{
    public DateOnly MetricDate { get; set; }
    public Guid ClinicId { get; set; }          // Guid.Empty represents Global aggregated rollups
    public string MetricKey { get; set; } = "";  // "assignments_count", "severity_count", "alerts_count", "test_completions"
    public string DimensionKey { get; set; } = ""; // "pending", "completed", "severe", "orp", etc.
    public long TotalCount { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
```

### Table: `app.health_test_daily_metrics`
- **Primary Key**: `(metric_date, clinic_id, metric_key, dimension_key)`
- **Indices**: B-tree index on `(metric_key, dimension_key, clinic_id)`.

---

## 3. Asynchronous Metric Events

All events implement marker interface `IHealthTestMetricEvent`:
- `HealthTestAssignedMetricEvent(Guid AssignmentId, Guid PatientId, Guid? ClinicId, DateOnly MetricDate)`: Emitted when individual tests or complete batteries are assigned.
- `HealthTestCompletedMetricEvent(Guid EvaluationId, Guid PatientId, Guid? ClinicId, DateOnly MetricDate, string? TestCode, HealthTestSeverity? Severity, int CreatedAlertsCount)`: Emitted upon score calculation and evaluation completion.
- `HealthTestAlertTransitionedMetricEvent(Guid AlertId, Guid PatientId, Guid? ClinicId, DateOnly MetricDate, HealthTestAlertStatus OldStatus, HealthTestAlertStatus NewStatus)`: Emitted when a clinician updates an alert's lifecycle.

---

## 4. Background Processor & Atomic Upserts

The `HealthTestMetricsProcessorHostedService` processes the `IHealthTestMetricsQueue` channel asynchronously.

### Upsert Logic Example (Evaluation Completion)
```sql
INSERT INTO app.health_test_daily_metrics (metric_date, clinic_id, metric_key, dimension_key, total_count, last_updated_at)
VALUES 
    (@date, @globalId, 'assignments_count', 'completed', 1, NOW()),
    (@date, @globalId, 'evaluations_count', 'completed', 1, NOW()),
    (@date, @globalId, 'test_completions', @testCode, 1, NOW()),
    (@date, @clinicId, 'assignments_count', 'completed', 1, NOW()),
    (@date, @clinicId, 'evaluations_count', 'completed', 1, NOW()),
    (@date, @clinicId, 'test_completions', @testCode, 1, NOW())
ON CONFLICT (metric_date, clinic_id, metric_key, dimension_key)
DO UPDATE SET 
    total_count = app.health_test_daily_metrics.total_count + 1,
    last_updated_at = NOW();

UPDATE app.health_test_daily_metrics 
SET total_count = GREATEST(0, total_count - 1), last_updated_at = NOW()
WHERE metric_date = @date AND metric_key = 'assignments_count' AND (dimension_key = 'pending' OR dimension_key = 'in_progress')
  AND (clinic_id = @globalId OR clinic_id = @clinicId);
```

---

## 5. Repository Optimization & Fallback

In `HealthTestRepository.cs`, analytical methods inspect `app.health_test_daily_metrics` first:
```csharp
public async Task<int> CountAssignmentsByStatusAsync(HealthTestAssignmentStatus status, CancellationToken ct = default)
{
    var statusKey = status.ToString().ToLowerInvariant();
    var preAggSum = await dbContext.HealthTestDailyMetrics.AsNoTracking()
        .Where(x => x.MetricKey == "assignments_count" && x.DimensionKey == statusKey && x.ClinicId == Guid.Empty)
        .SumAsync(x => (int?)x.TotalCount, ct);

    if (preAggSum.HasValue && preAggSum.Value > 0)
        return preAggSum.Value;

    // Resilient fallback to live OLTP table
    return await dbContext.HealthTestAssignments.AsNoTracking().CountAsync(x => x.Status == status, ct);
}
```
