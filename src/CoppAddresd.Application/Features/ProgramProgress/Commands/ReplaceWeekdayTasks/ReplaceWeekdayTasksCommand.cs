using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.ReplaceWeekdayTasks;

/// <summary>
/// Comando para reemplazar en bloque el horario semanal de una plantilla
/// (<c>PUT /templates/{id}/weekday-tasks</c>, SPEC §7.6). La plantilla no
/// existe → 404. Reemplazo atómico: las filas previas se eliminan y se
/// insertan las nuevas en la misma transacción.
/// </summary>
public sealed record ReplaceWeekdayTasksCommand(
    Guid TemplateId,
    IReadOnlyList<WeeklyDayTemplateRequest> Tasks,
    Guid? ActorId = null) : IRequest<IReadOnlyList<WeeklyDayTemplateDto>>;

/// <summary>Validación de input de <see cref="ReplaceWeekdayTasksCommand"/> (T-11).</summary>
public sealed class ReplaceWeekdayTasksCommandValidator : AbstractValidator<ReplaceWeekdayTasksCommand>
{
    public ReplaceWeekdayTasksCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty()
            .WithMessage("El templateId es requerido.");

        RuleFor(x => x.Tasks)
            .NotNull()
            .WithMessage("El conjunto de tareas es requerido.")
            .Must(tasks => tasks is { Count: > 0 })
            .WithMessage("Debe enviarse al menos una tarea por día.");

        RuleForEach(x => x.Tasks)
            .SetValidator(new CreateTemplate.WeeklyDayTemplateRequestValidator());

        RuleFor(x => x.Tasks)
            .Must(tasks => tasks is null || NoDuplicateWeekdayTask(tasks))
            .WithMessage("No puede haber dos filas con el mismo (weekday, taskCode).");
    }

    private static bool NoDuplicateWeekdayTask(IReadOnlyList<WeeklyDayTemplateRequest> tasks)
    {
        var seen = new HashSet<(short Weekday, TaskCode TaskCode)>();
        foreach (var task in tasks)
        {
            if (!seen.Add((task.Weekday, task.TaskCode)))
            {
                return false;
            }
        }

        return true;
    }
}