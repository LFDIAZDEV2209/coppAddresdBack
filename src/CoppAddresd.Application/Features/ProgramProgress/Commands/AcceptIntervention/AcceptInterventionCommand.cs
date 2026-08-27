using CoppAddresd.Application.Features.ProgramProgress.DTOs.Interventions;
using CoppAddresd.Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.AcceptIntervention;

/// <summary>
/// Paciente acepta una intervención (SPEC §22, D — AC-47):
/// <c>detected→accepted</c>, fija <c>accepted_at</c>, otorga
/// <c>INTERV_ACCEPT</c> (+15). Si es <c>recovery_mission</c> también
/// <c>RECOVERY_MISSION</c> (+50). Requiere que la intervención pertenezca
/// al paciente. Estado inválido → 409.
/// </summary>
public sealed record AcceptInterventionCommand(
    Guid InterventionId,
    Guid PatientId) : IRequest<InterventionDto>;

/// <summary>Validación de forma del payload (D).</summary>
public sealed class AcceptInterventionCommandValidator : AbstractValidator<AcceptInterventionCommand>
{
    public AcceptInterventionCommandValidator()
    {
        RuleFor(x => x.InterventionId)
            .NotEmpty()
            .WithMessage("El interventionId es requerido.");
        RuleFor(x => x.PatientId)
            .NotEmpty()
            .WithMessage("El patientId es requerido.");
    }
}

/// <summary>Orquesta la aceptación vía el repositorio y loguea sin PHI.</summary>
public sealed class AcceptInterventionCommandHandler(
    IProgramRepository repository,
    ILogger<AcceptInterventionCommandHandler> logger)
    : IRequestHandler<AcceptInterventionCommand, InterventionDto>
{
    public async Task<InterventionDto> Handle(AcceptInterventionCommand request, CancellationToken ct)
    {
        var intervention = await repository.AcceptInterventionAsync(
            request.InterventionId, request.PatientId, ct);

        logger.LogInformation(
            "Program.InterventionAccepted: intervention={InterventionId} patient={PatientId} " +
            "type={Type} xpAwarded={XpAwardedTotal}",
            intervention.Id, request.PatientId, intervention.Type, intervention.XpAwardedTotal);

        return intervention;
    }
}
