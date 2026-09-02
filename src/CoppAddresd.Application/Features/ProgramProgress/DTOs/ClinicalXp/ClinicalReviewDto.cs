using System.Text.Json.Serialization;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.ClinicalXp;

/// <summary>
/// Revisión clínica de XP visible para el clínico (SPEC §15): una mejoría
/// significativa detectada por el motor en <c>POST /scores/calculate</c> que
/// espera decisión (<c>pending</c>). Incluye el paciente, la métrica (código +
/// nombre), el |Δ%| observado y el período del Índice de Salud en que se
/// detectó. El clínico decide con
/// <c>POST /api/v1/program/xp-rules/clinical-pending/{id}/decide</c>.
/// </summary>
public sealed record ClinicalReviewDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("patientId")] Guid PatientId,
    [property: JsonPropertyName("metricId")] Guid MetricId,
    [property: JsonPropertyName("metricCode")] string MetricCode,
    [property: JsonPropertyName("metricName")] string MetricName,
    [property: JsonPropertyName("deltaPct")] decimal? DeltaPct,
    [property: JsonPropertyName("ruleCode")] string RuleCode,
    [property: JsonPropertyName("status")] ClinicalXpReviewStatus Status,
    [property: JsonPropertyName("healthScorePeriodStart")] DateOnly HealthScorePeriodStart,
    [property: JsonPropertyName("healthScorePeriodEnd")] DateOnly HealthScorePeriodEnd,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("decidedBy")] Guid? DecidedBy,
    [property: JsonPropertyName("decidedAt")] DateTime? DecidedAt,
    [property: JsonPropertyName("patient_name")] string? PatientName = null);