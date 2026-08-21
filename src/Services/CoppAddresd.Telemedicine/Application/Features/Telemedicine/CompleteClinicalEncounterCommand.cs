using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Finaliza el registro clínico de una cita: pasa el encuentro de <c>Draft</c> a
/// <c>Completed</c> (estado final e inmutable). Acepta opcionalmente los datos
/// finales para guardar + completar en una sola operación (la UI no necesita dos
/// llamadas). Es idempotente: completar un encuentro ya <c>Completed</c> devuelve
/// el existente sin error. Exige contenido mínimo (nota o campo clínico).
/// </summary>
public sealed record CompleteClinicalEncounterCommand(
    Guid AppointmentId,
    ClinicalDataDto? ClinicalData,
    string? Notes,
    Guid UserId,
    bool HasManagePermission) : IRequest<ClinicalEncounterDto>;

public sealed class CompleteClinicalEncounterCommandValidator
    : AbstractValidator<CompleteClinicalEncounterCommand>
{
    public CompleteClinicalEncounterCommandValidator()
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

public sealed class CompleteClinicalEncounterCommandHandler(
    IAppointmentRepository appointments,
    IEncounterRepository encounters,
    ITelemedicineReferenceDataService referenceData)
    : IRequestHandler<CompleteClinicalEncounterCommand, ClinicalEncounterDto>
{
    public async Task<ClinicalEncounterDto> Handle(
        CompleteClinicalEncounterCommand request,
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
            // No hay borrador previo: completar crea y finaliza en una sola
            // operación (la documentación se hizo directa al terminar).
            if (!EncounterSupport.HasContent(request.ClinicalData, request.Notes))
            {
                throw new BusinessRuleViolationException(
                    "No se puede completar un registro clínico sin contenido (nota o campo clínico).");
            }

            encounter = new ClinicalEncounter
            {
                AppointmentId = appointment.Id,
                PatientId = appointment.PatientId,
                ProfessionalId = appointment.ProfessionalId,
                SessionId = ActiveSessionId(appointment),
                EncounterDate = now,
                Status = EncounterStatus.Completed,
                ClinicalData = serialized,
                Notes = request.Notes,
                CreatedBy = request.UserId,
                CreatedAt = now.UtcDateTime,
                UpdatedAt = now.UtcDateTime,
            };

            var created = await encounters.AddAsync(encounter, ct);
            if (created.Id != encounter.Id)
            {
                // Perdimos la carrera: el otro guardado creó el encuentro.
                encounter = created;
                if (encounter.Status != EncounterStatus.Completed)
                {
                    ApplyAndComplete(encounter, serialized, request.Notes, now, request.ClinicalData);
                    await encounters.UpdateAsync(encounter, ct);
                }
            }

            return ToDto(encounter);
        }

        if (encounter.Status == EncounterStatus.Completed)
        {
            // Idempotente: completar un registro ya finalizado devuelve el estado
            // actual sin error (p. ej. reintento de la petición).
            return ToDto(encounter);
        }

        ApplyAndComplete(encounter, serialized, request.Notes, now, request.ClinicalData);
        await encounters.UpdateAsync(encounter, ct);

        return ToDto(encounter);
    }

    private static void ApplyAndComplete(
        ClinicalEncounter encounter,
        string? serialized,
        string? notes,
        DateTimeOffset now,
        ClinicalDataDto? data)
    {
        if (!EncounterSupport.HasContent(data, notes))
        {
            throw new BusinessRuleViolationException(
                "No se puede completar un registro clínico sin contenido (nota o campo clínico).");
        }

        encounter.ClinicalData = serialized;
        encounter.Notes = notes;
        encounter.Status = EncounterStatus.Completed;
        encounter.UpdatedAt = now.UtcDateTime;
    }

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
