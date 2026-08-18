using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Crea un paciente (agregado completo: identidad, contacto, cobertura,
/// estilo de vida, diagnósticos, medicamentos, alergias y vitales). Los
/// catálogos se referencian por id y se validan contra la BD.
/// </summary>
public record CreatePatientCommand(
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
    IReadOnlyList<VitalSignInput>? VitalSigns)
    : IRequest<PatientDto>;

public sealed class CreatePatientCommandHandler(
    IPatientRepository repository,
    ICatalogRepository catalogs,
    ILogger<CreatePatientCommandHandler> logger) : IRequestHandler<CreatePatientCommand, PatientDto>
{
    public async Task<PatientDto> Handle(CreatePatientCommand request, CancellationToken ct)
    {
        await CatalogGuard.ValidateAsync(
            catalogs,
            request.DocumentTypeId,
            request.EthnicityId,
            request.BloodTypeId,
            request.CountryId,
            request.StateId,
            request.CityId,
            request.InsurerId,
            request.Diagnoses,
            request.Medications,
            request.Allergies,
            ct);

        var medicalRecordNumber = string.IsNullOrWhiteSpace(request.MedicalRecordNumber)
            ? GenerateMedicalRecordNumber()
            : request.MedicalRecordNumber.Trim();

        if (await repository.GetByMedicalRecordNumberAsync(medicalRecordNumber, ct) is not null)
        {
            throw new BusinessRuleViolationException(
                $"Ya existe un paciente con el número de historia clínica '{medicalRecordNumber}'.");
        }

        var entity = new PatientProfile
        {
            Id = Guid.NewGuid(),
            MedicalRecordNumber = medicalRecordNumber,
            FirstName = request.FirstName.Trim(),
            MiddleName = PatientOptions.Normalize(request.MiddleName),
            LastName = request.LastName.Trim(),
            DocumentTypeId = request.DocumentTypeId,
            DocumentNumber = PatientOptions.Normalize(request.DocumentNumber),
            DateOfBirth = request.DateOfBirth,
            Gender = PatientOptions.Normalize(request.Gender),
            EthnicityId = request.EthnicityId,
            BloodTypeId = request.BloodTypeId,
            PhoneCountryCode = PatientOptions.Normalize(request.PhoneCountryCode),
            PhoneNumber = PatientOptions.Normalize(request.PhoneNumber),
            Email = PatientOptions.Normalize(request.Email),
            Address = PatientOptions.Normalize(request.Address),
            CityId = request.CityId,
            StateId = request.StateId,
            CountryId = request.CountryId,
            PostalCode = PatientOptions.Normalize(request.PostalCode),
            EmergencyContact = PatientOptions.Normalize(request.EmergencyContact),
            InsurerId = request.InsurerId,
            MemberId = PatientOptions.Normalize(request.MemberId),
            MaritalStatus = PatientOptions.Normalize(request.MaritalStatus),
            SmokingStatus = PatientOptions.Normalize(request.SmokingStatus),
            AlcoholStatus = PatientOptions.Normalize(request.AlcoholStatus),
            ExerciseLevel = PatientOptions.Normalize(request.ExerciseLevel),
            Disability = PatientOptions.Normalize(request.Disability),
            HospitalizationHistory = PatientOptions.Normalize(request.HospitalizationHistory),
            SurgeryHistory = PatientOptions.Normalize(request.SurgeryHistory),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Activo" : request.Status.Trim(),
            Notes = PatientOptions.Normalize(request.Notes),
            CreatedAt = DateTime.UtcNow,
        };

        ApplyChildren(entity, request.Diagnoses, request.Medications, request.Allergies, request.VitalSigns);

        await repository.AddAsync(entity, ct);

        logger.LogInformation("Paciente creado: {Id} ({FirstName} {LastName})",
            entity.Id, entity.FirstName, entity.LastName);

        var created = await repository.GetByIdAsync(entity.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer el paciente creado.");

        return PatientDto.FromEntity(created);
    }

    private static string GenerateMedicalRecordNumber()
        => $"MRN-{Guid.NewGuid():N}"[..14].ToUpperInvariant();

    internal static void ApplyChildren(
        PatientProfile entity,
        IReadOnlyList<DiagnosisInput>? diagnoses,
        IReadOnlyList<MedicationInput>? medications,
        IReadOnlyList<AllergyInput>? allergies,
        IReadOnlyList<VitalSignInput>? vitalSigns)
    {
        var now = DateTime.UtcNow;

        entity.Diagnoses = (diagnoses ?? [])
            .Select(d => new PatientDiagnosis
            {
                Id = Guid.NewGuid(),
                Icd10CodeId = d.Icd10CodeId,
                IsPrimary = d.IsPrimary,
                CreatedAt = now,
            }).ToList();

        entity.Medications = (medications ?? [])
            .Select((m, index) => new PatientMedication
            {
                Id = Guid.NewGuid(),
                MedicationId = m.MedicationId,
                Frequency = m.Frequency,
                SortOrder = index,
                CreatedAt = now,
            }).ToList();

        entity.Allergies = (allergies ?? [])
            .Select(a => new PatientAllergy
            {
                Id = Guid.NewGuid(),
                AllergenId = a.AllergenId,
                Notes = PatientOptions.Normalize(a.Notes),
                CreatedAt = now,
            }).ToList();

        entity.VitalSigns = (vitalSigns ?? [])
            .Select(v => new VitalSign
            {
                Id = Guid.NewGuid(),
                // El JSON deserializa fechas sin zona (Kind=Unspecified) y
                // Npgsql exige Utc para timestamptz: se fija la zona aquí.
                MeasuredAt = v.MeasuredAt is { } measured
                    ? DateTime.SpecifyKind(measured, DateTimeKind.Utc)
                    : now,
                Systolic = v.Systolic,
                Diastolic = v.Diastolic,
                HeartRate = v.HeartRate,
                TemperatureC = v.TemperatureC,
                O2Saturation = v.O2Saturation,
                HeightCm = v.HeightCm,
                WeightKg = v.WeightKg,
                CreatedAt = now,
            }).ToList();
    }
}