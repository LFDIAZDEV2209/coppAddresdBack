using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Entities.ProgramProgress;
using MediatR;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;

/// <summary>
/// Controles del programa de un paciente para el ERP (UC-004): línea de tiempo
/// de los hitos configurados, control abierto vigente, próximo vencimiento,
/// adherencia agregada y el documento del lote de examen de cada control
/// completado. Devuelve null si el paciente no tiene inscripción activa (el
/// controlador responde 404, igual que el overview 360).
/// </summary>
public sealed record GetPatientControlsQuery(Guid PatientId) : IRequest<PatientControlsDto?>;

public sealed class GetPatientControlsQueryHandler(
    IProgramRepository programRepository,
    IProgramControlRepository controlRepository,
    IClinicalMeasurementRepository measurementRepository,
    IOptions<ProgramControlSettings> settings,
    Func<DateTime>? utcNow = null
) : IRequestHandler<GetPatientControlsQuery, PatientControlsDto?>
{
    // El límite del repositorio es 500; una inscripción tiene a lo sumo una
    // fila por día configurado, así que el tope nunca recorta la línea real.
    private const int MaxControls = 500;

    private readonly Func<DateTime> _utcNow = utcNow ?? (() => DateTime.UtcNow);

    public async Task<PatientControlsDto?> Handle(
        GetPatientControlsQuery request,
        CancellationToken ct)
    {
        // Misma resolución de inscripción que el overview ERP: la activa más
        // reciente del paciente (sin inscripción no hay línea de tiempo → 404).
        var enrollment = await programRepository.GetActiveEnrollmentForPatientAsync(
            request.PatientId, ct);
        if (enrollment is null)
        {
            return null;
        }

        var controls = await controlRepository.ListAsync(
            enrollmentId: enrollment.Id, limit: MaxControls, ct: ct);

        var progress = ProgramControlProgress.Build(
            controls, settings.Value.Days, enrollment.StartLocalDate, _utcNow());

        var documents = await LoadDocumentsAsync(request.PatientId, controls, ct);
        return PatientControlsDto.FromProgress(progress, enrollment, documents);
    }

    /// <summary>
    /// Documento por control en UNA sola query set-based (sin N+1): se traen
    /// las mediciones de TODOS los lotes de examen referenciados por los
    /// controles (<c>WHERE batch_id = ANY(@ids)</c>) y se agrupan en memoria
    /// por <c>BatchId</c>. Un control sin <c>ExamBatchId</c> o cuyo lote no
    /// tiene mediciones queda sin documento (null).
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, PatientControlsDocumentDto>> LoadDocumentsAsync(
        Guid patientId,
        IReadOnlyList<ProgramControl> controls,
        CancellationToken ct)
    {
        var batchIds = controls
            .Where(c => c.ExamBatchId is not null)
            .Select(c => c.ExamBatchId!.Value)
            .Distinct()
            .ToList();

        if (batchIds.Count == 0)
        {
            return new Dictionary<Guid, PatientControlsDocumentDto>();
        }

        var measurements = await measurementRepository.ListForErpAsync(patientId, batchIds, ct);
        var measurementsByBatch = measurements
            .Where(m => m.BatchId is not null)
            .GroupBy(m => m.BatchId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PatientMeasurementDto>)group.ToList());

        var documents = new Dictionary<Guid, PatientControlsDocumentDto>();
        foreach (var control in controls.Where(c => c.ExamBatchId is not null))
        {
            var batchId = control.ExamBatchId!.Value;
            if (measurementsByBatch.TryGetValue(batchId, out var batchMeasurements))
            {
                documents[control.Id] = PatientControlsDocumentDto.FromMeasurements(
                    batchId, batchMeasurements);
            }
        }

        return documents;
    }
}
