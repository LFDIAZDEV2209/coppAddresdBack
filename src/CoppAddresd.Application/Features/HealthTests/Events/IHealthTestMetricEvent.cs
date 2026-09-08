using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests.Events;

/// <summary>
/// Contrato marcador para eventos de métricas de Tests de Salud procesados en background con 0ms de bloqueo.
/// </summary>
public interface IHealthTestMetricEvent;

/// <summary>Evento emitido al asignar un test a un paciente.</summary>
public sealed record HealthTestAssignedMetricEvent(
    Guid AssignmentId,
    Guid PatientId,
    Guid? ClinicId,
    DateOnly MetricDate
) : IHealthTestMetricEvent;

/// <summary>Evento emitido al completar y evaluar un test.</summary>
public sealed record HealthTestCompletedMetricEvent(
    Guid EvaluationId,
    Guid PatientId,
    Guid? ClinicId,
    DateOnly MetricDate,
    string? TestCode,
    HealthTestSeverity? Severity,
    int CreatedAlertsCount
) : IHealthTestMetricEvent;

/// <summary>Evento emitido al cambiar el estado de una alerta médica.</summary>
public sealed record HealthTestAlertTransitionedMetricEvent(
    Guid AlertId,
    Guid PatientId,
    Guid? ClinicId,
    DateOnly MetricDate,
    HealthTestAlertStatus OldStatus,
    HealthTestAlertStatus NewStatus
) : IHealthTestMetricEvent;
