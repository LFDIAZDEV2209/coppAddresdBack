using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Actualiza un paciente (agregado completo). Las colecciones hijas se
/// reemplazan por completo (reemplazo de agregado) en una sola transacción.
/// </summary>
public record UpdatePatientCommand(
    Guid Id,
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
    : IRequest<PatientDto?>;

public sealed class UpdatePatientCommandHandler(
    IPatientRepository repository,
    ILogger<UpdatePatientCommandHandler> logger) : IRequestHandler<UpdatePatientCommand, PatientDto?>
{
    public async Task<PatientDto?> Handle(UpdatePatientCommand request, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(request.Id, ct);
        if (entity is null)
            return null;

        if (request.InsurerId is not null &&
            await repository.GetInsurerByIdAsync(request.InsurerId.Value, ct) is null)
        {
            throw new InvalidOperationException(
                $"La aseguradora {request.InsurerId} no existe en el catálogo.");
        }

        entity.MedicalRecordNumber = string.IsNullOrWhiteSpace(request.MedicalRecordNumber)
            ? entity.MedicalRecordNumber
            : request.MedicalRecordNumber.Trim();
        entity.FirstName = request.FirstName.Trim();
        entity.MiddleName = Normalize(request.MiddleName);
        entity.LastName = request.LastName.Trim();
        entity.DocumentType = Normalize(request.DocumentType);
        entity.DocumentNumber = Normalize(request.DocumentNumber);
        entity.DateOfBirth = request.DateOfBirth;
        entity.Gender = Normalize(request.Gender);
        entity.Ethnicity = Normalize(request.Ethnicity);
        entity.BloodType = Normalize(request.BloodType);
        entity.Phone = Normalize(request.Phone);
        entity.Email = Normalize(request.Email);
        entity.Address = Normalize(request.Address);
        entity.City = Normalize(request.City);
        entity.State = Normalize(request.State);
        entity.PostalCode = Normalize(request.PostalCode);
        entity.EmergencyContact = Normalize(request.EmergencyContact);
        entity.InsurerId = request.InsurerId;
        entity.MemberId = Normalize(request.MemberId);
        entity.SmokingStatus = Normalize(request.SmokingStatus);
        entity.AlcoholStatus = Normalize(request.AlcoholStatus);
        entity.ExerciseLevel = Normalize(request.ExerciseLevel);
        entity.Disability = Normalize(request.Disability);
        entity.HospitalizationHistory = Normalize(request.HospitalizationHistory);
        entity.SurgeryHistory = Normalize(request.SurgeryHistory);
        entity.Status = string.IsNullOrWhiteSpace(request.Status) ? entity.Status : request.Status.Trim();
        entity.Notes = Normalize(request.Notes);
        entity.UpdatedAt = DateTime.UtcNow;

        var resolver = new PatientCatalogResolver(repository);
        var diagnoses = await resolver.ResolveDiagnosesAsync(request.Diagnoses, ct);
        var medications = await resolver.ResolveMedicationsAsync(request.Medications, ct);
        var allergies = await resolver.ResolveAllergiesAsync(request.Allergies, ct);

        ReplaceChildren(entity, diagnoses, medications, allergies, request.VitalSigns);

        await repository.UpdateAsync(entity, ct);

        logger.LogInformation("Paciente actualizado: {Id} ({FirstName} {LastName})",
            entity.Id, entity.FirstName, entity.LastName);

        var updated = await repository.GetByIdAsync(entity.Id, ct);
        return updated is null ? null : PatientDto.FromEntity(updated);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ReplaceChildren(
        PatientProfile entity,
        IReadOnlyList<ResolvedDiagnosis> diagnoses,
        IReadOnlyList<ResolvedMedication> medications,
        IReadOnlyList<ResolvedAllergy> allergies,
        IReadOnlyList<VitalSignInput>? vitalSigns)
    {
        // Las colecciones son reemplazadas por completo: EF borra los huérfanos
        // por el comportamiento Cascade configurado en las relaciones.
        entity.Diagnoses.Clear();
        entity.Medications.Clear();
        entity.Allergies.Clear();
        entity.VitalSigns.Clear();

        CreatePatientCommandHandler.ApplyChildren(entity, diagnoses, medications, allergies, vitalSigns);
    }
}