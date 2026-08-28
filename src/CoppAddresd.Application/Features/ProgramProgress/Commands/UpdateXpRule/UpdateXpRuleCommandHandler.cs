using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateXpRule;

/// <summary>
/// Aplica la edición prospective de una regla del catálogo de XP (SPEC §14.4):
/// carga la regla por código (404 si no existe), valida que
/// <c>valid_until</c> no retroceda antes de <c>valid_from</c>, copia los
/// campos editables y persiste. Nunca toca <c>app.xp_ledger</c>: el historial
/// queda intacto (prospective only).
/// </summary>
public sealed class UpdateXpRuleCommandHandler(
    IXpRuleCatalogRepository repository,
    ILogger<UpdateXpRuleCommandHandler> logger) : IRequestHandler<UpdateXpRuleCommand, XpRuleDto>
{
    public async Task<XpRuleDto> Handle(UpdateXpRuleCommand request, CancellationToken ct)
    {
        var rule = await repository.GetByCodeAsync(request.Code, ct)
            ?? throw new NotFoundException($"Regla de XP {request.Code} no encontrada.");

        // Prospective only (SPEC §14.4): una ventana válida no puede empezar
        // en el futuro de su propio inicio; el handler rechaza el retroceso
        // ANTES de persistir (el validador cubre el resto de los rangos).
        if (request.ValidUntil is { } validUntil && validUntil < rule.ValidFrom)
        {
            throw new UnprocessableEntityException(
                $"INVALID_VALIDITY_WINDOW: valid_until {validUntil:yyyy-MM-dd} es anterior " +
                $"a valid_from {rule.ValidFrom:yyyy-MM-dd} de la regla {rule.Code}.");
        }

        rule.BaseXp = request.BaseXp;
        rule.Multiplier = request.Multiplier;
        rule.MaxPerDay = request.MaxPerDay;
        rule.MaxPerWeek = request.MaxPerWeek;
        rule.RequiresValidation = request.RequiresValidation;
        rule.Active = request.Active;
        rule.ValidUntil = request.ValidUntil;

        var updated = await repository.UpdateAsync(rule, request.ActorId, ct);

        logger.LogInformation(
            "Program.UpdateXpRule: rule={RuleCode} baseXp={BaseXp} multiplier={Multiplier} " +
            "maxPerDay={MaxPerDay} maxPerWeek={MaxPerWeek} active={Active} actor={ActorId}",
            updated.Code, updated.BaseXp, updated.Multiplier,
            updated.MaxPerDay, updated.MaxPerWeek, updated.Active, request.ActorId);

        return XpRuleDto.FromEntity(updated);
    }
}