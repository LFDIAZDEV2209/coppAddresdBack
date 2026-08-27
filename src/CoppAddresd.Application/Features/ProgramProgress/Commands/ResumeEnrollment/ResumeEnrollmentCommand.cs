using CoppAddresd.Application.DTOs.ProgramProgress;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.ResumeEnrollment;

/// <summary>
/// Comando para reanudar una inscripción pausada (Paused → Active, SPEC §5.1
/// y §7.5).
/// </summary>
public sealed record ResumeEnrollmentCommand(
    Guid EnrollmentId,
    Guid? ActorId = null) : IRequest<ProgramEnrollmentDto>;

/// <summary>Validación de input de <see cref="ResumeEnrollmentCommand"/> (T-11).</summary>
public sealed class ResumeEnrollmentCommandValidator : AbstractValidator<ResumeEnrollmentCommand>
{
    public ResumeEnrollmentCommandValidator()
    {
        RuleFor(x => x.EnrollmentId)
            .NotEmpty()
            .WithMessage("El enrollmentId es requerido.");
    }
}