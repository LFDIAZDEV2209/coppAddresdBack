using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetWeekdayTasks;

public sealed record GetWeekdayTasksQuery(Guid TemplateId, int? WeekNumber = null)
    : IRequest<IReadOnlyList<WeeklyDayTemplateDto>>;

/// <summary>Lee una semana efectiva o las filas genéricas del contrato anterior.</summary>
public sealed class GetWeekdayTasksQueryHandler(IProgramRepository repository)
    : IRequestHandler<GetWeekdayTasksQuery, IReadOnlyList<WeeklyDayTemplateDto>>
{
    public async Task<IReadOnlyList<WeeklyDayTemplateDto>> Handle(GetWeekdayTasksQuery request, CancellationToken ct)
    {
        var template = await repository.GetTemplateAsync(request.TemplateId, ct)
            ?? throw new NotFoundException("Plantilla no encontrada.");
        ProgramTemplateWeekRules.ValidateWeek(template.TotalWeeks, request.WeekNumber);
        return template.DayTemplates
            .Where(d => (d.WeekNumber == null || d.WeekNumber == request.WeekNumber)
                && ProgramTemplateWeekRules.IsAllowedDay(template.TotalWeeks, request.WeekNumber, d.Weekday))
            .GroupBy(d => (d.Weekday, d.TaskCode))
            .Select(g => g.OrderByDescending(d => d.WeekNumber.HasValue).First())
            .OrderBy(d => d.Weekday).ThenBy(d => d.SortOrder)
            .Select(WeeklyDayTemplateDto.FromEntity).ToList();
    }
}
