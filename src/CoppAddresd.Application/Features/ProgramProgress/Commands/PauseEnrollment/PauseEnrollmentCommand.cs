using CoppAddresd.Application.DTOs.ProgramProgress;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.PauseEnrollment;

/// <summary>
/// Comando para pausar una inscripción activa (Active → Paused, SPEC §5.1 y
/// §7.5). El <c>Reason</c> es solo informativo: la tabla de inscripciones no
/// tiene columna de motivo en MVP, se conserva en el log.
/// </summary>
public sealed record PauseEnrollmentCommand(
    Guid EnrollmentId,
    string? Reason = null,
    Guid? ActorId = null) : IRequest<ProgramEnrollmentDto>;

/// <summary>Validación de input de <see cref="PauseEnrollmentCommand"/> (T-11).</summary>
public sealed class PauseEnrollmentCommandValidator : AbstractValidator<PauseEnrollmentCommand>
{
    public PauseEnrollmentCommandValidator()
    {
        RuleFor(x => x.EnrollmentId)
            .NotEmpty()
            .WithMessage("El enrollmentId es requerido.");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .WithMessage("El motivo no puede superar 500 caracteres.");
    }
}