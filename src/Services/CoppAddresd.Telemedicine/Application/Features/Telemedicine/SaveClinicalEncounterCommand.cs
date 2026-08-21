using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Guarda (borrador o actualización) del encuentro clínico de una cita. La
/// creación es perezosa e idempotente: el primer guardado crea el encuentro
/// <c>Draft</c> y los siguientes lo actualizan. Solo el profesional de la cita o
/// un supervisor pueden documentar; la cita debe estar en curso o completada, y
/// un registro clínico <c>Completed</c> es inmutable.
/// </summary>
public sealed record SaveClinicalEncounterCommand(
    Guid AppointmentId,
    ClinicalDataDto? ClinicalData,
    string? Notes,
    Guid UserId,
    bool HasManagePermission) : IRequest<ClinicalEncounterDto>;

public sealed class SaveClinicalEncounterCommandValidator
    : AbstractValidator<SaveClinicalEncounterCommand>
{
    public SaveClinicalEncounterCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(4000);

        When(x => x.ClinicalData is not null, () =>
        {
            RuleFor(x => x.ClinicalData!.MotivoConsulta).MaximumLength(4000);
            RuleFor(x => x.ClinicalData!.Evaluacion).MaximumLength(4000);
            RuleFor(x => x.ClinicalData!.Diagnostico).MaximumLength(4000);
            RuleFor(x => x.ClinicalData!.Plan).MaximumLength(4000);
            RuleFor(x => x.ClinicalData!.Indicaciones).MaximumLength(4000);
            RuleFor(x => x.ClinicalData!.Observaciones).MaximumLength(4000);
            RuleFor(x => x.ClinicalData!.Seguimiento).MaximumLength(4000);
        });
    }
}

public sealed class SaveClinicalEncounterCommandHandler(
    IAppointmentRepository appointments,
    IEncounterRepository encounters,
    ITelemedicineReferenceDataService referenceData)
    : IRequestHandler<SaveClinicalEncounterCommand, ClinicalEncounterDto>
{
    public async Task<ClinicalEncounterDto> Handle(
        SaveClinicalEncounterCommand request,
        CancellationToken ct)
    {
        var appointment = await appointments.GetForUpdateAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        await SessionSupport.RequireSessionOwnerAsync(
            referenceData, appointment, request.UserId, request.HasManagePermission, ct);

        EncounterSupport.EnsureCanDocument(appointment.Status);

        var now = DateTimeOffset.UtcNow;
        var serialized = EncounterSupport.Serialize(request.ClinicalData);

        var encounter = await encounters.GetForUpdateByAppointmentIdAsync(appointment.Id, ct);

        if (encounter is null)
        {
            encounter = new ClinicalEncounter
            {
                AppointmentId = appointment.Id,
                PatientId = appointment.PatientId,
                ProfessionalId = appointment.ProfessionalId,
                SessionId = ActiveSessionId(appointment),
                EncounterDate = now,
                Status = EncounterStatus.Draft,
                ClinicalData = serialized,
                Notes = request.Notes,
                CreatedBy = request.UserId,
                CreatedAt = now.UtcDateTime,
            };

            var created = await encounters.AddAsync(encounter, ct);
            if (created.Id != encounter.Id)
            {
                // Perdimos la carrera de creación (otro guardado concurrente):
                // AddAsync devolvió el existente TRACKEADO → aplicar cambios.
                EncounterSupport.EnsureEditable(created.Status);
                created.ClinicalData = serialized;
                created.Notes = request.Notes;
                created.UpdatedAt = now.UtcDateTime;
                await encounters.UpdateAsync(created, ct);
                encounter = created;
            }

            return ToDto(encounter);
        }

        EncounterSupport.EnsureEditable(encounter.Status);
        encounter.ClinicalData = serialized;
        encounter.Notes = request.Notes;
        encounter.UpdatedAt = now.UtcDateTime;
        await encounters.UpdateAsync(encounter, ct);

        return ToDto(encounter);
    }

    /// <summary>Víncula el encuentro a la sesión activa de la cita (si existe).</summary>
    private static Guid? ActiveSessionId(TelemedicineAppointment appointment)
        => appointment.Sessions
            .Where(s => s.Status == TelemedicineSessionStatus.Active)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefault();

    private static ClinicalEncounterDto ToDto(ClinicalEncounter encounter)
        => new(
            encounter.Id,
            encounter.AppointmentId,
            encounter.SessionId,
            encounter.PatientId,
            encounter.ProfessionalId,
            encounter.EncounterDate,
            encounter.Status,
            EncounterSupport.Deserialize(encounter.ClinicalData),
            encounter.Notes,
            encounter.CreatedAt,
            encounter.UpdatedAt);
}
