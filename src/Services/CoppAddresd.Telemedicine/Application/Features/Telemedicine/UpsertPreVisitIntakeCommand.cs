using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Autoguardado (upsert) de la pre-consulta del paciente (F4). Solo el paciente
/// de la cita escribe (el profesional/supervisor leen); el autor y el paciente
/// se derivan del JWT y de la cita, nunca del cuerpo. Solo se permite mientras
/// la cita está <c>Confirmed</c>; después → 409 (solo lectura).
/// </summary>
public sealed record UpsertPreVisitIntakeCommand(
    Guid AppointmentId,
    string Reason,
    string? Symptoms,
    string? Allergies,
    string? Medications,
    Guid UserId,
    bool HasManagePermission) : IRequest<PreVisitIntakeDto>;

public sealed class UpsertPreVisitIntakeCommandValidator
    : AbstractValidator<UpsertPreVisitIntakeCommand>
{
    /// <summary>Longitud máxima del motivo (coincide con la columna).</summary>
    public const int MaxReasonLength = 500;

    /// <summary>Longitud máxima de los síntomas reportados.</summary>
    public const int MaxSymptomsLength = 4000;

    /// <summary>Longitud máxima de las alergias declaradas.</summary>
    public const int MaxAllergiesLength = 2000;

    /// <summary>Longitud máxima de la medicación actual.</summary>
    public const int MaxMedicationsLength = 2000;

    public UpsertPreVisitIntakeCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.Reason)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("El motivo de la consulta es obligatorio.")
            .Must(reason => reason.Trim().Length > 0)
            .WithMessage("El motivo de la consulta es obligatorio.")
            .Must(reason => reason.Trim().Length <= MaxReasonLength)
            .WithMessage($"El motivo de la consulta no puede superar los {MaxReasonLength} caracteres.");

        RuleFor(x => x.Symptoms)
            .Must(value => value is null || value.Trim().Length <= MaxSymptomsLength)
            .WithMessage($"Los síntomas no pueden superar los {MaxSymptomsLength} caracteres.");

        RuleFor(x => x.Allergies)
            .Must(value => value is null || value.Trim().Length <= MaxAllergiesLength)
            .WithMessage($"Las alergias no pueden superar los {MaxAllergiesLength} caracteres.");

        RuleFor(x => x.Medications)
            .Must(value => value is null || value.Trim().Length <= MaxMedicationsLength)
            .WithMessage($"La medicación no puede superar los {MaxMedicationsLength} caracteres.");
    }
}

public sealed class UpsertPreVisitIntakeCommandHandler(
    IAppointmentRepository appointments,
    IPreVisitIntakeRepository intakes,
    IAppointmentReferenceDataService referenceData,
    ILogger<UpsertPreVisitIntakeCommandHandler> logger)
    : IRequestHandler<UpsertPreVisitIntakeCommand, PreVisitIntakeDto>
{
    public async Task<PreVisitIntakeDto> Handle(
        UpsertPreVisitIntakeCommand request,
        CancellationToken ct)
    {
        var appointment = await appointments.GetByIdAsync(request.AppointmentId, ct)
            ?? throw new NotFoundException("Cita", request.AppointmentId);

        var role = await SessionSupport.RequireParticipantAsync(
            referenceData, appointment, request.UserId, request.HasManagePermission, ct);

        // El dato es subjetivo del paciente: el profesional y el supervisor
        // solo leen (403 al intentar escribir).
        if (role is not SessionParticipant.Patient)
        {
            throw new ForbiddenException(
                "Solo el paciente de la cita puede guardar su pre-consulta.");
        }

        SessionSupport.EnsureIntakeEditable(appointment.Status);

        var now = DateTimeOffset.UtcNow;
        var existing = await intakes.GetForUpdateByAppointmentIdAsync(appointment.Id, ct);

        if (existing is null)
        {
            var intake = new PreVisitIntake
            {
                AppointmentId = appointment.Id,
                PatientId = appointment.PatientId,
                Reason = request.Reason.Trim(),
                Symptoms = Normalize(request.Symptoms),
                Allergies = Normalize(request.Allergies),
                Medications = Normalize(request.Medications),
                CreatedBy = request.UserId,
                CreatedAt = now.UtcDateTime,
            };

            var created = await intakes.AddAsync(intake, ct);
            if (created.Id != intake.Id)
            {
                // Autoguardado concurrente: otro guardado creó la fila primero.
                // AddAsync devolvió la existente TRACKEADA → aplicar cambios.
                Apply(created, request, now);
                await intakes.UpdateAsync(created, ct);
                intake = created;
            }

            LogSaved(logger, intake, created: true);
            return PreVisitIntakeDto.FromEntity(intake);
        }

        Apply(existing, request, now);
        await intakes.UpdateAsync(existing, ct);

        LogSaved(logger, existing, created: false);
        return PreVisitIntakeDto.FromEntity(existing);
    }

    private static void Apply(
        PreVisitIntake intake,
        UpsertPreVisitIntakeCommand request,
        DateTimeOffset now)
    {
        intake.Reason = request.Reason.Trim();
        intake.Symptoms = Normalize(request.Symptoms);
        intake.Allergies = Normalize(request.Allergies);
        intake.Medications = Normalize(request.Medications);
        intake.UpdatedAt = now.UtcDateTime;
    }

    /// <summary>Texto opcional: trim y vacío → null (evita "" persistido).</summary>
    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Log sin PHI: solo ids y longitudes, nunca el contenido clínico.</summary>
    private static void LogSaved(
        ILogger<UpsertPreVisitIntakeCommandHandler> logger,
        PreVisitIntake intake,
        bool created)
        => logger.LogInformation(
            "Pre-consulta {IntakeId} {Action} para la cita {AppointmentId} (motivo {ReasonLength}, síntomas {SymptomsLength}, alergias {AllergiesLength}, medicación {MedicationsLength} caracteres).",
            intake.Id,
            created ? "creada" : "actualizada",
            intake.AppointmentId,
            intake.Reason.Length,
            intake.Symptoms?.Length ?? 0,
            intake.Allergies?.Length ?? 0,
            intake.Medications?.Length ?? 0);
}
