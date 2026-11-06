using CoppAddresd.Application.Features.Nutrition;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CoppAddresd.IntegrationTests;

/// <summary>Prueba la migración aditiva exclusivamente en una base efímera propia.</summary>
public sealed class NutritionInstructionProvenanceMigrationTests : IAsyncLifetime
{
    private readonly string server = Environment.GetEnvironmentVariable("COP_TEST_DB_CONNECTION") ?? "";
    private string database = "";
    private string connection = "";
    private const string Migration = "20261106142836_AddNutritionInstructionTranslationProvenance";
    private AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);

    public async Task InitializeAsync()
    {
        if (server.Length == 0) return;
        database = $"copp_nutrition_i18n_test_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(server) { Database = "postgres" }.ConnectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\" TEMPLATE template0", admin)) await command.ExecuteNonQueryAsync();
        connection = new NpgsqlConnectionStringBuilder(server) { Database = database }.ConnectionString;
        await using var db = Context();
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA auth; CREATE TABLE auth.\"Users\" (\"Id\" uuid PRIMARY KEY, \"SecurityStamp\" text NULL);");
    }

    public async Task DisposeAsync()
    {
        if (database.Length == 0) return;
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(server) { Database = "postgres" }.ConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    [NutritionPostgresFact]
    public async Task NullableProvenance_UpDownUp_DoesNotBlessHistoricalEnglishOrModifyClinicalSource()
    {
        await using var db = Context();
        var previous = db.Database.GetMigrations().TakeWhile(name => name != Migration).Last();
        await db.GetService<IMigrator>().MigrateAsync(previous);
        var patient = new PatientProfile { FirstName = "Paciente", LastName = "Prueba", Status = "Activo", UserId = Guid.NewGuid() };
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.\"Users\" (\"Id\") VALUES ({patient.UserId.Value})");
        db.PatientProfiles.Add(patient);
        await db.SaveChangesAsync();
        var plan = Guid.NewGuid(); var meal = Guid.NewGuid(); var item = Guid.NewGuid(); var actor = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO app.nutrition_prescriptions
            (id,patient_id,cycle_id,version,revision,state,provisional,is_published,menus_outdated,
             start_date,end_date,calorie_target,protein_target,water_ml,instructions,instructions_en,created_by,created_at,approved_by,approved_at)
            VALUES ({plan},{patient.Id},{Guid.NewGuid()},1,7,'aprobado',false,true,false,
             DATE '2026-11-05',DATE '2026-12-04',1500,80,2250,'Máximo 1 g de sal.','Maximum 2 g of salt.',{actor},{DateTime.UtcNow},{actor},{DateTime.UtcNow})
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO app.nutrition_prescription_meals (id,prescription_id,day_number,meal_type,scheduled_time,menu_name,notes,water_ml) VALUES ({meal},{plan},1,'des',TIME '07:00','Menú original','Nota clínica original',300)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO app.nutrition_prescription_items (id,meal_id,food_item_id,grams,sort_order,notes) VALUES ({item},{meal},'avena',45.5,1,'Conservar porción')");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO app.nutrition_prescriptions
            (id,patient_id,cycle_id,version,revision,state,provisional,is_published,menus_outdated,instructions,instructions_en,created_by,created_at)
            SELECT gen_random_uuid(),{patient.Id},gen_random_uuid(),1,1,'borrador_ia',false,false,false,
             'Fuente histórica','Historical English',{actor},{DateTime.UtcNow} FROM generate_series(1,1000)
            """);
        var original = await SourceState(db, plan);

        await db.GetService<IMigrator>().MigrateAsync(Migration);
        Assert.Equal(original, await SourceState(db, plan));
        Assert.Equal(1001, await db.NutritionPrescriptions.CountAsync(p => p.InstructionsEnSourceHash == null));
        var legacy = await db.NutritionPrescriptions.AsNoTracking().SingleAsync(p => p.Id == plan);
        Assert.Null(legacy.InstructionsEnSourceHash);
        Assert.Null(NutritionInstructionTranslation.VerifiedEnglish(legacy));
        Assert.Equal("Maximum 2 g of salt.", legacy.InstructionsEn);

        var future = new NutritionPrescription
        {
            PatientId = patient.Id, CreatedBy = actor, Instructions = "Máximo 1 g de sal.", InstructionsEn = "Maximum 1 g of salt.",
            CalorieTarget = 1700, ProteinTarget = 90, WaterMl = 2500,
        };
        future.InstructionsEnSourceHash = NutritionInstructionTranslation.PairedSourceHash(future.Instructions, future.InstructionsEn);
        db.NutritionPrescriptions.Add(future);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var saved = await db.NutritionPrescriptions.AsNoTracking().SingleAsync(p => p.Id == future.Id);
        Assert.Equal(future.InstructionsEnSourceHash, saved.InstructionsEnSourceHash);
        Assert.Equal("Maximum 1 g of salt.", NutritionInstructionTranslation.VerifiedEnglish(saved));

        await db.GetService<IMigrator>().MigrateAsync(previous);
        Assert.Equal(original, await SourceState(db, plan));
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("""SELECT COUNT(*)::int AS "Value" FROM information_schema.columns WHERE table_schema='app' AND table_name='nutrition_prescriptions' AND column_name='instructions_en_source_hash'""").SingleAsync());
        await db.GetService<IMigrator>().MigrateAsync(Migration);
        Assert.Equal(original, await SourceState(db, plan));
        Assert.Equal(1002, await db.NutritionPrescriptions.CountAsync(p => p.InstructionsEnSourceHash == null));
        saved = await db.NutritionPrescriptions.AsNoTracking().SingleAsync(p => p.Id == future.Id);
        Assert.Equal("Máximo 1 g de sal.", saved.Instructions);
        Assert.Equal("Maximum 1 g of salt.", saved.InstructionsEn);
        Assert.Equal(1700, saved.CalorieTarget);
        Assert.Equal(90, saved.ProteinTarget);
        Assert.Equal(2500, saved.WaterMl);
        Assert.Null(NutritionInstructionTranslation.VerifiedEnglish(saved));
    }

    private static Task<string> SourceState(AppDbContext db, Guid plan) => db.Database.SqlQuery<string>($"""
        SELECT jsonb_build_object('plan',to_jsonb(p)-'instructions_en_source_hash',
          'meals',(SELECT jsonb_agg(m ORDER BY m.id) FROM app.nutrition_prescription_meals m WHERE m.prescription_id=p.id),
          'items',(SELECT jsonb_agg(i ORDER BY i.id) FROM app.nutrition_prescription_items i
            JOIN app.nutrition_prescription_meals m ON i.meal_id=m.id WHERE m.prescription_id=p.id))::text AS "Value"
        FROM app.nutrition_prescriptions p WHERE p.id={plan}
        """).SingleAsync();
}
