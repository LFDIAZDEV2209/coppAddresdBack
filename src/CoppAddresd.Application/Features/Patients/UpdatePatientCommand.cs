using CoppAddresd.Application.Features.Patients.Events;
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
    Guid? UpdatedBy,
    IReadOnlyList<DiagnosisInput>? Diagnoses,
    IReadOnlyList<MedicationInput>? Medications,
    IReadOnlyList<AllergyInput>? Allergies,
    IReadOnlyList<VitalSignInput>? VitalSigns)
    : IRequest<PatientDto?>;

public sealed class UpdatePatientCommandHandler(
    IPatientRepository repository,
    ICatalogRepository catalogs,
    ILogger<UpdatePatientCommandHandler> logger,
    IPatientMetricsQueue? metricsQueue = null) : IRequestHandler<UpdatePatientCommand, PatientDto?>
{
    public async Task<PatientDto?> Handle(UpdatePatientCommand request, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(request.Id, ct);
        if (entity is null)
            return null;

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

        var oldStatus = entity.Status;
        var newStatus = string.IsNullOrWhiteSpace(request.Status) ? entity.Status : request.Status.Trim();

        entity.MedicalRecordNumber = string.IsNullOrWhiteSpace(request.MedicalRecordNumber)
            ? entity.MedicalRecordNumber
            : request.MedicalRecordNumber.Trim();
        entity.FirstName = request.FirstName.Trim();
        entity.MiddleName = PatientOptions.Normalize(request.MiddleName);
        entity.LastName = request.LastName.Trim();
        entity.DocumentTypeId = request.DocumentTypeId;
        entity.DocumentNumber = PatientOptions.Normalize(request.DocumentNumber);
        entity.DateOfBirth = request.DateOfBirth;
        entity.Gender = PatientOptions.Normalize(request.Gender);
        entity.EthnicityId = request.EthnicityId;
        entity.BloodTypeId = request.BloodTypeId;
        entity.PhoneCountryCode = PatientOptions.Normalize(request.PhoneCountryCode);
        entity.PhoneNumber = PatientOptions.Normalize(request.PhoneNumber);
        entity.Email = PatientOptions.Normalize(request.Email);
        entity.Address = PatientOptions.Normalize(request.Address);
        entity.CityId = request.CityId;
        entity.StateId = request.StateId;
        entity.CountryId = request.CountryId;
        entity.PostalCode = PatientOptions.Normalize(request.PostalCode);
        entity.EmergencyContact = PatientOptions.Normalize(request.EmergencyContact);
        entity.InsurerId = request.InsurerId;
        entity.MemberId = PatientOptions.Normalize(request.MemberId);
        entity.MaritalStatus = PatientOptions.Normalize(request.MaritalStatus);
        entity.SmokingStatus = PatientOptions.Normalize(request.SmokingStatus);
        entity.AlcoholStatus = PatientOptions.Normalize(request.AlcoholStatus);
        entity.ExerciseLevel = PatientOptions.Normalize(request.ExerciseLevel);
        entity.Disability = PatientOptions.Normalize(request.Disability);
        entity.HospitalizationHistory = PatientOptions.Normalize(request.HospitalizationHistory);
        entity.SurgeryHistory = PatientOptions.Normalize(request.SurgeryHistory);
        entity.Status = newStatus;
        entity.Notes = PatientOptions.Normalize(request.Notes);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = request.UpdatedBy;

        ReplaceChildren(entity, request.Diagnoses, request.Medications, request.Allergies, request.VitalSigns);

        await repository.UpdateAsync(entity, ct);

        // Pre-agregación CQRS en background (0ms overhead en HTTP)
        if (oldStatus != entity.Status && metricsQueue != null)
        {
            await metricsQueue.EnqueueAsync(new PatientStatusChangedMetricEvent(
                entity.Id,
                entity.ClinicId,
                oldStatus,
                entity.Status,
                DateTime.UtcNow
            ));
        }

        logger.LogInformation("Paciente actualizado: {Id} ({FirstName} {LastName})",
            entity.Id, entity.FirstName, entity.LastName);

        var updated = await repository.GetByIdAsync(entity.Id, ct);
        return updated is null ? null : PatientDto.FromEntity(updated);
    }

    private static void ReplaceChildren(
        PatientProfile entity,
        IReadOnlyList<DiagnosisInput>? diagnoses,
        IReadOnlyList<MedicationInput>? medications,
        IReadOnlyList<AllergyInput>? allergies,
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