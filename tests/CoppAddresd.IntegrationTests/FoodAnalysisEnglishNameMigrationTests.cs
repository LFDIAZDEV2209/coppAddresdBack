using CoppAddresd.Domain.Entities.FoodAi;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CoppAddresd.IntegrationTests;

/// <summary>Solo crea y elimina una base efímera; nunca migra la base de la conexión de entrada.</summary>
public sealed class FoodAnalysisEnglishNameMigrationTests : IAsyncLifetime
{
    private readonly string server = Environment.GetEnvironmentVariable("COP_TEST_DB_CONNECTION") ?? "";
    private string database = "";
    private string connection = "";
    private const string Migration = "20261106135712_AddFoodAnalysisEnglishNames";
    private AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);

    public async Task InitializeAsync()
    {
        if (server.Length == 0) return;
        database = $"copp_foodai_i18n_test_{Guid.NewGuid():N}";
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
    public async Task NullableNameEn_UpDownUp_PreservesLegacyIdentityPortionAndNutrition_AndFutureEnglish()
    {
        var analysis = Guid.NewGuid(); var item = Guid.NewGuid(); var bulk = Guid.NewGuid();
        await using var db = Context();
        var previous = db.Database.GetMigrations().TakeWhile(name => name != Migration).Last();
        await db.GetService<IMigrator>().MigrateAsync(previous);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO foodai.food_analyses (id,analysis_id,status,detector_version,segmenter_version,classifier_version,portion_method) VALUES ({analysis},{analysis},'completed','qa','qa','qa','qa')");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO foodai.food_analysis_items (id,analysis_id,item_index,name,detection_confidence,bbox_x,bbox_y,bbox_width,bbox_height,estimated_grams,calories) VALUES ({item},{analysis},0,'Arroz con pollo',0.9,0,0,10,10,180,270)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO foodai.food_analyses (id,analysis_id,status,detector_version,segmenter_version,classifier_version,portion_method) VALUES ({bulk},{bulk},'completed','qa','qa','qa','qa')");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO foodai.food_analysis_items (id,analysis_id,item_index,name,detection_confidence,bbox_x,bbox_y,bbox_width,bbox_height,estimated_grams,calories) SELECT gen_random_uuid(),{bulk},g,'legacy_catalog_item',0.9,0,0,10,10,100,123 FROM generate_series(0,1999) g");
        await db.GetService<IMigrator>().MigrateAsync(Migration);
        Assert.Equal(2000, await db.Database.SqlQueryRaw<int>("""SELECT COUNT(*)::int AS "Value" FROM foodai.food_analysis_items WHERE name='legacy_catalog_item' AND estimated_grams=100 AND calories=123 AND name_en IS NULL""").SingleAsync());
        var repository = new FoodAnalysisRepository(db);
        var legacy = Assert.Single((await repository.GetByAnalysisIdAsync(analysis))!.Items);
        Assert.Null(legacy.NameEn); Assert.Equal("Arroz con pollo", legacy.Name); Assert.Equal(180, legacy.EstimatedGrams); Assert.Equal(270, legacy.Calories);
        var future = new FoodAnalysis { AnalysisId = Guid.NewGuid(), Status = "completed", DetectorVersion = "qa", SegmenterVersion = "qa", ClassifierVersion = "qa", PortionMethod = "qa",
            Items = [new() { Name = "Arroz con pollo", NameEn = "Chicken and rice", ItemIndex = 0, EstimatedGrams = 250, Calories = 375 }] };
        await repository.AddAsync(future);
        db.ChangeTracker.Clear();
        var saved = Assert.Single((await repository.GetByAnalysisIdAsync(future.AnalysisId))!.Items);
        Assert.Equal("Chicken and rice", saved.NameEn); Assert.Equal("Arroz con pollo", saved.Name); Assert.Equal(250, saved.EstimatedGrams); Assert.Equal(375, saved.Calories);
        await db.GetService<IMigrator>().MigrateAsync(previous);
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM information_schema.columns WHERE table_schema='foodai' AND table_name='food_analysis_items' AND column_name='name_en'").SingleAsync());
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM foodai.food_analysis_items WHERE name='Arroz con pollo'").SingleAsync());
        await db.GetService<IMigrator>().MigrateAsync(Migration);
        var restored = Assert.Single((await repository.GetByAnalysisIdAsync(analysis))!.Items);
        Assert.Null(restored.NameEn); Assert.Equal("Arroz con pollo", restored.Name); Assert.Equal(180, restored.EstimatedGrams); Assert.Equal(270, restored.Calories);
    }
}
