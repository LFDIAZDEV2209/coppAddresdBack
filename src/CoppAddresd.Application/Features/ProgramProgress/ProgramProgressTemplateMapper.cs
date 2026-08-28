using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Domain.Entities.ProgramProgress;

namespace CoppAddresd.Application.Features.ProgramProgress;

/// <summary>
/// Mapeo de filas por día de la semana del payload (CRUD de plantillas, SPEC
/// §7.6) a la entidad <see cref="WeeklyDayTemplate"/>. El Id lo asigna el
/// repositorio (<c>AddDayTemplates</c>); aquí solo se transporta la forma.
/// </summary>
internal static class ProgramProgressTemplateMapper
{
    public static WeeklyDayTemplate ToEntity(WeeklyDayTemplateRequest r) => new()
    {
        Weekday = r.Weekday,
        TaskCode = r.TaskCode,
        Points = r.Points,
        SortOrder = r.SortOrder,
        MediaId = r.MediaId,
    };

    public static IReadOnlyList<WeeklyDayTemplate> ToEntities(IEnumerable<WeeklyDayTemplateRequest> rows)
        => rows.Select(ToEntity).ToList();
}