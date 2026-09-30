using CoppAddresd.Domain.Enums.ProgramProgress;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.BulkAssignProgramContent;

/// <summary>
/// Asignación masiva de lecciones/podcasts del programa (REQ-PCA-04): aplica
/// un <paramref name="MediaId"/> sobre un rango de semanas y días concretos,
/// ya sea sobre una plantilla (<c>TargetType = "Template"</c>) o sobre las
/// semanas de una inscripción (<c>TargetType = "Enrollment"</c>).
/// La propagación respeta la inmutabilidad de las semanas congeladas
/// (design D2): completadas o con fecha de cierre ya ocurrida, que solo se
/// tocan con la bandera explícita <paramref name="ForceFrozen"/> (evento de
/// auditoría crítico).
/// </summary>
/// <param name="TargetType">"Template" (plantilla del catálogo) o "Enrollment" (inscripción).</param>
/// <param name="TargetId">Id de la plantilla o de la inscripción según TargetType.</param>
/// <param name="FromWeek">Semana inicial del rango (1..83).</param>
/// <param name="ToWeek">Semana final del rango (mayor o igual a FromWeek).</param>
/// <param name="Weekdays">Días objetivo (1 = lunes … 7 = domingo, ISO).</param>
/// <param name="MediaId">Medio (podcast) a asignar. Debe estar Published.</param>
/// <param name="ForceFrozen">Propagar también sobre semanas congeladas (trazabilidad crítica).</param>
/// <param name="ActorId">Usuario del ERP que ejecuta (auditoría).</param>
public sealed record BulkAssignProgramContentCommand(
    string TargetType,
    Guid TargetId,
    int FromWeek,
    int ToWeek,
    IReadOnlyList<short> Weekdays,
    Guid MediaId,
    bool ForceFrozen = false,
    Guid? ActorId = null
) : IRequest<BulkAssignProgramContentResult>;

/// <summary>
/// Resultado transparente de la propagación (contract §4 del change):
/// <c>{ success, totalWeeksTargeted, updatedWeeks, frozenWeeksSkipped, affectedEnrollments, message }</c>.
/// </summary>
public sealed record BulkAssignProgramContentResult(
    bool Success,
    int TotalWeeksTargeted,
    int UpdatedWeeks,
    int FrozenWeeksSkipped,
    int AffectedEnrollments,
    string Message
);

/// <summary>Validación de input de <see cref="BulkAssignProgramContentCommand"/>.</summary>
public sealed class BulkAssignProgramContentCommandValidator
    : AbstractValidator<BulkAssignProgramContentCommand>
{
    /// <summary>Tope absoluto de semanas del programa (83 días).</summary>
    public const int ProgramMaxWeeks = 83;

    public BulkAssignProgramContentCommandValidator()
    {
        RuleFor(x => x.TargetType)
            .NotEmpty()
            .WithMessage("El targetType es requerido.")
            .Must(t => NormalizeTargetType(t) is not null)
            .WithMessage("El targetType debe ser 'Template' o 'Enrollment'.");

        RuleFor(x => x.TargetId).NotEmpty().WithMessage("El targetId es requerido.");

        RuleFor(x => x.MediaId).NotEmpty().WithMessage("El mediaId es requerido.");

        RuleFor(x => x.FromWeek)
            .InclusiveBetween(1, ProgramMaxWeeks)
            .WithMessage($"El rango de semanas debe estar entre 1 y {ProgramMaxWeeks}.");

        RuleFor(x => x.ToWeek)
            .InclusiveBetween(1, ProgramMaxWeeks)
            .WithMessage($"El rango de semanas debe estar entre 1 y {ProgramMaxWeeks}.")
            .GreaterThanOrEqualTo(x => x.FromWeek)
            .WithMessage(
                "La semana final (toWeek) debe ser mayor o igual a la inicial (fromWeek)."
            );

        RuleFor(x => x.Weekdays)
            .NotNull()
            .WithMessage("Los días de la semana (weekdays) son requeridos.")
            .Must(w => w is { Count: > 0 })
            .WithMessage("Debe seleccionarse al menos un día (1 = lunes … 7 = domingo).")
            .Must(w => w == null || w.All(d => d is >= 1 and <= 7))
            .WithMessage("Los días de la semana deben estar entre 1 (lunes) y 7 (domingo).");
    }

    /// <summary>Normaliza el targetType a la forma canónica (o null si es inválido).</summary>
    public static string? NormalizeTargetType(string? targetType) =>
        targetType?.Trim().ToLowerInvariant() switch
        {
            "template" => "Template",
            "enrollment" => "Enrollment",
            _ => null,
        };
}
