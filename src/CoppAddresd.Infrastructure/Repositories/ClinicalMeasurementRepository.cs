using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.DTOs.LabExam;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Ancla de un lote de mediciones: fila de <c>app.clinical_measurements</c>
/// cuyo Id está referenciado por <c>task_completions.vital_signs_batch_id</c>.
/// El par (Source, ObservedAt) identifica el lote para el remap de
/// <c>batchId</c> (PersistVitalsAsync escribe un mismo <c>ObservedAt</c> por
/// lote, por lo que la igualdad es exacta, no por ventana de segundos).
/// </summary>
public sealed record MeasurementAnchor(Guid Id, DateTime ObservedAt, string Source);

/// <summary>
/// Repositorio de mediciones clínicas: contexto de IA (proyección a DTO en una
/// sola query) y contexto ERP (lista plana con <c>batchId</c> derivado de los
/// anclas de lote). Sin N+1.
/// </summary>
public sealed class ClinicalMeasurementRepository(AppDbContext dbContext)
    : IClinicalMeasurementRepository
{
    public async Task<IReadOnlyList<ClinicalMeasurementDto>> ListByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .ClinicalMeasurements.AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.ObservedAt)
            .Select(x => new ClinicalMeasurementDto(
                x.Metric!.Code,
                x.Value,
                x.Unit!.Code,
                x.ObservedAt
            ))
            .ToListAsync(ct);

    public async Task AddBatchAsync(
        IReadOnlyList<ClinicalMeasurement> measurements,
        CancellationToken ct = default
    )
    {
        if (measurements.Count == 0)
        {
            return;
        }

        await dbContext.ClinicalMeasurements.AddRangeAsync(measurements, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MeasurementMetric>> GetActiveMetricsWithUnitsAsync(
        CancellationToken ct = default
    )
    {
        return await dbContext
            .MeasurementMetrics.AsNoTracking()
            .Include(m => m.DefaultUnit)
            .Where(m => m.IsActive)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Upsert por día local: lee las filas del día (una sola query para todas
    /// las métricas) y actualiza o inserta. El índice único parcial no existe
    /// en BD, así que la idempotencia se garantiza en esta única ruta de
    /// escritura del móvil (Source = "device").
    /// </summary>
    public async Task UpsertDailyDeviceMetricsAsync(
        Guid patientId,
        IReadOnlyList<DailyDeviceMetric> rows,
        DateTime dayStartUtc,
        DateTime dayEndUtc,
        DateTime observedAt,
        Guid actorId,
        string source,
        CancellationToken ct = default
    )
    {
        if (rows.Count == 0)
        {
            return;
        }

        var metricIds = rows.Select(r => r.MetricId).Distinct().ToArray();
        var existing = await dbContext
            .ClinicalMeasurements.Where(m =>
                m.PatientId == patientId
                && metricIds.Contains(m.MetricId)
                && m.Source == source
                && m.ObservedAt >= dayStartUtc
                && m.ObservedAt < dayEndUtc
            )
            .ToListAsync(ct);

        var byMetric = existing.GroupBy(m => m.MetricId).ToDictionary(g => g.Key, g => g.First());
        var now = DateTime.UtcNow;

        foreach (var row in rows)
        {
            if (byMetric.TryGetValue(row.MetricId, out var current))
            {
                current.Value = row.Value;
                current.UnitId = row.UnitId;
                current.ObservedAt = observedAt;
                current.RecordedAt = now;
                continue;
            }

            dbContext.ClinicalMeasurements.Add(
                new ClinicalMeasurement
                {
                    Id = Guid.NewGuid(),
                    PatientId = patientId,
                    MetricId = row.MetricId,
                    UnitId = row.UnitId,
                    Value = row.Value,
                    ObservedAt = observedAt,
                    RecordedAt = now,
                    CreatedAt = now,
                    CreatedBy = actorId,
                    Source = source,
                }
            );
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UnitOfMeasure>> GetActiveUnitsAsync(
        CancellationToken ct = default
    )
    {
        return await dbContext.UnitOfMeasures.AsNoTracking().Where(u => u.IsActive).ToListAsync(ct);
    }

    /// <summary>
    /// Última medición por métrica (R1). GroupBy + OrderByDescending + First se
    /// traduce a <c>SELECT DISTINCT ON (code) ... ORDER BY code, observed_at DESC,
    /// recorded_at DESC</c> en Npgsql: una sola query con desempate determinista.
    /// </summary>
    /// <remarks>
    /// La guarda 3VL es obligatoria: <c>m.BatchId != excludeBatchId</c> por sí sola
    /// evalúa <c>NULL != guid</c> ⇒ UNKNOWN y descarta silenciosamente las filas con
    /// <c>batch_id</c> NULL (device/checkin/manual). La rama explícita
    /// <c>m.BatchId == null</c> conserva ese historial cross-source (spec
    /// "History found across sources").
    /// </remarks>
    public async Task<IReadOnlyDictionary<string, LabExamMetricSnapshot>> GetLastPerMetricAsync(
        Guid patientId,
        IEnumerable<string> metricNames,
        Guid excludeBatchId,
        CancellationToken ct = default
    )
    {
        var names = metricNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (names.Length == 0)
        {
            return new Dictionary<string, LabExamMetricSnapshot>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await dbContext
            .ClinicalMeasurements.AsNoTracking()
            .Where(m =>
                m.PatientId == patientId
                && (m.BatchId == null || m.BatchId != excludeBatchId)
                && names.Contains(m.Metric!.Code)
            )
            .Select(m => new
            {
                Code = m.Metric!.Code,
                m.Value,
                UnitSymbol = m.Unit!.Symbol,
                m.ObservedAt,
                m.RecordedAt,
            })
            .GroupBy(x => x.Code)
            .Select(g =>
                g.OrderByDescending(x => x.ObservedAt).ThenByDescending(x => x.RecordedAt).First()
            )
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.Code,
            r => new LabExamMetricSnapshot(r.Code, r.Value, r.UnitSymbol, r.ObservedAt),
            StringComparer.OrdinalIgnoreCase
        );
    }

    /// <summary>
    /// Proyección ERP de las mediciones de un paciente. Dos queries set-based:
    /// Q1 anclas de lote del paciente (filas de clinical_measurements cuyo Id
    /// referencia task_completions.vital_signs_batch_id vía la inscripción) y
    /// Q2 las filas del paciente en una proyección con los joins de catálogo
    /// (métrica y unidad), ordenadas por ObservedAt desc / RecordedAt desc. El
    /// <c>batchId</c> se remapea en memoria (función pura
    /// <see cref="ResolveBatchId"/>): EncounterId → ancla propia → match exacto
    /// (Source, ObservedAt) con tie-break Min(AnchorId) → null.
    /// Con <paramref name="batchId"/> provisto (lote de examen, UC-004) se
    /// filtra <c>WHERE batch_id = @batchId</c> y se omite Q1: las filas del
    /// lote ya traen su BatchId y no necesitan el remap por anclas de check-in.
    /// </summary>
    public async Task<IReadOnlyList<PatientMeasurementDto>> ListForErpAsync(
        Guid patientId,
        Guid? batchId = null,
        CancellationToken ct = default
    )
    {
        // Q1: anclas del paciente. Un solo query: la subconsulta DISTINCT de
        // vital_signs_batch_id (⋈ program_enrollments.patient_id) se traduce a
        // un IN sobre el Id de la medición.
        var anchors = new List<MeasurementAnchor>();
        if (batchId is null)
        {
            anchors = await dbContext
                .ClinicalMeasurements.AsNoTracking()
                .Where(m =>
                    dbContext
                        .TaskCompletions.AsNoTracking()
                        .Where(tc =>
                            tc.VitalSignsBatchId != null && tc.Enrollment!.PatientId == patientId
                        )
                        .Select(tc => tc.VitalSignsBatchId!.Value)
                        .Distinct()
                        .Contains(m.Id)
                )
                .Select(m => new MeasurementAnchor(m.Id, m.ObservedAt, m.Source))
                .ToListAsync(ct);
        }
        var anchorIds = anchors.Select(a => a.Id).ToHashSet();

        // Q2: filas del paciente en una proyección (joins con métrica y unidad),
        // ordenadas por observación desc y registro desc. AsNoTracking.
        var query = dbContext
            .ClinicalMeasurements.AsNoTracking()
            .Where(m => m.PatientId == patientId);

        if (batchId is { } labBatchId)
        {
            query = query.Where(m => m.BatchId == labBatchId);
        }

        var rows = await query
            .OrderByDescending(m => m.ObservedAt)
            .ThenByDescending(m => m.RecordedAt)
            .Select(m => new PatientMeasurementDto(
                m.Id,
                m.Metric!.Code,
                m.Metric!.Name,
                m.Value,
                m.Unit!.Code,
                m.Unit!.Symbol,
                m.ObservedAt,
                m.Source,
                m.BatchId ?? m.EncounterId,
                m.SourceKey
            ))
            .ToListAsync(ct);

        // Remap en memoria: el BatchId proyectado (BatchId ?? EncounterId) se reemplaza
        // solo cuando es null. La derivación es la función pura ResolveBatchId.
        return rows.Select(r =>
            {
                var resolvedBatchId = ResolveBatchId(
                    r.Id,
                    r.BatchId,
                    r.Source,
                    r.ObservedAt,
                    anchorIds,
                    anchors
                );
                return resolvedBatchId == r.BatchId ? r : r with { BatchId = resolvedBatchId };
            })
            .ToList();
    }

    /// <summary>
    /// Variante set-based de lotes (UC-004): filas del paciente restringidas a
    /// VARIOS lotes en UNA query. <c>ids.Contains(m.BatchId.Value)</c> con la
    /// guarda 3VL (<c>BatchId != null</c>) se traduce a
    /// <c>WHERE batch_id IS NOT NULL AND batch_id = ANY(@ids)</c>: sin anclas
    /// (las filas del lote ya traen su BatchId) ni remap en memoria. Misma
    /// proyección y orden que la sobrecarga de un lote.
    /// </summary>
    public async Task<IReadOnlyList<PatientMeasurementDto>> ListForErpAsync(
        Guid patientId,
        IReadOnlyCollection<Guid> batchIds,
        CancellationToken ct = default
    )
    {
        var ids = batchIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        return await dbContext
            .ClinicalMeasurements.AsNoTracking()
            .Where(m =>
                m.PatientId == patientId && m.BatchId != null && ids.Contains(m.BatchId.Value)
            )
            .OrderByDescending(m => m.ObservedAt)
            .ThenByDescending(m => m.RecordedAt)
            .Select(m => new PatientMeasurementDto(
                m.Id,
                m.Metric!.Code,
                m.Metric!.Name,
                m.Value,
                m.Unit!.Code,
                m.Unit!.Symbol,
                m.ObservedAt,
                m.Source,
                m.BatchId,
                m.SourceKey
            ))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Derivación pura del <c>batchId</c> de una fila (D1 del diseño):
    /// 1) <paramref name="existingBatchId"/> no nulo → el batch o encuentro existente;
    /// 2) la fila es ancla (su Id está en <paramref name="anchorIds"/>) → su Id;
    /// 3) match EXACTO de (Source, ObservedAt) contra las anclas → el Id de
    ///    ancla menor (Min, determinista ante lotes del mismo instante);
    /// 4) sin match → null. Orden equivalente al del sketch del diseño
    ///    (EncounterId/BatchId tiene precedencia vía la proyección; las anclas siempre
    ///    tienen EncounterId/BatchId null).
    /// </summary>
    public static Guid? ResolveBatchId(
        Guid rowId,
        Guid? encounterId,
        string source,
        DateTime observedAt,
        IReadOnlyCollection<Guid> anchorIds,
        IReadOnlyCollection<MeasurementAnchor> anchors
    )
    {
        if (encounterId is not null)
        {
            return encounterId;
        }

        if (anchorIds.Contains(rowId))
        {
            return rowId;
        }

        return anchors
            .Where(a => a.Source == source && a.ObservedAt == observedAt)
            .Select(a => (Guid?)a.Id)
            .Min();
    }
}
