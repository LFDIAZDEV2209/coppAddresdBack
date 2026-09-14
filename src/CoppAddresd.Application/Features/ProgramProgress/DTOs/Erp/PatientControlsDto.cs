using System.Text.Json.Serialization;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Entities.ProgramProgress;

namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;

/// <summary>
/// Read model de los Controles del programa para el ERP (UC-004):
/// <c>GET /api/v1/program/erp/patients/{patientId}/controls</c>. Expone la
/// línea de tiempo de hitos con el ciclo de vida completo de cada control, el
/// control abierto vigente, el próximo vencimiento, la adherencia agregada y
/// el documento (lote de examen) asociado. snake_case explícito (misma
/// convención que <see cref="PatientOverviewDto"/>).
/// </summary>
public sealed record PatientControlsDto(
    [property: JsonPropertyName("enrollment")] PatientControlsEnrollmentDto Enrollment,
    [property: JsonPropertyName("current_control")] PatientControlsCurrentControlDto? CurrentControl,
    [property: JsonPropertyName("next_due")] PatientControlsNextDueDto? NextDue,
    [property: JsonPropertyName("milestones")] IReadOnlyList<PatientControlsMilestoneDto> Milestones,
    [property: JsonPropertyName("adherence")] PatientControlsAdherenceDto Adherence)
{
    /// <summary>
    /// Arma el contrato HTTP a partir del read model puro, la inscripción y el
    /// índice de documentos por control (los controles sin lote o con lote sin
    /// mediciones quedan con <c>document = null</c>).
    /// </summary>
    public static PatientControlsDto FromProgress(
        ProgramControlProgressSnapshot progress,
        ProgramEnrollment enrollment,
        IReadOnlyDictionary<Guid, PatientControlsDocumentDto> documents) =>
        new(
            new PatientControlsEnrollmentDto(
                enrollment.Id,
                enrollment.StartLocalDate,
                enrollment.Timezone,
                enrollment.Status.ToString()),
            progress.CurrentControl is { } current
                ? new PatientControlsCurrentControlDto(
                    current.ControlId,
                    current.MilestoneDay,
                    current.Status,
                    current.SentAt)
                : null,
            progress.NextDue is { } next
                ? new PatientControlsNextDueDto(next.MilestoneDay, next.TargetDate)
                : null,
            progress
                .Milestones.Select(m => new PatientControlsMilestoneDto(
                    m.MilestoneDay,
                    m.TargetDate,
                    m.Status,
                    m.ControlId,
                    m.SentAt,
                    m.RespondedAt,
                    m.FollowupSentAt,
                    m.CompletedAt,
                    m.ClosedReason,
                    m.ControlId is { } controlId
                    && documents.TryGetValue(controlId, out var document)
                        ? document
                        : null))
                .ToList(),
            new PatientControlsAdherenceDto(
                progress.Adherence.Completed,
                progress.Adherence.Missed,
                progress.Adherence.ClosedWithoutExam,
                progress.Adherence.Pending,
                progress.Adherence.Responded,
                progress.Adherence.FollowupsSent,
                progress.Adherence.MessagesSent));
}

/// <summary>Inscripción activa del paciente (contexto de la línea de tiempo).</summary>
public sealed record PatientControlsEnrollmentDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("start_local_date")] DateOnly StartLocalDate,
    [property: JsonPropertyName("timezone")] string Timezone,
    [property: JsonPropertyName("status")] string Status);

/// <summary>Control abierto vigente (null si no hay ninguno).</summary>
public sealed record PatientControlsCurrentControlDto(
    [property: JsonPropertyName("control_id")] Guid ControlId,
    [property: JsonPropertyName("milestone_day")] int MilestoneDay,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("sent_at")] DateTime? SentAt);

/// <summary>Próximo hito por cumplir (null si todos los días tienen fila).</summary>
public sealed record PatientControlsNextDueDto(
    [property: JsonPropertyName("milestone_day")] int MilestoneDay,
    [property: JsonPropertyName("target_date")] DateOnly TargetDate);

/// <summary>Entrada de la línea de tiempo: un día de hito (con o sin fila de control).</summary>
public sealed record PatientControlsMilestoneDto(
    [property: JsonPropertyName("milestone_day")] int MilestoneDay,
    [property: JsonPropertyName("target_date")] DateOnly TargetDate,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("control_id")] Guid? ControlId,
    [property: JsonPropertyName("sent_at")] DateTime? SentAt,
    [property: JsonPropertyName("responded_at")] DateTime? RespondedAt,
    [property: JsonPropertyName("followup_sent_at")] DateTime? FollowupSentAt,
    [property: JsonPropertyName("completed_at")] DateTime? CompletedAt,
    [property: JsonPropertyName("closed_reason")] string? ClosedReason,
    [property: JsonPropertyName("document")] PatientControlsDocumentDto? Document);

/// <summary>
/// Documento del lote de examen asociado a un control completado. El lote es
/// implícito: no hay tabla de lotes — lo definen las mediciones clínicas del
/// mismo <c>batch_id</c> junto con su clave S3 de origen (<c>source_key</c>).
/// </summary>
public sealed record PatientControlsDocumentDto(
    [property: JsonPropertyName("exam_batch_id")] Guid ExamBatchId,
    [property: JsonPropertyName("source_key")] string? SourceKey,
    [property: JsonPropertyName("observed_at")] DateTime? ObservedAt,
    [property: JsonPropertyName("measurement_count")] int MeasurementCount,
    [property: JsonPropertyName("metric_codes")] IReadOnlyList<string> MetricCodes)
{
    /// <summary>
    /// Proyecta el documento desde las mediciones del lote (lista ordenada por
    /// observación descendente): la fecha de observación es la más reciente y
    /// los códigos de métrica se deduplican y ordenan para que el payload sea
    /// determinista.
    /// </summary>
    public static PatientControlsDocumentDto FromMeasurements(
        Guid batchId,
        IReadOnlyList<PatientMeasurementDto> measurements) =>
        new(
            batchId,
            measurements
                .Select(m => m.SourceKey)
                .FirstOrDefault(sk => !string.IsNullOrWhiteSpace(sk)),
            measurements.Count > 0 ? measurements[0].ObservedAt : null,
            measurements.Count,
            measurements
                .Select(m => m.MetricCode)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToList());
}

/// <summary>Adherencia agregada del ciclo de controles de la inscripción.</summary>
public sealed record PatientControlsAdherenceDto(
    [property: JsonPropertyName("completed")] int Completed,
    [property: JsonPropertyName("missed")] int Missed,
    [property: JsonPropertyName("closed_without_exam")] int ClosedWithoutExam,
    [property: JsonPropertyName("pending")] int Pending,
    [property: JsonPropertyName("responded")] int Responded,
    [property: JsonPropertyName("followups_sent")] int FollowupsSent,
    [property: JsonPropertyName("messages_sent")] int MessagesSent);
