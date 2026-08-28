using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using MediatR;
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
        var tasks = ProgramProgressTemplateMapper.ToEntities(request.Tasks);

        var replaced = await repository.ReplaceWeekdayTasksAsync(
            request.TemplateId, tasks, request.ActorId, ct);

        logger.LogInformation(
            "Program.ReplaceWeekdayTasks: template={TemplateId} filas={Count}",
            request.TemplateId, replaced.Count);

        return replaced.Select(WeeklyDayTemplateDto.FromEntity).ToList();
    }
}