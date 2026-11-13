using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ReplaceWeekdayTasks;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetWeekdayTasks;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    [NutritionPostgresFact]
    public async Task SemanaPodcasts_ConservaReferenciasClinicasEnDtoYReemplazo()
    {
        await using var db = Context();
        var template = new ProgramTemplate { Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString("N"), Name = "Vínculos", TotalWeeks = 12 };
        var routine = new ExerciseRoutine { Id = Guid.NewGuid(), Name = "Rutina", Status = NutritionPlanStatus.Active };
        var general = new ExerciseRoutine { Id = Guid.NewGuid(), Name = "Rutina genérica", Status = NutritionPlanStatus.Active };
        var plan = new NutritionPlan { Id = Guid.NewGuid(), Name = "Plan", DurationDays = 7, Status = NutritionPlanStatus.Active };
        var podcast = new MediaItem { Id = Guid.NewGuid(), Title = "Podcast", Author = "Equipo", MediaType = MediaType.Podcast,
            Category = MediaCategory.Biologia, Status = MediaStatus.Published, StorageKey = "media/podcasts/prueba.mp3" };
        db.MediaItems.Add(podcast);
        db.ProgramTemplates.Add(template); db.ExerciseRoutines.AddRange(routine, general); db.NutritionPlans.Add(plan);
        db.WeeklyDayTemplates.AddRange(
            new() { TemplateId = template.Id, Weekday = 1, TaskCode = TaskCode.ejercicio, RoutineId = general.Id },
            new() { TemplateId = template.Id, Weekday = 1, TaskCode = TaskCode.nut, NutritionPlanId = plan.Id },
            new() { TemplateId = template.Id, WeekNumber = 2, Weekday = 1, TaskCode = TaskCode.ejercicio, RoutineId = routine.Id },
            new() { TemplateId = template.Id, WeekNumber = 2, Weekday = 1, TaskCode = TaskCode.nut, NutritionPlanId = plan.Id });
        await db.SaveChangesAsync();
        var repository = new ProgramRepository(db, new ConfigurationBuilder().Build());
        var reader = new GetWeekdayTasksQueryHandler(repository);
        var writer = new ReplaceWeekdayTasksCommandHandler(repository, NullLogger<ReplaceWeekdayTasksCommandHandler>.Instance);
        IReadOnlyList<WeeklyDayTemplateRequest> PodcastOnly = [new(1, TaskCode.podcast, 10, MediaId: podcast.Id)];
        await writer.Handle(new(template.Id, PodcastOnly, WeekNumber: 2), CancellationToken.None);
        var saved = await reader.Handle(new(template.Id, 2), CancellationToken.None);
        Assert.Equal(podcast.Id, Assert.Single(saved, d => d.TaskCode == TaskCode.podcast).MediaId);
        Assert.Equal(routine.Id, Assert.Single(saved, d => d.TaskCode == TaskCode.ejercicio).RoutineId);
        Assert.Equal(plan.Id, Assert.Single(saved, d => d.TaskCode == TaskCode.nut).NutritionPlanId);
        Assert.Equal(general.Id, Assert.Single(await reader.Handle(new(template.Id, 3), CancellationToken.None), d => d.TaskCode == TaskCode.ejercicio).RoutineId);

        // Cliente anterior: reenvía todas las misiones pero omite routineId/nutritionPlanId.
        var full = saved.Select(d => new WeeklyDayTemplateRequest(d.Weekday, d.TaskCode, d.Points, d.SortOrder, d.MediaId)).ToArray();
        await writer.Handle(new(template.Id, full, WeekNumber: 2), CancellationToken.None);
        saved = await reader.Handle(new(template.Id, 2), CancellationToken.None);
        Assert.Equal(routine.Id, Assert.Single(saved, d => d.TaskCode == TaskCode.ejercicio).RoutineId);
        Assert.Equal(plan.Id, Assert.Single(saved, d => d.TaskCode == TaskCode.nut).NutritionPlanId);

        // Primera escritura de la semana: hereda los vínculos genéricos, sin tocar otra semana.
        await writer.Handle(new(template.Id, full, WeekNumber: 4), CancellationToken.None);
        var inherited = await reader.Handle(new(template.Id, 4), CancellationToken.None);
        Assert.Equal(general.Id, Assert.Single(inherited, d => d.TaskCode == TaskCode.ejercicio).RoutineId);
        Assert.Equal(plan.Id, Assert.Single(inherited, d => d.TaskCode == TaskCode.nut).NutritionPlanId);
        Assert.Equal(routine.Id, Assert.Single(await reader.Handle(new(template.Id, 2), CancellationToken.None), d => d.TaskCode == TaskCode.ejercicio).RoutineId);
    }
}
