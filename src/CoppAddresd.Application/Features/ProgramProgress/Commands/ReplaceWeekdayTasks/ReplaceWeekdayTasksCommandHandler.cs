using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using MediatR;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.ReplaceWeekdayTasks;

/// <summary>
/// Reemplaza el horario semanal de la plantilla (SPEC §7.6). El repositorio
/// valida la existencia (404) y ejecuta el reemplazo atómico
/// (<c>ExecuteDelete</c> + insert en una transacción).
/// </summary>
public sealed class ReplaceWeekdayTasksCommandHandler(
    IProgramRepository repository,
    ILogger<ReplaceWeekdayTasksCommandHandler> logger) : IRequestHandler<ReplaceWeekdayTasksCommand, IReadOnlyList<WeeklyDayTemplateDto>>
{
    public async Task<IReadOnlyList<WeeklyDayTemplateDto>> Handle(ReplaceWeekdayTasksCommand request, CancellationToken ct)
    {
        var template = await repository.GetTemplateAsync(request.TemplateId, ct)
            ?? throw new NotFoundException("Plantilla no encontrada.");
        var tasks = ProgramProgressTemplateMapper.ToEntities(request.Tasks);
        var prior = template.DayTemplates.Where(d => d.WeekNumber == null || d.WeekNumber == request.WeekNumber)
            .GroupBy(d => (d.Weekday, d.TaskCode))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.WeekNumber.HasValue).First());
        // Editar podcasts no significa quitar vínculos clínicos omitidos por el cliente.
        foreach (var task in tasks)
            if (prior.TryGetValue((task.Weekday, task.TaskCode), out var previous))
            {
                task.RoutineId ??= previous.RoutineId;
                task.NutritionPlanId ??= previous.NutritionPlanId;
            }

        var replaced = await repository.ReplaceWeekdayTasksAsync(
            request.TemplateId, tasks, request.ActorId, ct, request.WeekNumber);

        logger.LogInformation(
            "Program.ReplaceWeekdayTasks: template={TemplateId} filas={Count}",
            request.TemplateId, replaced.Count);

        return replaced.Select(WeeklyDayTemplateDto.FromEntity).ToList();
    }
}