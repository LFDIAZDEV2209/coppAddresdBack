using CoppAddresd.Application.DTOs.ProgramProgress;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.DecideAdaptation;

/// <summary>
/// Comando para decidir una recomendación de adaptación (SPEC §7.7):
/// <c>Approve</c> (Pending → Approved), <c>Reject</c> (Pending → Rejected) o
/// <c>Apply</c> (Approved → Applied, usado por el motor en P2). Al pasar a
/// <c>Applied</c> el handler registra la fila semántica
/// <c>AdaptationApplied</c> en <c>audit.activity_logs</c> (AC-17).
/// </summary>
public sealed record DecideAdaptationCommand(
    Guid AdaptationId,
    AdaptationDecisionAction Decision,
    string? Note = null,
    Guid? ActorId = null) : IRequest<AdaptationRecommendationDto>;

/// <summary>Validación de input de <see cref="DecideAdaptationCommand"/> (T-11).</summary>
public sealed class DecideAdaptationCommandValidator : AbstractValidator<DecideAdaptationCommand>
{
    public DecideAdaptationCommandValidator()
    {
        RuleFor(x => x.AdaptationId)
            .NotEmpty()
            .WithMessage("El id de la recomendación es requerido.");

        RuleFor(x => x.Decision)
            .IsInEnum()
            .WithMessage("Decisión inválida (Approve | Reject | Apply).");

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .WithMessage("La nota no puede superar 500 caracteres.");
    }
}