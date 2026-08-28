using CoppAddresd.Application.Features.ProgramProgress.DTOs.ClinicalXp;
using CoppAddresd.Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.DecideClinicalReview;

/// <summary>
/// Decisión de una revisión clínica de XP (SPEC §15, D): aprueba o rechaza una
/// mejoría significativa detectada por el motor. Aprobada → se otorga
/// <c>CLINICAL_SIGNIFICANT</c> (100 XP por defecto) con
/// <c>validated_by</c>/<c>validated_at</c> del clínico (cuenta en los totales);
/// rechazada → sin XP. Ya decidida → 409 <c>REVIEW_ALREADY_DECIDED</c>. La API
/// lo protege con <c>Program.Adapt</c> y el rol clínico lo exige el repositorio
/// (misma guardia AC-22 que la línea base: un paciente decidir su propia XP →
/// 403).
/// </summary>
public sealed record DecideClinicalReviewCommand(
    Guid ReviewId,
    bool Approve,
    Guid? ActorId,
    IReadOnlyList<string> CallerRoles) : IRequest<ClinicalReviewDto>;

/// <summary>
/// Validación de forma del payload (D): el <c>reviewId</c> es requerido. La
/// regla de negocio (rol clínico, estado de la revisión) vive en el
/// repositorio, como en la línea base clínica (AC-22).
/// </summary>
public sealed class DecideClinicalReviewCommandValidator : AbstractValidator<DecideClinicalReviewCommand>
{
    public DecideClinicalReviewCommandValidator()
    {
        RuleFor(x => x.ReviewId)
            .NotEmpty()
            .WithMessage("El reviewId es requerido.");
    }
}

/// <summary>
/// Orquesta la decisión vía el repositorio (una transacción: estado de la
/// revisión + otorgamiento de XP si se aprueba) y loguea sin PHI.
/// </summary>
public sealed class DecideClinicalReviewCommandHandler(
    IProgramRepository repository,
    ILogger<DecideClinicalReviewCommandHandler> logger)
    : IRequestHandler<DecideClinicalReviewCommand, ClinicalReviewDto>
{
    public async Task<ClinicalReviewDto> Handle(DecideClinicalReviewCommand request, CancellationToken ct)
    {
        var review = await repository.DecideClinicalXpReviewAsync(
            request.ReviewId, request.Approve, request.ActorId, request.CallerRoles, ct);

        logger.LogInformation(
            "Program.DecideClinicalReview: review={ReviewId} patient={PatientId} " +
            "metric={MetricCode} decision={Decision} actor={ActorId}",
            review.Id, review.PatientId, review.MetricCode,
            request.Approve ? "approve" : "reject", request.ActorId);

        return review;
    }
}