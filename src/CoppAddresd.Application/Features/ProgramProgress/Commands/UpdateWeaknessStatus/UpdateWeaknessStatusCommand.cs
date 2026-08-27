using CoppAddresd.Application.Features.ProgramProgress.DTOs.Weaknesses;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateWeaknessStatus;

/// <summary>
/// Transición de estado de una debilidad (SPEC §21, D — AC-44): el clínico
/// reconoce (<c>acknowledged</c>), interviene (<c>in_intervention</c>),
/// resuelve (<c>resolved</c>, fija <c>resolved_at</c>) o descarta
/// (<c>dismissed</c>) un hallazgo. La API lo protege con <c>Program.Adapt</c> y
/// el rol clínico lo exige el repositorio (misma guardia AC-22 que la línea
/// base clínica y la revisión de XP: un paciente cambiando el estado de su
/// propia debilidad → 403 FORBIDDEN). Transición idempotente: aplicar el mismo
/// estado devuelve la fila sin error.
/// </summary>
public sealed record UpdateWeaknessStatusCommand(
    Guid WeaknessId,
    WeaknessStatus Status,
    Guid? ActorId,
    IReadOnlyList<string> CallerRoles) : IRequest<WeaknessDto>;

/// <summary>
/// Validación de forma del payload (D): el <c>weaknessId</c> es requerido y el
/// estado debe ser uno de los transicionables por el clínico
/// (<c>acknowledged</c>/<c>in_intervention</c>/<c>resolved</c>/<c>dismissed</c>).
/// <c>open</c> se rechaza: una fila recién detectada ya nace abierta y el ciclo
/// de vida no regresa a ese estado (SPEC §21, A). La regla de negocio (rol
/// clínico, existencia de la fila) vive en el repositorio, como en la línea
/// base clínica (AC-22).
/// </summary>
public sealed class UpdateWeaknessStatusCommandValidator : AbstractValidator<UpdateWeaknessStatusCommand>
{
    public UpdateWeaknessStatusCommandValidator()
    {
        RuleFor(x => x.WeaknessId)
            .NotEmpty()
            .WithMessage("El weaknessId es requerido.");

        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("El estado de la debilidad no es válido.")
            .Must(status => status is WeaknessStatus.acknowledged
                or WeaknessStatus.in_intervention
                or WeaknessStatus.resolved
                or WeaknessStatus.dismissed)
            .WithMessage("El estado open no es transicionable por el clínico (la fila ya nace abierta).");
    }
}

/// <summary>
/// Orquesta la transición vía el repositorio (una transacción: estado + sello
/// <c>resolved_at</c> si aplica) y loguea sin PHI.
/// </summary>
public sealed class UpdateWeaknessStatusCommandHandler(
    IProgramRepository repository,
    ILogger<UpdateWeaknessStatusCommandHandler> logger)
    : IRequestHandler<UpdateWeaknessStatusCommand, WeaknessDto>
{
    public async Task<WeaknessDto> Handle(UpdateWeaknessStatusCommand request, CancellationToken ct)
    {
        var weakness = await repository.UpdateWeaknessStatusAsync(
            request.WeaknessId, request.Status, request.ActorId, request.CallerRoles, ct);

        logger.LogInformation(
            "Program.WeaknessStatusChanged: weakness={WeaknessId} patient={PatientId} " +
            "code={Code} status={Status} actor={ActorId}",
            weakness.Id, weakness.PatientId, weakness.Code, weakness.Status, request.ActorId);

        return weakness;
    }
}