using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    [NutritionPostgresFact]
    public async Task PlantillaPorSemana_UnicidadVolumenYReversaProtegida()
    {
        await using var db = Context();
        var template = new ProgramTemplate { Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString("N"), Name = "Migración" };
        db.ProgramTemplates.Add(template); await db.SaveChangesAsync();
        db.WeeklyDayTemplates.Add(new() { TemplateId = template.Id, Weekday = 1, TaskCode = TaskCode.podcast });
        db.WeeklyDayTemplates.Add(new() { TemplateId = template.Id, WeekNumber = 2, Weekday = 1, TaskCode = TaskCode.podcast });
        await db.SaveChangesAsync();
        foreach (var week in new int?[] { null, 2 })
        {
            db.WeeklyDayTemplates.Add(new() { TemplateId = template.Id, WeekNumber = week, Weekday = 1, TaskCode = TaskCode.podcast });
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
            db.ChangeTracker.Clear();
        }
        var migrator = db.GetService<IMigrator>();
        var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var current = migrations.Single(m => m.EndsWith("_AddWeeklyTemplateWeekNumber"));
        var previous = migrations[Array.IndexOf(migrations, current) - 1];
        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous));
        await db.Database.ExecuteSqlRawAsync("DELETE FROM app.weekly_day_templates WHERE week_number IS NOT NULL");
        await migrator.MigrateAsync(previous);
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM information_schema.columns WHERE table_schema='app' AND table_name='weekly_day_templates' AND column_name='week_number'").SingleAsync());
        // Volumen previo al Up: la ampliación y el índice se prueban con 10.000 filas.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO app.program_templates (id, code, name) SELECT gen_random_uuid(), 'volumen-' || n, 'Volumen' FROM generate_series(1,10000) n");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO app.weekly_day_templates (template_id,weekday,task_code,points) SELECT id,1,'podcast',0 FROM app.program_templates WHERE code LIKE 'volumen-%'");
        await migrator.MigrateAsync(current);
        Assert.Equal(10001, await db.WeeklyDayTemplates.CountAsync());
        Assert.True(await db.Database.SqlQueryRaw<bool>("SELECT indisvalid AS \"Value\" FROM pg_index WHERE indexrelid='app.uq_weekly_day_templates_template_week_day_task'::regclass").SingleAsync());
        await migrator.MigrateAsync(previous);
        await migrator.MigrateAsync(current);
        Assert.Equal(10001, await db.WeeklyDayTemplates.CountAsync());
    }
}
