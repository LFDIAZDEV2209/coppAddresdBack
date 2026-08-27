using CoppAddresd.Application.Features.ProgramProgress.DTOs.Interventions;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateInterventionStatus;

/// <summary>
/// Clínico actualiza el estado de una intervención (SPEC §22, D — AC-48):
/// transiciones con auditoría; <c>completed</c> REQUIRES resultado y otorga
/// <c>INTERV_COMPLETE</c> (+200, validated_by = clínico) + debilidad
/// vinculada → <c>resolved</c>. <c>in_progress</c> es el estado por defecto
/// tras tele-asistencia confirmada. Estado inválido → 409.
/// </summary>
public sealed record UpdateInterventionStatusCommand(
    Guid InterventionId,
    InterventionStatus Status,
    Guid? ActorId,
    IReadOnlyList<string> CallerRoles,
    string? Result = null,
    Guid? AssignedTo = null) : IRequest<InterventionDto>;

/// <summary>Validación de forma del payload (D): el estado debe ser uno válido.</summary>
public sealed class UpdateInterventionStatusCommandValidator : AbstractValidator<UpdateInterventionStatusCommand>
{
    public UpdateInterventionStatusCommandValidator()
    {
        RuleFor(x => x.InterventionId)
            .NotEmpty()
            .WithMessage("El interventionId es requerido.");

        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("El estado de la intervención no es válido.");

        RuleFor(x => x.Result)
            .NotEmpty()
            .When(x => x.Status == InterventionStatus.completed)
            .WithMessage("Se requiere un resultado al completar una intervención.");
    }
}

/// <summary>Orquesta la transición vía el repositorio y loguea sin PHI.</summary>
public sealed class UpdateInterventionStatusCommandHandler(
    IProgramRepository repository,
    ILogger<UpdateInterventionStatusCommandHandler> logger)
    : IRequestHandler<UpdateInterventionStatusCommand, InterventionDto>
{
    public async Task<InterventionDto> Handle(UpdateInterventionStatusCommand request, CancellationToken ct)
    {
        var intervention = await repository.UpdateInterventionStatusAsync(
            request.InterventionId, request.Status, request.ActorId,
            request.CallerRoles, request.Result, request.AssignedTo, ct);

        logger.LogInformation(
            "Program.InterventionStatusChanged: intervention={InterventionId} " +
            "status={Status} actor={ActorId} xpAwarded={XpAwardedTotal}",
            intervention.Id, intervention.Status, request.ActorId, intervention.XpAwardedTotal);

        return intervention;
    }
}
