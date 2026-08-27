using CoppAddresd.Application.Features.ProgramProgress.DTOs.Interventions;
using CoppAddresd.Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.MarkTeleComply;

/// <summary>
/// Hook de telemedicina: cumplimiento evaluado (SPEC §22, D — AC-49):
/// otorga <c>TELE_COMPLY</c> (+50) y puede avanzar hacia
/// <c>completed</c>.
/// </summary>
public sealed record MarkTeleComplyCommand(Guid InterventionId) : IRequest<InterventionDto>;

/// <summary>Validación de forma del payload.</summary>
public sealed class MarkTeleComplyCommandValidator : AbstractValidator<MarkTeleComplyCommand>
{
    public MarkTeleComplyCommandValidator()
    {
        RuleFor(x => x.InterventionId)
            .NotEmpty()
            .WithMessage("El interventionId es requerido.");
    }
}

/// <summary>Orquesta el hook vía el repositorio y loguea sin PHI.</summary>
public sealed class MarkTeleComplyCommandHandler(
    IProgramRepository repository,
    ILogger<MarkTeleComplyCommandHandler> logger)
    : IRequestHandler<MarkTeleComplyCommand, InterventionDto>
{
    public async Task<InterventionDto> Handle(MarkTeleComplyCommand request, CancellationToken ct)
    {
        var intervention = await repository.MarkTeleComplyAsync(request.InterventionId, ct);

        logger.LogInformation(
            "Program.TeleComply: intervention={InterventionId} xpAwarded={XpAwardedTotal}",
            intervention.Id, intervention.XpAwardedTotal);

        return intervention;
    }
}
