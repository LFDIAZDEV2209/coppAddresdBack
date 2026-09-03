using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
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
public sealed class ClinicalMeasurementRepository(AppDbContext dbContext) : IClinicalMeasurementRepository
{
    public async Task<IReadOnlyList<ClinicalMeasurementDto>> ListByPatientAsync(
        Guid patientId,
        CancellationToken ct = default)
        => await dbContext.ClinicalMeasurements
            .AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.ObservedAt)
            .Select(x => new ClinicalMeasurementDto(
                x.Metric!.Code,
                x.Value,
                x.Unit!.Code,
                x.ObservedAt))
            .ToListAsync(ct);

    /// <summary>
    /// Proyección ERP de las mediciones de un paciente. Dos queries set-based:
    /// Q1 anclas de lote del paciente (filas de clinical_measurements cuyo Id
    /// referencia task_completions.vital_signs_batch_id vía la inscripción) y
    /// Q2 las filas del paciente en una proyección con los joins de catálogo
    /// (métrica y unidad), ordenadas por ObservedAt desc / RecordedAt desc. El
    /// <c>batchId</c> se remapea en memoria (función pura
    /// <see cref="ResolveBatchId"/>): EncounterId → ancla propia → match exacto
    /// (Source, ObservedAt) con tie-break Min(AnchorId) → null.
    /// </summary>
    public async Task<IReadOnlyList<PatientMeasurementDto>> ListForErpAsync(
        Guid patientId,
        CancellationToken ct = default)
    {
        // Q1: anclas del paciente. Un solo query: la subconsulta DISTINCT de
        // vital_signs_batch_id (⋈ program_enrollments.patient_id) se traduce a
        // un IN sobre el Id de la medición.
        var anchors = await dbContext.ClinicalMeasurements.AsNoTracking()
            .Where(m => dbContext.TaskCompletions.AsNoTracking()
                .Where(tc => tc.VitalSignsBatchId != null && tc.Enrollment!.PatientId == patientId)
                .Select(tc => tc.VitalSignsBatchId!.Value)
                .Distinct()
                .Contains(m.Id))
            .Select(m => new MeasurementAnchor(m.Id, m.ObservedAt, m.Source))
            .ToListAsync(ct);
        var anchorIds = anchors.Select(a => a.Id).ToHashSet();

        // Q2: filas del paciente en una proyección (joins con métrica y unidad),
        // ordenadas por observación desc y registro desc. AsNoTracking.
        var rows = await dbContext.ClinicalMeasurements.AsNoTracking()
            .Where(m => m.PatientId == patientId)
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
                m.EncounterId))
            .ToListAsync(ct);

        // Remap en memoria: el BatchId proyectado (EncounterId) se reemplaza
        // solo cuando es null. La derivación es la función pura ResolveBatchId.
        return rows
            .Select(r =>
            {
                var batchId = ResolveBatchId(
                    r.Id, r.BatchId, r.Source, r.ObservedAt, anchorIds, anchors);
                return batchId == r.BatchId ? r : r with { BatchId = batchId };
            })
            .ToList();
    }

    /// <summary>
    /// Derivación pura del <c>batchId</c> de una fila (D1 del diseño):
    /// 1) <paramref name="encounterId"/> no nulo → el encuentro;
    /// 2) la fila es ancla (su Id está en <paramref name="anchorIds"/>) → su Id;
    /// 3) match EXACTO de (Source, ObservedAt) contra las anclas → el Id de
    ///    ancla menor (Min, determinista ante lotes del mismo instante);
    /// 4) sin match → null. Orden equivalente al del sketch del diseño
    ///    (EncounterId tiene precedencia vía la proyección; las anclas siempre
    ///    tienen EncounterId null).
    /// </summary>
    public static Guid? ResolveBatchId(
        Guid rowId,
        Guid? encounterId,
        string source,
        DateTime observedAt,
        IReadOnlyCollection<Guid> anchorIds,
        IReadOnlyCollection<MeasurementAnchor> anchors)
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