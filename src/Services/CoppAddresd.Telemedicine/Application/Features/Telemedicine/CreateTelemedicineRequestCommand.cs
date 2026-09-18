using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// El paciente solicita una cita de telemedicina. La solicitud nace
/// <c>Pending</c>; su confirmación (por el profesional) deriva en una cita.
/// El paciente puede elegir especialidad (obligatoria) y, opcionalmente, un
/// profesional preferido y una fecha/hora preferida.
/// </summary>
/// <remarks>
/// Alcance dual resuelto en el handler: <c>ErpMode=true</c> (usuario con
/// permiso <c>Appointments.RequestsCreate</c>) acepta cualquier
/// <c>PatientId</c> del cuerpo; <c>ErpMode=false</c> (paciente de la app
/// móvil) exige que el paciente del JWT (<c>CreatedBy</c>) sea el
/// <c>PatientId</c> de la solicitud (403 en caso contrario).
/// </remarks>
public sealed record CreateTelemedicineRequestCommand(
    Guid PatientId,
    Guid OrganizationId,
    Guid SpecialtyId,
    Guid? ProfessionalId,
    Guid? ClinicId,
    Guid? LocationId,
    DateTimeOffset? PreferredStart,
    string Reason,
    Guid CreatedBy,
    bool ErpMode
) : IRequest<TelemedicineRequestDto>;

public sealed class CreateTelemedicineRequestCommandValidator
    : AbstractValidator<CreateTelemedicineRequestCommand>
{
    public CreateTelemedicineRequestCommandValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.SpecialtyId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de la consulta es obligatorio.")
            .MaximumLength(2000);
        RuleFor(x => x.CreatedBy).NotEmpty();
    }
}

public sealed class CreateTelemedicineRequestCommandHandler(
    IRequestRepository requests,
    IAppointmentReferenceDataService referenceData,
    ITelemedicineSettingsProvider settingsProvider,
    IAlertRepository alerts,
    ITelemedicineNotifier? notifier = null,
    ILogger<CreateTelemedicineRequestCommandHandler>? logger = null
) : IRequestHandler<CreateTelemedicineRequestCommand, TelemedicineRequestDto>
{
    public async Task<TelemedicineRequestDto> Handle(
        CreateTelemedicineRequestCommand request,
        CancellationToken ct
    )
    {
        // Alcance dual: el ERP (con permiso) opera para cualquier paciente;
        // el paciente de la app móvil solo para sí mismo (identidad del JWT).
        if (!request.ErpMode)
        {
            var actingPatient = await referenceData.GetPatientByUserIdAsync(request.CreatedBy, ct);
            if (actingPatient is null)
            {
                throw new ForbiddenException(
                    "Solo los pacientes pueden solicitar citas desde la app móvil."
                );
            }

            if (actingPatient.Id != request.PatientId)
            {
                throw new ForbiddenException("Solo puedes solicitar citas para tu propio perfil.");
            }
        }

        var patient = await ReferenceDataGuard.RequirePatientAsync(
            referenceData,
            request.PatientId,
            ct
        );
        var specialty = await ReferenceDataGuard.RequireSpecialtyAsync(
            referenceData,
            request.SpecialtyId,
            ct
        );

        ProfessionalRefDto? professional = null;
        if (request.ProfessionalId is { } professionalId)
        {
            professional = await ReferenceDataGuard.RequireProfessionalAsync(
                referenceData,
                professionalId,
                ct
            );

            // Si el paciente eligió un profesional, la especialidad debe estar
            // entre las que atiende (catálogo de especialidades del profesional).
            if (
                professional.SpecialtyIds.Count > 0
                && !professional.SpecialtyIds.Contains(request.SpecialtyId)
            )
            {
                throw new BusinessRuleViolationException(
                    "El profesional seleccionado no atiende la especialidad solicitada."
                );
            }
        }

        if (request.PreferredStart is { } preferred)
        {
            var settings = await settingsProvider.GetSettingsAsync(
                request.OrganizationId,
                request.ClinicId,
                ct
            );
            var now = DateTimeOffset.UtcNow;

            // La preferencia es una ventana (no un slot reservado): se valida
            // dentro de la ventana permitida, sin exigir duración.
            if (preferred < now.AddHours(settings.MinAdvanceBookingHours))
            {
                throw new BusinessRuleViolationException(
                    $"La fecha preferida debe tener al menos {settings.MinAdvanceBookingHours} h de anticipación."
                );
            }

            if (preferred > now.AddDays(settings.MaxAdvanceBookingDays))
            {
                throw new BusinessRuleViolationException(
                    $"La fecha preferida no puede superar los {settings.MaxAdvanceBookingDays} días."
                );
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

        // Bandeja: nueva solicitud → al profesional que la confirmará (si el
        // paciente eligió uno; si no, la asigna el staff/agenda).
        if (entity.ProfessionalId is { } targetProfessionalId)
        {
            var targetProfessional = await referenceData.GetProfessionalAsync(
                targetProfessionalId,
                ct
            );
            if (
                AlertMaterializer.NewRequest(
                    targetProfessional?.UserId,
                    entity.Id,
                    entity.SpecialtyId,
                    patient.FullName,
                    specialty.Name
                ) is
                { } alert
            )
            {
                await alerts.AddRangeAsync([alert], ct);
            }

            // F2: push al profesional elegido (best-effort: no rompe la creación).
            if (targetProfessional?.UserId is { } professionalUserId)
            {
                await NotificationSupport.TrySendAsync(
                    notifier,
                    logger,
                    new TelemedicineNotification(
                        professionalUserId,
                        $"Nueva solicitud de {patient.FullName}",
                        $"{patient.FullName} solicitó una cita de {specialty.Name}.",
                        [TelemedicineNotificationChannel.Push],
                        NotificationSupport.Data(requestId: entity.Id, screen: "requests"),
                        $"request:{entity.Id:N}:new"
                    ),
                    ct
                );
            }
        }

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
            entity.CreatedAt,
            entity.RejectionReason
        );
    }
}
