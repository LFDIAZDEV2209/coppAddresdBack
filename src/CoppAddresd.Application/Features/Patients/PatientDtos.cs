using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>Diagnóstico de un paciente (DTO anidado del agregado).</summary>
public record DiagnosisDto(
    Guid Id,
    Guid Icd10CodeId,
    string Icd10Code,
    string? Description,
    bool IsPrimary)
{
    public static DiagnosisDto FromEntity(PatientDiagnosis entity) => new(
        entity.Id,
        entity.Icd10CodeId,
        entity.Icd10Code!.Code,
        entity.Icd10Code!.Description,
        entity.IsPrimary);
}

/// <summary>Medicamento de un paciente (DTO anidado del agregado).</summary>
public record MedicationDto(
    Guid Id,
    Guid MedicationId,
    string Name,
    string? Ndc,
    string? RxNorm,
    string? DrugClass,
    string? Frequency)
{
    public static MedicationDto FromEntity(PatientMedication entity) => new(
        entity.Id,
        entity.MedicationId,
        entity.Medication!.Name,
        entity.Medication!.Ndc,
        entity.Medication!.RxNorm,
        entity.Medication!.DrugClass,
        entity.Frequency);
}

/// <summary>Alergia de un paciente (DTO anidado del agregado).</summary>
public record AllergyDto(
    Guid Id,
    Guid AllergenId,
    string Allergen,
    string? Notes)
{
    public static AllergyDto FromEntity(PatientAllergy entity) => new(
        entity.Id,
        entity.AllergenId,
        entity.Allergen!.Name,
        entity.Notes);
}

/// <summary>Medición de signos vitales (DTO anidado del agregado).</summary>
public record VitalSignDto(
    Guid Id,
    DateTime MeasuredAt,
    int? Systolic,
    int? Diastolic,
    int? HeartRate,
    decimal? TemperatureC,
    int? O2Saturation,
    decimal? HeightCm,
    decimal? WeightKg)
{
    public static VitalSignDto FromEntity(VitalSign entity) => new(
        entity.Id,
        entity.MeasuredAt,
        entity.Systolic,
        entity.Diastolic,
        entity.HeartRate,
        entity.TemperatureC,
        entity.O2Saturation,
        entity.HeightCm,
        entity.WeightKg);
}

/// <summary>Representación completa de un paciente para la API (agregado).</summary>
public record PatientDto(
    Guid Id,
    string? MedicalRecordNumber,
    string FirstName,
    string? MiddleName,
    string LastName,
    Guid? DocumentTypeId,
    string? DocumentTypeName,
    string? DocumentNumber,
    DateTime? DateOfBirth,
    string? Gender,
    Guid? EthnicityId,
    string? EthnicityName,
    Guid? BloodTypeId,
    string? BloodTypeName,
    string? PhoneCountryCode,
    string? PhoneNumber,
    string? Email,
    string? Address,
    Guid? CityId,
    string? CityName,
    Guid? StateId,
    string? StateCode,
    Guid? CountryId,
    string? CountryName,
    string? PostalCode,
    string? EmergencyContact,
    Guid? InsurerId,
    string? InsurerName,
    string? MemberId,
    string? MaritalStatus,
    string? SmokingStatus,
    string? AlcoholStatus,
    string? ExerciseLevel,
    string? Disability,
    string? HospitalizationHistory,
    string? SurgeryHistory,
    string Status,
    string? Notes,
    Guid? ClinicId,
    string? ClinicName,
    Guid? LocationId,
    string? LocationName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<DiagnosisDto> Diagnoses,
    IReadOnlyList<MedicationDto> Medications,
    IReadOnlyList<AllergyDto> Allergies,
    IReadOnlyList<VitalSignDto> VitalSigns)
{
    public static PatientDto FromEntity(PatientProfile entity) => new(
        entity.Id,
        entity.MedicalRecordNumber,
        entity.FirstName,
        entity.MiddleName,
        entity.LastName,
        entity.DocumentTypeId,
        entity.DocumentType?.Name,
        entity.DocumentNumber,
        entity.DateOfBirth,
        entity.Gender,
        entity.EthnicityId,
        entity.Ethnicity?.Name,
        entity.BloodTypeId,
        entity.BloodType?.Code,
        entity.PhoneCountryCode,
        entity.PhoneNumber,
        entity.Email,
        entity.Address,
        entity.CityId,
        entity.City?.Name,
        entity.StateId,
        entity.State?.Code,
        entity.CountryId,
        entity.Country?.Name,
        entity.PostalCode,
        entity.EmergencyContact,
        entity.InsurerId,
        entity.Insurer?.Name,
        entity.MemberId,
        entity.MaritalStatus,
        entity.SmokingStatus,
        entity.AlcoholStatus,
        entity.ExerciseLevel,
        entity.Disability,
        entity.HospitalizationHistory,
        entity.SurgeryHistory,
        entity.Status,
        entity.Notes,
        entity.ClinicId,
        entity.Clinic?.Name,
        entity.LocationId,
        entity.Location?.Name,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.Diagnoses.OrderBy(d => d.IsPrimary ? 0 : 1).Select(DiagnosisDto.FromEntity).ToList(),
        entity.Medications.OrderBy(m => m.SortOrder).Select(MedicationDto.FromEntity).ToList(),
        entity.Allergies.Select(AllergyDto.FromEntity).ToList(),
        entity.VitalSigns.OrderByDescending(v => v.MeasuredAt).Select(VitalSignDto.FromEntity).ToList());
}

/// <summary>Fila del listado de pacientes (sin agregado completo).</summary>
public record PatientListItemDto(
    Guid Id,
    string? MedicalRecordNumber,
    string FirstName,
    string LastName,
    string? DocumentTypeName,
    string? DocumentNumber,
    DateTime? DateOfBirth,
    string? Gender,
    string? PhoneCountryCode,
    string? PhoneNumber,
    string? Email,
    Guid? ClinicId,
    string? ClinicName,
    string? InsurerName,
    string Status,
    DateTime CreatedAt)
{
    public static PatientListItemDto FromEntity(PatientProfile entity) => new(
        entity.Id,
        entity.MedicalRecordNumber,
        entity.FirstName,
        entity.LastName,
        entity.DocumentType?.Name,
        entity.DocumentNumber,
        entity.DateOfBirth,
        entity.Gender,
        entity.PhoneCountryCode,
        entity.PhoneNumber,
        entity.Email,
        entity.ClinicId,
        entity.Clinic?.Name,
        entity.Insurer?.Name,
        entity.Status,
        entity.CreatedAt);
}

/// <summary>Resultado paginado del listado de pacientes.</summary>
public record PaginatedPatientsResult(
    IReadOnlyList<PatientListItemDto> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>Aseguradora del catálogo.</summary>
public record InsurerDto(Guid Id, string Name)
{
    public static InsurerDto FromEntity(Insurer entity) => new(entity.Id, entity.Name);
}

/// <summary>Payload de creación de paciente (agregado completo).</summary>
public record CreatePatientRequest(
    string? MedicalRecordNumber,
    string FirstName,
    string? MiddleName,
    string LastName,
    Guid? DocumentTypeId,
    string? DocumentNumber,
    DateTime? DateOfBirth,
    string? Gender,
    Guid? EthnicityId,
    Guid? BloodTypeId,
    string? PhoneCountryCode,
    string? PhoneNumber,
    string? Email,
    string? Address,
    Guid? CityId,
    Guid? StateId,
    Guid? CountryId,
    string? PostalCode,
    string? EmergencyContact,
    Guid? InsurerId,
    string? MemberId,
    string? MaritalStatus,
    string? SmokingStatus,
    string? AlcoholStatus,
    string? ExerciseLevel,
    string? Disability,
    string? HospitalizationHistory,
    string? SurgeryHistory,
    string? Status,
    string? Notes,
    IReadOnlyList<DiagnosisInput>? Diagnoses,
    IReadOnlyList<MedicationInput>? Medications,
    IReadOnlyList<AllergyInput>? Allergies,
    IReadOnlyList<VitalSignInput>? VitalSigns);

/// <summary>Payload de actualización de paciente (agregado completo).</summary>
public record UpdatePatientRequest(
    string? MedicalRecordNumber,
    string FirstName,
    string? MiddleName,
    string LastName,
    Guid? DocumentTypeId,
    string? DocumentNumber,
    DateTime? DateOfBirth,
    string? Gender,
    Guid? EthnicityId,
    Guid? BloodTypeId,
    string? PhoneCountryCode,
    string? PhoneNumber,
    string? Email,
    string? Address,
    Guid? CityId,
    Guid? StateId,
    Guid? CountryId,
    string? PostalCode,
    string? EmergencyContact,
    Guid? InsurerId,
    string? MemberId,
    string? MaritalStatus,
    string? SmokingStatus,
    string? AlcoholStatus,
    string? ExerciseLevel,
    string? Disability,
    string? HospitalizationHistory,
    string? SurgeryHistory,
    string? Status,
    string? Notes,
    IReadOnlyList<DiagnosisInput>? Diagnoses,
    IReadOnlyList<MedicationInput>? Medications,
    IReadOnlyList<AllergyInput>? Allergies,
    IReadOnlyList<VitalSignInput>? VitalSigns);

/// <summary>Diagnóstico enviado por el cliente (referencia al catálogo ICD-10).</summary>
public record DiagnosisInput(Guid Icd10CodeId, bool IsPrimary = false);

/// <summary>Medicamento enviado por el cliente (referencia al catálogo).</summary>
public record MedicationInput(Guid MedicationId, string? Frequency);

/// <summary>Alergia enviada por el cliente (referencia al catálogo).</summary>
public record AllergyInput(Guid AllergenId, string? Notes);

/// <summary>Medición de signos vitales enviada por el cliente.</summary>
public record VitalSignInput(
    DateTime? MeasuredAt,
    int? Systolic,
    int? Diastolic,
    int? HeartRate,
    decimal? TemperatureC,
    int? O2Saturation,
    decimal? HeightCm,
    decimal? WeightKg);

/// <summary>Payload de asignación de un profesional a un paciente.</summary>
public record AssignPatientProfessionalRequest(Guid ProfessionalId, string? RelationshipType);