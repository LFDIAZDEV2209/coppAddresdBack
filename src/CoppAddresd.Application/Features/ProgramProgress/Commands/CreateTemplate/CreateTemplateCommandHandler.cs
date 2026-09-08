using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.CreateTemplate;

/// <summary>
/// Crea la plantilla en <c>Draft</c> (versión 1) con su conjunto de días y la
/// devuelve con el detalle (misma forma que <c>GET /templates/{id}</c>). El
/// repositorio reemplaza las filas por día y traduce el código duplicado a
/// 409 <c>TEMPLATE_CODE_EXISTS</c>.
/// </summary>
public sealed class CreateTemplateCommandHandler(
    IProgramRepository repository,
    ILogger<CreateTemplateCommandHandler> logger) : IRequestHandler<CreateTemplateCommand, ProgramTemplateDto>
{
    private static readonly (TaskCode Code, int Points)[] DefaultTaskSeeds =
    [
        (TaskCode.podcast, 80),
        (TaskCode.vitals, 120),
        (TaskCode.nut, 150),
        (TaskCode.ejercicio, 150),
        (TaskCode.nutraceutico, 80),
        (TaskCode.emocional, 120),
    ];

    public async Task<ProgramTemplateDto> Handle(CreateTemplateCommand request, CancellationToken ct)
    {
        var totalWeeks = request.ResolvedTotalWeeks;

        var template = new ProgramTemplate
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            TotalWeeks = totalWeeks,
            Status = TemplateStatus.Draft,
            Version = 1,
            StreakMinTasks = 1,
            EssentialTaskCodes = ["nut", "ejercicio", "nutraceutico"],
            CreatedAt = DateTime.UtcNow,
        };

        var requestDays = request.Days is { Count: > 0 }
            ? request.Days
            : BuildDefaultDays();

        var days = ProgramProgressTemplateMapper.ToEntities(requestDays);

        var created = await repository.UpsertTemplateAsync(template, days, request.ActorId, ct);

        var dto = await repository.GetTemplateAsync(created.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la plantilla creada.");

        logger.LogInformation(
            "Program.CreateTemplate: template={TemplateId} code={Code} semanas={TotalWeeks} filas={DayCount}",
            created.Id, created.Code, created.TotalWeeks, days.Count);

        return ProgramTemplateDto.FromEntity(dto);
    }

    public static IReadOnlyList<WeeklyDayTemplateRequest> BuildDefaultDays()
    {
        var list = new List<WeeklyDayTemplateRequest>(7 * DefaultTaskSeeds.Length);
        for (short weekday = 1; weekday <= 7; weekday++)
        {
            for (var i = 0; i < DefaultTaskSeeds.Length; i++)
            {
                list.Add(new WeeklyDayTemplateRequest(
                    Weekday: weekday,
                    TaskCode: DefaultTaskSeeds[i].Code,
                    Points: DefaultTaskSeeds[i].Points,
                    SortOrder: i + 1));
            }
        }
        return list;
    }
}