namespace CoppAddresd.Application.Features.Patients.Events;

/// <summary>
/// Contrato marcador para eventos de métricas de pacientes procesados asíncronamente en background (0ms overhead).
/// </summary>
public interface IPatientMetricEvent;

/// <summary>Evento emitido al registrar un nuevo paciente.</summary>
public sealed record PatientRegisteredMetricEvent(
    Guid PatientId,
    Guid? ClinicId,
    string? Gender,
    DateTime? DateOfBirth,
    Guid? InsurerId,
    string Status,
    DateTime CreatedAtUtc
) : IPatientMetricEvent;

/// <summary>Evento emitido cuando cambia el estado operativo del paciente (Activo, Inactivo, etc.).</summary>
public sealed record PatientStatusChangedMetricEvent(
    Guid PatientId,
    Guid? ClinicId,
    string OldStatus,
    string NewStatus,
    DateTime ChangedAtUtc
) : IPatientMetricEvent;

/// <summary>Evento emitido cuando se asigna o desasigna un profesional médico a un paciente.</summary>
public sealed record PatientAssignmentMetricEvent(
    Guid PatientId,
    Guid? ClinicId,
    Guid ProfessionalId,
    bool IsAssigned, // true = asignado, false = desasignado
    DateTime ChangedAtUtc
) : IPatientMetricEvent;
