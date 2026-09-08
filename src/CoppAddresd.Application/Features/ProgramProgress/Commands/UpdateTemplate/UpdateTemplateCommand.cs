using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateTemplate;

/// <summary>
/// Comando para actualizar una plantilla existente (SPEC §7.6, <c>Program.Edit</c>).
/// <c>Status</c>/<c>Version</c>/<c>PublishedAt</c> no cambian aquí: el estado
/// del ciclo de vida solo lo mueven <c>Publish</c> y <c>Archive</c>. El
/// conjunto de días se reemplaza en bloque (semántica de
/// <c>UpsertTemplateAsync</c>); para tocar solo el horario semanal sin tocar
/// metadatos está <c>ReplaceWeekdayTasks</c>.
/// </summary>
public sealed record UpdateTemplateCommand(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int? TotalWeeks = null,
    IReadOnlyList<WeeklyDayTemplateRequest>? Days = null,
    Guid? ActorId = null,
    int? TotalDays = null) : IRequest<ProgramTemplateDto>
{
    public int ResolvedTotalWeeks =>
        TotalDays.HasValue && TotalDays.Value > 0
            ? (int)Math.Ceiling(TotalDays.Value / 7.0)
            : TotalWeeks ?? 0;
}

/// <summary>Validación de input de <see cref="UpdateTemplateCommand"/> (T-11).</summary>
public sealed class UpdateTemplateCommandValidator : AbstractValidator<UpdateTemplateCommand>
{
    public UpdateTemplateCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El id de la plantilla es requerido.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("El código es requerido.")
            .MaximumLength(40)
            .WithMessage("El código no puede superar 40 caracteres.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("El nombre es requerido.")
            .MaximumLength(120)
            .WithMessage("El nombre no puede superar 120 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .WithMessage("La descripción no puede superar 2000 caracteres.");

        RuleFor(x => x.ResolvedTotalWeeks)
            .GreaterThan(0)
            .WithMessage("totalWeeks (o totalDays) debe ser mayor a cero.");

        When(x => x.Days is { Count: > 0 }, () =>
        {
            RuleForEach(x => x.Days!)
                .SetValidator(new CreateTemplate.WeeklyDayTemplateRequestValidator());

            RuleFor(x => x.Days!)
                .Must(NoDuplicateWeekdayTask)
                .WithMessage("No puede haber dos filas con el mismo (weekday, taskCode).");
        });
    }

    private static bool NoDuplicateWeekdayTask(IReadOnlyList<WeeklyDayTemplateRequest> days)
    {
        var seen = new HashSet<(short Weekday, TaskCode TaskCode)>();
        foreach (var day in days)
        {
            if (!seen.Add((day.Weekday, day.TaskCode)))
            {
                return false;
            }
        }

        return true;
    }
}