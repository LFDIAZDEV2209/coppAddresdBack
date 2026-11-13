using CoppAddresd.Application.Features.ProgramProgress.Queries.GetWeekdayTasks;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    [NutritionPostgresFact]
    public async Task TareasPorSemana_ReemplazoAisladoLecturaEfectivaYRechazoDia84()
    {
        await using var db = Context();
        var template = new ProgramTemplate { Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString("N"), Name = "83 días", TotalWeeks = 12 };
        db.ProgramTemplates.Add(template); await db.SaveChangesAsync();
        var repository = new ProgramRepository(db, new ConfigurationBuilder().Build());
        WeeklyDayTemplate[] Rows(int points, short weekday = 1) => [new() { Weekday = weekday, TaskCode = TaskCode.podcast, Points = points }];
        await repository.ReplaceWeekdayTasksAsync(template.Id, Rows(10), ct: CancellationToken.None);
        await repository.ReplaceWeekdayTasksAsync(template.Id, Rows(20), ct: CancellationToken.None, weekNumber: 2);
        await repository.ReplaceWeekdayTasksAsync(template.Id, Rows(30), ct: CancellationToken.None, weekNumber: 3);
        await repository.ReplaceWeekdayTasksAsync(template.Id, Rows(25), ct: CancellationToken.None, weekNumber: 2);
        var handler = new GetWeekdayTasksQueryHandler(repository);
        Assert.Equal(25, Assert.Single(await handler.Handle(new(template.Id, 2), CancellationToken.None)).Points);
        Assert.Equal(30, Assert.Single(await handler.Handle(new(template.Id, 3), CancellationToken.None)).Points);
        Assert.Equal(10, Assert.Single(await handler.Handle(new(template.Id, 4), CancellationToken.None)).Points);
        Assert.Null(Assert.Single(await handler.Handle(new(template.Id), CancellationToken.None)).WeekNumber);
        Assert.Equal(2, Assert.Single(await handler.Handle(new(template.Id, 2), CancellationToken.None)).WeekNumber);
        await repository.ReplaceWeekdayTasksAsync(template.Id, [.. Rows(11), .. Rows(17, 7)], ct: CancellationToken.None);
        Assert.Equal(25, Assert.Single(await handler.Handle(new(template.Id, 2), CancellationToken.None), d => d.Weekday == 1).Points);
        Assert.Equal(30, Assert.Single(await handler.Handle(new(template.Id, 3), CancellationToken.None), d => d.Weekday == 1).Points);
        Assert.DoesNotContain(await handler.Handle(new(template.Id, 12), CancellationToken.None), d => d.Weekday == 7);
        await repository.ReplaceWeekdayTasksAsync(template.Id, Rows(83, 6), ct: CancellationToken.None, weekNumber: 12);
        Assert.Contains(await handler.Handle(new(template.Id, 12), CancellationToken.None), d => d.Weekday == 6 && d.Points == 83);
        var error = await Assert.ThrowsAsync<UnprocessableEntityException>(() => repository.ReplaceWeekdayTasksAsync(template.Id, Rows(84, 7), ct: CancellationToken.None, weekNumber: 12));
        Assert.Contains("PROGRAM_DAY_OUT_OF_RANGE", error.Message);
        Assert.Contains(await handler.Handle(new(template.Id, 12), CancellationToken.None), d => d.Weekday == 6 && d.Points == 83);
        foreach (var invalid in new[] { 0, 13 })
        {
            await Assert.ThrowsAsync<UnprocessableEntityException>(() => handler.Handle(new(template.Id, invalid), CancellationToken.None));
            await Assert.ThrowsAsync<UnprocessableEntityException>(() => repository.ReplaceWeekdayTasksAsync(template.Id, Rows(99), ct: CancellationToken.None, weekNumber: invalid));
        }
        await using var verify = Context();
        Assert.Equal(5, await verify.WeeklyDayTemplates.CountAsync(t => t.TemplateId == template.Id));
        // El CRUD anterior de metadatos/genéricas tampoco borra las específicas.
        await using var editRequest = Context();
        await new ProgramRepository(editRequest, new ConfigurationBuilder().Build())
            .UpsertTemplateAsync(template, Rows(12), ct: CancellationToken.None);
        Assert.Equal(25, Assert.Single(await handler.Handle(new(template.Id, 2), CancellationToken.None)).Points);
        await using var metadataRequest = Context();
        var metadataHandler = new CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateTemplate.UpdateTemplateCommandHandler(
            new ProgramRepository(metadataRequest, new ConfigurationBuilder().Build()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CoppAddresd.Application.Features.ProgramProgress.Commands.UpdateTemplate.UpdateTemplateCommandHandler>.Instance);
        await metadataHandler.Handle(new(template.Id, template.Code, "Nombre editado", null, TotalWeeks: 12), CancellationToken.None);
        Assert.Equal(25, Assert.Single(await handler.Handle(new(template.Id, 2), CancellationToken.None)).Points);
        Assert.Equal(4, await verify.WeeklyDayTemplates.CountAsync(t => t.TemplateId == template.Id));
    }
}
