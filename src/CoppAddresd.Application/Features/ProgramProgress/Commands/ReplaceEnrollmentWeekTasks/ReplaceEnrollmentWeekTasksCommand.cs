using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.ReplaceEnrollmentWeekTasks;

/// <summary>
/// Comando para reemplazar en bloque el horario/snapshot de tareas de una semana concreta
/// de una inscripción de paciente (<c>PUT /enrollments/{enrollmentId}/weeks/{weekNumber}/tasks</c>).
/// Reemplaza el <c>TasksSnapshot</c> de la semana activa.
/// </summary>
public sealed record ReplaceEnrollmentWeekTasksCommand(
    Guid EnrollmentId,
    int WeekNumber,
    IReadOnlyList<WeeklyDayTemplateRequest> Tasks,
    Guid? ActorId = null) : IRequest<EnrollmentWeekDetailDto>;

/// <summary>Validación de input de <see cref="ReplaceEnrollmentWeekTasksCommand"/>.</summary>
public sealed class ReplaceEnrollmentWeekTasksCommandValidator : AbstractValidator<ReplaceEnrollmentWeekTasksCommand>
{
    public ReplaceEnrollmentWeekTasksCommandValidator()
    {
        RuleFor(x => x.EnrollmentId)
            .NotEmpty()
            .WithMessage("El enrollmentId es requerido.");

        RuleFor(x => x.WeekNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("El número de semana debe ser al menos 1.");

        RuleFor(x => x.Tasks)
            .NotNull()
            .WithMessage("El conjunto de tareas es requerido.")
            .Must(tasks => tasks is { Count: > 0 })
            .WithMessage("Debe enviarse al menos una tarea.");

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
