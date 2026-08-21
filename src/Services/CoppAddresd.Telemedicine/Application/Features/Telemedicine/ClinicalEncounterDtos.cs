using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Datos clínicos estructurados del encuentro (persistidos como <c>jsonb</c> en
/// <c>clinical_data</c>). Es un núcleo TIPADO pero EXTENSIBLE: el ERP puede
/// ampliar el esquema agregando campos a este record sin romper el modelo (el
/// jsonb es tolerante a campos desconocidos en la lectura). NO es un EHR
/// completo: solo la información clínica mínima de la consulta.
/// </summary>
public sealed record ClinicalDataDto(
    string? MotivoConsulta,
    string? Evaluacion,
    string? Diagnostico,
    string? Plan,
    string? Indicaciones,
    string? Observaciones,
    string? Seguimiento);

/// <summary>
/// Encuentro clínico de una cita para la API. El estado de este agregado es
/// independiente del de la cita y del de la sesión/sala: representa el registro
/// clínico, no la operativa de la consulta.
/// </summary>
public sealed record ClinicalEncounterDto(
    Guid Id,
    Guid AppointmentId,
    Guid? SessionId,
    Guid PatientId,
    Guid ProfessionalId,
    DateTimeOffset EncounterDate,
    EncounterStatus Status,
    ClinicalDataDto? ClinicalData,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
