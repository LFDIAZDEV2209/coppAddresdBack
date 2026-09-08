using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.CreateTemplate;

/// <summary>
/// Comando para crear una plantilla de programa en estado <c>Draft</c>
/// (versión 1; la publicación la incrementa, SPEC §7.6). Requiere
/// <c>Program.Edit</c> en la capa API. El conjunto de días es la fuente de
/// verdad de la semana (7 días × 6 tareas en la plantilla por defecto).
/// </summary>
public sealed record CreateTemplateCommand(
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

/// <summary>
/// Validación de input de <see cref="CreateTemplateCommand"/> (T-11): campos
/// requeridos no vacíos, puntos no negativos y sin filas duplicadas por
/// (weekday, task_code) dentro del payload (el índice único del repositorio lo
/// exigiría con 409; aquí se rechaza antes, en 400).
/// Si Days es null o vacío, el handler generará las 42 tareas por defecto.
/// </summary>
public sealed class CreateTemplateCommandValidator : AbstractValidator<CreateTemplateCommand>
{
    public CreateTemplateCommandValidator()
    {
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
                .SetValidator(new WeeklyDayTemplateRequestValidator());

            RuleFor(x => x.Days!)
                .Must(NoDuplicateWeekdayTask)
                .WithMessage("No puede haber dos filas con el mismo (weekday, taskCode).");
        });
    }

    private static bool NoDuplicateWeekdayTask(IReadOnlyList<WeeklyDayTemplateRequest>? days)
    {
        if (days is null)
        {
            return true;
        }

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

/// <summary>Reglas de una fila por día de la semana (compartidas por los comandos de plantilla).</summary>
public sealed class WeeklyDayTemplateRequestValidator : AbstractValidator<WeeklyDayTemplateRequest>
{
    public WeeklyDayTemplateRequestValidator()
    {
        RuleFor(x => x.Weekday)
            .InclusiveBetween((short)1, (short)7)
            .WithMessage("weekday debe estar entre 1 (lunes) y 7 (domingo).");

        RuleFor(x => x.TaskCode)
            .IsInEnum()
            .WithMessage("taskCode inválido.");

        RuleFor(x => x.Points)
            .GreaterThanOrEqualTo(0)
            .WithMessage("points no puede ser negativo.");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("sortOrder no puede ser negativo.");
    }
}