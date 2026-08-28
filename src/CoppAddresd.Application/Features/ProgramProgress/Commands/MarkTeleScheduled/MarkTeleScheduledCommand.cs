using CoppAddresd.Application.Features.ProgramProgress.DTOs.Interventions;
using CoppAddresd.Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.MarkTeleScheduled;

/// <summary>
/// Hook de telemedicina: teleconsulta agendada (SPEC §22, D — AC-49):
/// otorga <c>TELE_SCHEDULE</c> (+50) y fija una nota. No cambia el estado.
/// Callable por el servicio de telemedicina o por un clínico con Program.Adapt.
/// </summary>
public sealed record MarkTeleScheduledCommand(Guid InterventionId) : IRequest<InterventionDto>;

/// <summary>Validación de forma del payload.</summary>
public sealed class MarkTeleScheduledCommandValidator : AbstractValidator<MarkTeleScheduledCommand>
{
    public MarkTeleScheduledCommandValidator()
    {
        RuleFor(x => x.InterventionId)
            .NotEmpty()
            .WithMessage("El interventionId es requerido.");
    }
}

/// <summary>Orquesta el hook vía el repositorio y loguea sin PHI.</summary>
public sealed class MarkTeleScheduledCommandHandler(
    IProgramRepository repository,
    ILogger<MarkTeleScheduledCommandHandler> logger)
    : IRequestHandler<MarkTeleScheduledCommand, InterventionDto>
{
    public async Task<InterventionDto> Handle(MarkTeleScheduledCommand request, CancellationToken ct)
    {
        var intervention = await repository.MarkTeleScheduledAsync(request.InterventionId, ct);

        logger.LogInformation(
            "Program.TeleScheduled: intervention={InterventionId} xpAwarded={XpAwardedTotal}",
            intervention.Id, intervention.XpAwardedTotal);

        return intervention;
    }
}
