using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Crea un paciente (agregado completo: identidad, contacto, cobertura,
/// estilo de vida, diagnósticos, medicamentos, alergias y vitales).
/// </summary>
public record CreatePatientCommand(
    string? MedicalRecordNumber,
    string FirstName,
    string? MiddleName,
    string LastName,
    string? DocumentType,
    string? DocumentNumber,
    DateTime? DateOfBirth,
    string? Gender,
    string? Ethnicity,
    string? BloodType,
    string? Phone,
    string? Email,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    string? EmergencyContact,
    Guid? InsurerId,
    string? MemberId,
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
    ILogger<CreatePatientCommandHandler> logger) : IRequestHandler<CreatePatientCommand, PatientDto>
{
    public async Task<PatientDto> Handle(CreatePatientCommand request, CancellationToken ct)
    {
        if (request.InsurerId is not null &&
            await repository.GetInsurerByIdAsync(request.InsurerId.Value, ct) is null)
        {
            throw new InvalidOperationException(
                $"La aseguradora {request.InsurerId} no existe en el catálogo.");
        }

        var medicalRecordNumber = string.IsNullOrWhiteSpace(request.MedicalRecordNumber)
            ? GenerateMedicalRecordNumber()
            : request.MedicalRecordNumber.Trim();

        if (await repository.GetByMedicalRecordNumberAsync(medicalRecordNumber, ct) is not null)
        {
            throw new InvalidOperationException(
                $"Ya existe un paciente con el número de historia clínica '{medicalRecordNumber}'.");
        }

        var entity = new PatientProfile
        {
            Id = Guid.NewGuid(),
            MedicalRecordNumber = medicalRecordNumber,
            FirstName = request.FirstName.Trim(),
            MiddleName = Normalize(request.MiddleName),
            LastName = request.LastName.Trim(),
            DocumentType = Normalize(request.DocumentType),
            DocumentNumber = Normalize(request.DocumentNumber),
            DateOfBirth = request.DateOfBirth,
            Gender = Normalize(request.Gender),
            Ethnicity = Normalize(request.Ethnicity),
            BloodType = Normalize(request.BloodType),
            Phone = Normalize(request.Phone),
            Email = Normalize(request.Email),
            Address = Normalize(request.Address),
            City = Normalize(request.City),
            State = Normalize(request.State),
            PostalCode = Normalize(request.PostalCode),
            EmergencyContact = Normalize(request.EmergencyContact),
            InsurerId = request.InsurerId,
            MemberId = Normalize(request.MemberId),
            SmokingStatus = Normalize(request.SmokingStatus),
            AlcoholStatus = Normalize(request.AlcoholStatus),
            ExerciseLevel = Normalize(request.ExerciseLevel),
            Disability = Normalize(request.Disability),
            HospitalizationHistory = Normalize(request.HospitalizationHistory),
            SurgeryHistory = Normalize(request.SurgeryHistory),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Activo" : request.Status.Trim(),
            Notes = Normalize(request.Notes),
            CreatedAt = DateTime.UtcNow,
        };

        var resolver = new PatientCatalogResolver(repository);
        var diagnoses = await resolver.ResolveDiagnosesAsync(request.Diagnoses, ct);
        var medications = await resolver.ResolveMedicationsAsync(request.Medications, ct);
        var allergies = await resolver.ResolveAllergiesAsync(request.Allergies, ct);

        ApplyChildren(entity, diagnoses, medications, allergies, request.VitalSigns);

        await repository.AddAsync(entity, ct);

        logger.LogInformation("Paciente creado: {Id} ({FirstName} {LastName})",
            entity.Id, entity.FirstName, entity.LastName);

        var created = await repository.GetByIdAsync(entity.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer el paciente creado.");

        return PatientDto.FromEntity(created);
    }

    private static string GenerateMedicalRecordNumber()
        => $"MRN-{Guid.NewGuid():N}"[..14].ToUpperInvariant();

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static void ApplyChildren(
        PatientProfile entity,
        IReadOnlyList<ResolvedDiagnosis> diagnoses,
        IReadOnlyList<ResolvedMedication> medications,
        IReadOnlyList<ResolvedAllergy> allergies,
        IReadOnlyList<VitalSignInput>? vitalSigns)
    {
        var now = DateTime.UtcNow;

        entity.Diagnoses = diagnoses
            .Select(d => new PatientDiagnosis
            {
                Id = Guid.NewGuid(),
                Icd10CodeId = d.Icd10CodeId,
                IsPrimary = d.IsPrimary,
                CreatedAt = now,
            }).ToList();

        entity.Medications = medications
            .Select((m, index) => new PatientMedication
            {
                Id = Guid.NewGuid(),
                MedicationId = m.MedicationId,
                Frequency = m.Frequency,
                SortOrder = index,
                CreatedAt = now,
            }).ToList();

        entity.Allergies = allergies
            .Select(a => new PatientAllergy
            {
                Id = Guid.NewGuid(),
                AllergenId = a.AllergenId,
                Notes = a.Notes,
                CreatedAt = now,
            }).ToList();

        entity.VitalSigns = (vitalSigns ?? [])
            .Select(v => new VitalSign
            {
                Id = Guid.NewGuid(),
                MeasuredAt = v.MeasuredAt ?? now,
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