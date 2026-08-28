using CoppAddresd.Application.DTOs.ProgramProgress;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.WithdrawEnrollment;

/// <summary>
/// Comando para retirar una inscripción (terminal, conserva historial, SPEC
/// §5.1 y §7.5). El <c>Reason</c> es solo informativo: la tabla de
/// inscripciones no tiene columna de motivo en MVP, se conserva en el log.
/// </summary>
public sealed record WithdrawEnrollmentCommand(
    Guid EnrollmentId,
    string? Reason = null,
    Guid? ActorId = null) : IRequest<ProgramEnrollmentDto>;

/// <summary>Validación de input de <see cref="WithdrawEnrollmentCommand"/> (T-11).</summary>
public sealed class WithdrawEnrollmentCommandValidator : AbstractValidator<WithdrawEnrollmentCommand>
{
    public WithdrawEnrollmentCommandValidator()
    {
        RuleFor(x => x.EnrollmentId)
            .NotEmpty()
            .WithMessage("El enrollmentId es requerido.");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .WithMessage("El motivo no puede superar 500 caracteres.");
    }
}