using CoppAddresd.Application.Features.ProgramProgress.DTOs.Interventions;
using CoppAddresd.Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.MarkTeleAttended;

/// <summary>
/// Hook de telemedicina: asistencia confirmada por el clínico
/// (SPEC §22, D — AC-49): otorga <c>TELE_ATTEND</c> (+100, validated_by =
/// clínico) y mueve la intervención a <c>in_progress</c>.
/// </summary>
public sealed record MarkTeleAttendedCommand(
    Guid InterventionId,
    Guid ClinicianId) : IRequest<InterventionDto>;

/// <summary>Validación de forma del payload.</summary>
public sealed class MarkTeleAttendedCommandValidator : AbstractValidator<MarkTeleAttendedCommand>
{
    public MarkTeleAttendedCommandValidator()
    {
        RuleFor(x => x.InterventionId)
            .NotEmpty()
            .WithMessage("El interventionId es requerido.");
        RuleFor(x => x.ClinicianId)
            .NotEmpty()
            .WithMessage("El clinicianId es requerido.");
    }
}

/// <summary>Orquesta el hook vía el repositorio y loguea sin PHI.</summary>
public sealed class MarkTeleAttendedCommandHandler(
    IProgramRepository repository,
    ILogger<MarkTeleAttendedCommandHandler> logger)
    : IRequestHandler<MarkTeleAttendedCommand, InterventionDto>
{
    public async Task<InterventionDto> Handle(MarkTeleAttendedCommand request, CancellationToken ct)
    {
        var intervention = await repository.MarkTeleAttendedAsync(
            request.InterventionId, request.ClinicianId, ct);

        logger.LogInformation(
            "Program.TeleAttended: intervention={InterventionId} clinician={ClinicianId} " +
            "xpAwarded={XpAwardedTotal}",
            intervention.Id, request.ClinicianId, intervention.XpAwardedTotal);

        return intervention;
    }
}
