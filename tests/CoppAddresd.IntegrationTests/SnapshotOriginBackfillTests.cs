using System.Diagnostics;
using System.Text.Json;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    [NutritionPostgresFact]
    public async Task ClasificacionOrigin_SimulaEsIdempotenteYRevierteSinPisarEdiciones()
    {
        await using var db = Context(); var patient = await PatientAsync(db);
        var template = new ProgramTemplate { Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString("N"), Name = "Origen", Version = 1 };
        db.ProgramTemplates.Add(template);
        db.WeeklyDayTemplates.Add(new() { TemplateId = template.Id, Weekday = 1, TaskCode = TaskCode.podcast, Points = 10 });
        db.WeeklyDayTemplates.Add(new() { TemplateId = template.Id, WeekNumber = 2, Weekday = 2, TaskCode = TaskCode.podcast, Points = 25 });
        var enrollment = new ProgramEnrollment { Id = Guid.NewGuid(), PatientId = patient.Id, TemplateId = template.Id, StartLocalDate = new(2030, 1, 7) };
        db.ProgramEnrollments.Add(enrollment);
        JsonElement Old(int points = 10) => JsonSerializer.SerializeToElement(new[] { new { weekday = 1, task_code = "podcast", points, sort_order = 0 } });
        var future = new ProgramWeek { Id = Guid.NewGuid(), EnrollmentId = enrollment.Id, WeekNumber = 2,
            WeekStartDateLocal = new(2030, 1, 14), WeekEndDateLocal = new(2030, 1, 20), TemplateVersionAtStart = 1,
            TasksSnapshot = JsonSerializer.Deserialize<JsonElement>("""[{"weekday":1,"task_code":"podcast","points":10,"sort_order":0},{"weekday":2,"task_code":"podcast","points":25,"sort_order":0},{"weekday":3,"task_code":"nut","points":20,"sort_order":0},{"weekday":4,"task_code":"ejercicio","points":10,"sort_order":0},{"weekday":5,"task_code":"vitals","points":10,"sort_order":0,"origin":"individual"}]""") };
        var active = new ProgramWeek { Id = Guid.NewGuid(), EnrollmentId = enrollment.Id, WeekNumber = 1,
            WeekStartDateLocal = new(2030, 1, 7), WeekEndDateLocal = new(2030, 1, 13), Status = ProgramWeekStatus.Active, TasksSnapshot = Old() };
        var ambiguous = new ProgramWeek { Id = Guid.NewGuid(), EnrollmentId = enrollment.Id, WeekNumber = 3,
            WeekStartDateLocal = new(2030, 1, 21), WeekEndDateLocal = new(2030, 1, 27), TasksSnapshot = Old() };
        var historical = new ProgramWeek { Id = Guid.NewGuid(), EnrollmentId = enrollment.Id, WeekNumber = 4,
            WeekStartDateLocal = new(2020, 1, 6), WeekEndDateLocal = new(2020, 1, 12), TasksSnapshot = Old() };
        db.ProgramWeeks.AddRange(future, active, ambiguous, historical); await db.SaveChangesAsync();
        await db.Entry(future).ReloadAsync();
        var before = future.TasksSnapshot.GetRawText();
        var simulation = await OriginScriptAsync("simulate");
        Assert.Equal(0, simulation.GetProperty("escrituras").GetInt32());
        Assert.True(simulation.GetProperty("counts").GetProperty("items_program").GetInt32() >= 2);
        await db.Entry(future).ReloadAsync(); Assert.Equal(before, future.TasksSnapshot.GetRawText());
        var applied = await OriginScriptAsync("apply"); Assert.Equal(2, applied.GetProperty("escrituras").GetInt32());
        await db.Entry(future).ReloadAsync();
        Assert.Equal("program", future.TasksSnapshot[0].GetProperty("origin").GetString());
        Assert.Equal("program", future.TasksSnapshot[1].GetProperty("origin").GetString());
        Assert.Equal("individual", future.TasksSnapshot[2].GetProperty("origin").GetString());
        Assert.Equal(0, (await OriginScriptAsync("apply")).GetProperty("escrituras").GetInt32());
        Assert.Equal(0, (await OriginScriptAsync("simulate", true)).GetProperty("escrituras").GetInt32());
        Assert.Equal(2, (await OriginScriptAsync("reverse")).GetProperty("escrituras").GetInt32());
        await db.Entry(future).ReloadAsync(); Assert.Equal(before, future.TasksSnapshot.GetRawText());
        Assert.Equal(0, (await OriginScriptAsync("reverse")).GetProperty("escrituras").GetInt32());
        await OriginScriptAsync("apply");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE app.program_weeks SET tasks_snapshot=jsonb_set(tasks_snapshot,'{{0,points}}','99') WHERE id={future.Id}");
        var reversed = await OriginScriptAsync("reverse");
        Assert.Equal(1, reversed.GetProperty("counts").GetProperty("conflicto_editado_despues").GetInt32());
        await db.Entry(future).ReloadAsync(); Assert.Equal(99, future.TasksSnapshot[0].GetProperty("points").GetInt32());
        await db.Entry(active).ReloadAsync(); Assert.False(active.TasksSnapshot[0].TryGetProperty("origin", out _));
        await db.Entry(historical).ReloadAsync(); Assert.False(historical.TasksSnapshot[0].TryGetProperty("origin", out _));
    }

    private async Task<JsonElement> OriginScriptAsync(string mode, bool reverse = false)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "scripts", "classify_week_snapshot_origin.py"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var start = new ProcessStartInfo("python") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(Path.Combine(directory.FullName, "scripts", "classify_week_snapshot_origin.py"));
        start.ArgumentList.Add("--mode"); start.ArgumentList.Add(mode);
        start.ArgumentList.Add("--today"); start.ArgumentList.Add("2026-11-13");
        start.ArgumentList.Add("--batch-size"); start.ArgumentList.Add("1");
        if (reverse) start.ArgumentList.Add("--reverse");
        var builder = new NpgsqlConnectionStringBuilder(connection);
        start.Environment["PGHOST"] = builder.Host; start.Environment["PGPORT"] = builder.Port.ToString();
        start.Environment["PGDATABASE"] = builder.Database; start.Environment["PGUSER"] = builder.Username;
        start.Environment["PGPASSWORD"] = builder.Password;
        using var process = Process.Start(start)!;
        var output = await process.StandardOutput.ReadToEndAsync(); var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); Assert.True(process.ExitCode == 0, error);
        return JsonSerializer.Deserialize<JsonElement>(output);
    }
}
