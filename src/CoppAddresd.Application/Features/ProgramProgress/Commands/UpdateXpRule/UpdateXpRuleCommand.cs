using CoppAddresd.Application.DTOs.ProgramProgress;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateXpRule;

/// <summary>
/// Comando para actualizar una regla del catálogo de XP (SPEC §14.4, permiso
/// <c>Program.Edit</c>). <b>Prospective only</b>: la edición nunca reescribe el
/// historial de <c>app.xp_ledger</c> (la columna <c>rule_code</c> de cada
/// entrada es solo provenance); solo afecta otorgamientos futuros.
///
/// La identidad de la regla (código, nombre, categoría, <c>valid_from</c>) no
/// es editable aquí: el código es la clave de negocio del catálogo y de la FK
/// de <c>xp_ledger.rule_code</c>.
/// </summary>
public sealed record UpdateXpRuleCommand(
    string Code,
    int? BaseXp,
    decimal Multiplier,
    int? MaxPerDay,
    int? MaxPerWeek,
    bool RequiresValidation,
    bool Active,
    DateOnly? ValidUntil,
    Guid? ActorId = null) : IRequest<XpRuleDto>;

/// <summary>
/// Validación de input de <see cref="UpdateXpRuleCommand"/> (SPEC §14.4):
/// rechaza multiplier &lt;= 0, base_xp &lt; 0 y topes &lt; 0; además valida que
/// <c>valid_until</c>, cuando se envía, no sea anterior a la vigencia vigente
/// de la regla (el handler carga la regla y compara).
/// </summary>
public sealed class UpdateXpRuleCommandValidator : AbstractValidator<UpdateXpRuleCommand>
{
    public UpdateXpRuleCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("El código de la regla es requerido.")
            .MaximumLength(60)
            .WithMessage("El código no puede superar 60 caracteres.");

        RuleFor(x => x.Multiplier)
            .GreaterThan(0)
            .WithMessage("El multiplicador debe ser mayor a cero.");

        RuleFor(x => x.BaseXp)
            .GreaterThanOrEqualTo(0)
            .When(x => x.BaseXp.HasValue)
            .WithMessage("baseXp no puede ser negativo.");

        RuleFor(x => x.MaxPerDay)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MaxPerDay.HasValue)
            .WithMessage("maxPerDay no puede ser negativo.");

        RuleFor(x => x.MaxPerWeek)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MaxPerWeek.HasValue)
            .WithMessage("maxPerWeek no puede ser negativo.");
    }
}