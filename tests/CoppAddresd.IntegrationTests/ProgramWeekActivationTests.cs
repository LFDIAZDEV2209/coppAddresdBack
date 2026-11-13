using System.Text.Json;
using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    [NutritionPostgresFact]
    public Task ActivacionPorFecha_ConservaPreedicionFutura() => VerificarPreedicionAsync(false);

    [NutritionPostgresFact]
    public Task ActivacionPorSemanaPerfecta_ConservaPreedicionFutura() => VerificarPreedicionAsync(true);

    private async Task VerificarPreedicionAsync(bool semanaPerfecta)
    {
        await using var db = Context();
        var patient = await PatientAsync(db);
        var template = new ProgramTemplate { Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString("N"),
            Name = "Activación", TotalWeeks = 12, Status = TemplateStatus.Active };
        var days = Enumerable.Range(1, 7).Select(day => new WeeklyDayTemplate {
            Id = Guid.NewGuid(), TemplateId = template.Id, Weekday = (short)day,
            TaskCode = TaskCode.podcast, Points = 10, SortOrder = 1 }).ToArray();
        db.ProgramTemplates.Add(template); db.WeeklyDayTemplates.AddRange(days);
        var start = new DateOnly(2026, 11, 2);
        var enrollment = new ProgramEnrollment { Id = Guid.NewGuid(), PatientId = patient.Id,
            TemplateId = template.Id, StartLocalDate = start };
        var first = new ProgramWeek { Id = Guid.NewGuid(), EnrollmentId = enrollment.Id, WeekNumber = 1,
            Status = ProgramWeekStatus.Active, WeekStartDateLocal = start, WeekEndDateLocal = start.AddDays(6),
            TemplateVersionAtStart = 1, TasksSnapshot = JsonSerializer.SerializeToElement(days.Select(d => new {
                weekday = d.Weekday, task_code = "podcast", points = 10, sort_order = 1 })) };
        var next = new ProgramWeek { Id = Guid.NewGuid(), EnrollmentId = enrollment.Id, WeekNumber = 2,
            WeekStartDateLocal = start.AddDays(7), WeekEndDateLocal = start.AddDays(13),
            TasksSnapshot = JsonSerializer.SerializeToElement(Array.Empty<object>()) };
        db.ProgramEnrollments.Add(enrollment); db.ProgramWeeks.AddRange(first, next);
        await db.SaveChangesAsync();
        var repository = new ProgramRepository(db, new ConfigurationBuilder().Build());

        // Se usa la misma escritura que el ERP; la segunda semana aún está bloqueada.
        var edited = days.Select(d => new WeeklyDayTemplate { Weekday = d.Weekday, TaskCode = d.TaskCode,
            Points = d.Weekday == 4 ? 77 : 10, SortOrder = d.SortOrder }).ToArray();
        await repository.ReplaceEnrollmentWeekTasksAsync(enrollment.Id, 2, edited, ct: CancellationToken.None);
        Assert.Equal(77, next.TasksSnapshot.EnumerateArray().Single(t => t.GetProperty("weekday").GetInt32() == 4).GetProperty("points").GetInt32());

        if (semanaPerfecta)
        {
            for (var day = 0; day < 7; day++)
                await repository.CompleteTaskAsync(new CompleteTaskInput(enrollment.Id, start.AddDays(day),
                    TaskCode.podcast, null, null, null, null, null), CancellationToken.None);
        }
        else
            await repository.CompleteTaskAsync(new CompleteTaskInput(enrollment.Id, start.AddDays(7),
                TaskCode.podcast, null, null, null, null, null), CancellationToken.None);

        await using var verification = Context();
        var activated = await verification.ProgramWeeks.AsNoTracking().SingleAsync(w => w.Id == next.Id);
        Assert.Equal(ProgramWeekStatus.Active, activated.Status);
        // Regresión 1.2: la activación conserva el jueves preeditado.
        Assert.Equal(77, activated.TasksSnapshot.EnumerateArray().Single(t => t.GetProperty("weekday").GetInt32() == 4).GetProperty("points").GetInt32());
    }
}
