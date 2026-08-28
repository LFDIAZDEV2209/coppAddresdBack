using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.EnrollPatient;

/// <summary>
/// Comando de inscripción de un paciente al programa (SPEC §7.5, permiso
/// <c>Program.Enroll</c> en la capa API). <c>TemplateId</c> es opcional: si no
/// se envía, el handler resuelve la plantilla por defecto
/// (<c>Program:DefaultTemplate:Code</c>, fallback <c>default-83w</c>) vía
/// <paramref name="DefaultTemplateCode"/> que la capa API lee de configuración.
/// <c>StartLocalDate</c> opcional: si no se envía, arranca el lunes de la
/// semana local actual del paciente.
/// </summary>
public sealed record EnrollPatientCommand(
    Guid PatientId,
    Guid? TemplateId,
    string Timezone,
    DateOnly? StartLocalDate,
    string? DefaultTemplateCode = null,
    Guid? ActorId = null) : IRequest<ProgramEnrollmentDto>;

/// <summary>
/// Validación de input de <see cref="EnrollPatientCommand"/> (T-11): solo la
/// forma del payload (zona IANA válida, GUIDs). La fecha de inicio NO lleva
/// guardia contra fechas futuras en UTC por la misma razón que en
/// <see cref="CoppAddresd.Application.Features.ProgramProgress.Commands.CompleteTask.CompleteTaskCommandValidator"/>:
/// un
/// <c>startLocalDate</c> del "hoy" local de un paciente UTC+ es mañana en UTC,
/// y además una inscripción con inicio futuro (próxima semana) es legítima.
/// El default (lunes de la semana local) y la matemática de fechas los
/// resuelve el handler con la zona IANA del paciente (SPEC §6.11).
/// </summary>
public sealed class EnrollPatientCommandValidator : AbstractValidator<EnrollPatientCommand>
{
    public EnrollPatientCommandValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty()
            .WithMessage("El patientId es requerido.");

        RuleFor(x => x.Timezone)
            .NotEmpty()
            .WithMessage("La zona horaria es requerida.")
            .Must(ProgramProgressTime.IsValidIanaTimezone)
            .WithMessage("La zona horaria debe ser un identificador IANA válido (ej: America/Bogota).");

        RuleFor(x => x.TemplateId)
            .NotEmpty()
            .WithMessage("El templateId debe ser un GUID válido.")
            .When(x => x.TemplateId.HasValue);

        RuleFor(x => x.StartLocalDate)
            .NotEmpty()
            .When(x => x.StartLocalDate.HasValue)
            .WithMessage("startLocalDate debe ser una fecha válida.");
    }
}