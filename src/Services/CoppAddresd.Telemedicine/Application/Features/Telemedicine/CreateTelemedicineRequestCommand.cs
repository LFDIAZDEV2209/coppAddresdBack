using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// El paciente solicita una cita de telemedicina. La solicitud nace
/// <c>Pending</c>; su confirmación (por el profesional) deriva en una cita.
/// El paciente puede elegir especialidad (obligatoria) y, opcionalmente, un
/// profesional preferido y una fecha/hora preferida.
/// </summary>
public sealed record CreateTelemedicineRequestCommand(
    Guid PatientId,
    Guid OrganizationId,
    Guid SpecialtyId,
    Guid? ProfessionalId,
    Guid? ClinicId,
    Guid? LocationId,
    DateTimeOffset? PreferredStart,
    string Reason,
    Guid CreatedBy) : IRequest<TelemedicineRequestDto>;

public sealed class CreateTelemedicineRequestCommandValidator
    : AbstractValidator<CreateTelemedicineRequestCommand>
{
    public CreateTelemedicineRequestCommandValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.SpecialtyId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("El motivo de la consulta es obligatorio.")
            .MaximumLength(2000);
        RuleFor(x => x.CreatedBy).NotEmpty();
    }
}

public sealed class CreateTelemedicineRequestCommandHandler(
    IRequestRepository requests,
    ITelemedicineReferenceDataService referenceData,
    ITelemedicineSettingsProvider settingsProvider)
    : IRequestHandler<CreateTelemedicineRequestCommand, TelemedicineRequestDto>
{
    public async Task<TelemedicineRequestDto> Handle(
        CreateTelemedicineRequestCommand request,
        CancellationToken ct)
    {
        var patient = await ReferenceDataGuard.RequirePatientAsync(referenceData, request.PatientId, ct);
        var specialty = await ReferenceDataGuard.RequireSpecialtyAsync(referenceData, request.SpecialtyId, ct);

        ProfessionalRefDto? professional = null;
        if (request.ProfessionalId is { } professionalId)
        {
            professional = await ReferenceDataGuard.RequireProfessionalAsync(referenceData, professionalId, ct);

            // Si el paciente eligió un profesional, la especialidad debe estar
            // entre las que atiende (catálogo de especialidades del profesional).
            if (professional.SpecialtyIds.Count > 0
                && !professional.SpecialtyIds.Contains(request.SpecialtyId))
            {
                throw new BusinessRuleViolationException(
                    "El profesional seleccionado no atiende la especialidad solicitada.");
            }
        }

        if (request.PreferredStart is { } preferred)
        {
            var settings = await settingsProvider.GetSettingsAsync(
                request.OrganizationId, request.ClinicId, ct);
            var now = DateTimeOffset.UtcNow;

            // La preferencia es una ventana (no un slot reservado): se valida
            // dentro de la ventana permitida, sin exigir duración.
            if (preferred < now.AddHours(settings.MinAdvanceBookingHours))
            {
                throw new BusinessRuleViolationException(
                    $"La fecha preferida debe tener al menos {settings.MinAdvanceBookingHours} h de anticipación.");
            }

            if (preferred > now.AddDays(settings.MaxAdvanceBookingDays))
            {
                throw new BusinessRuleViolationException(
                    $"La fecha preferida no puede superar los {settings.MaxAdvanceBookingDays} días.");
            }
        }

        var entity = new TelemedicineRequest
        {
            PatientId = request.PatientId,
            OrganizationId = request.OrganizationId,
            SpecialtyId = request.SpecialtyId,
            ProfessionalId = request.ProfessionalId,
            ClinicId = request.ClinicId,
            LocationId = request.LocationId,
            // Normaliza a UTC (Npgsql exige offset 0 para timestamptz).
            PreferredStart = request.PreferredStart?.ToUniversalTime(),
            Reason = request.Reason,
            Status = AppointmentRequestStatus.Pending,
            CreatedBy = request.CreatedBy,
        };

        await requests.AddAsync(entity, ct);

        return new TelemedicineRequestDto(
            entity.Id,
            entity.PatientId,
            patient.FullName,
            entity.ProfessionalId,
            entity.SpecialtyId,
            specialty.Name,
            entity.OrganizationId,
            entity.ClinicId,
            entity.LocationId,
            entity.PreferredStart,
            entity.Reason,
            entity.Status,
            entity.CreatedAt);
    }
}
