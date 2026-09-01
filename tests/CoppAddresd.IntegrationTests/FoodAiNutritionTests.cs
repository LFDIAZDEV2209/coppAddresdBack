using CoppAddresd.Api.Seeders;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CoppAddresd.IntegrationTests;

/// <summary>
/// Los tests que escriben en la BD comparten Npgsql; la paralelización global
/// de xUnit con otros tests produce corrupción de conexión (Index out of
/// range en NpgsqlDataReader). Esta collection fuerza ejecución secuencial.
/// </summary>
[CollectionDefinition("foodai-nutrition", DisableParallelization = true)]
public sealed class FoodAiNutritionCollection
{
}

[Collection("foodai-nutrition")]
public sealed class FoodAiNutritionTests : IAsyncLifetime
{
    private const string EnvVar = "COP_TEST_DB_CONNECTION";

    private readonly string _connectionString = Environment.GetEnvironmentVariable(EnvVar)! + ";Pooling=false";
    private readonly string _testAlias = $"test_food_{Guid.NewGuid():N}"[..30];
    private readonly string _testFoodName = $"Test Food {Guid.NewGuid():N}";
    private bool _skipped;

    public Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _skipped = true;
        }

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_skipped)
        {
            return;
        }

        // Limpia solo el alimento de prueba (no toca datos sembrados).
        await using var db = BuildDb();
        await db.Foods.Where(f => f.Name == _testFoodName).ExecuteDeleteAsync();
    }

    private Task ExecuteAsync(Func<NpgsqlCommand, CancellationToken, Task> action)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        return action(cmd, CancellationToken.None);
    }

    private AppDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Provider_resuelve_por_alias_y_devuelve_nutricion_por_100g()
    {
        if (_skipped) return;
        await using var db = BuildDb();
        var foodId = Guid.NewGuid();
        db.Foods.Add(new CoppAddresd.Domain.Entities.FoodAi.Food
        {
            Id = foodId,
            Name = _testFoodName,
            DisplayName = "Test Food",
            Category = "fruit",
            NutritionEntries =
            {
                new CoppAddresd.Domain.Entities.FoodAi.FoodNutrition
                {
                    Id = Guid.NewGuid(),
                    FoodId = foodId,
                    ServingGrams = 100m,
                    Calories = 89m,
                    Protein = 1.09m,
                    Carbohydrates = 22.84m,
                    Fat = 0.33m,
                    Fiber = 2.6m,
                    Sugar = 12.23m,
                    Sodium = 1m,
                    Source = "USDA FoodData Central",
                    SourceVersion = "2026-08-27",
                },
            },
            Aliases =
            {
                new CoppAddresd.Domain.Entities.FoodAi.FoodAlias { Id = Guid.NewGuid(), FoodId = foodId, Alias = _testAlias, Source = "yolo" },
            },
        });
        await db.SaveChangesAsync();
        INutritionProvider provider = new DatabaseNutritionProvider(db);

        var result = await provider.GetNutritionAsync(_testAlias);

        Assert.NotNull(result);
        Assert.Equal(100m, result.ServingGrams);
        Assert.Equal(89m, result.Calories);
        Assert.Equal(22.84m, result.Carbohydrates);
        Assert.Equal("USDA FoodData Central", result.Source);
    }

    [Fact]
    public async Task Provider_alimento_inexistente_devuelve_null()
    {
        if (_skipped) return;
        await using var db = BuildDb();
        INutritionProvider provider = new DatabaseNutritionProvider(db);

        var result = await provider.GetNutritionAsync("alimento_que_no_existe_xyz");

        Assert.Null(result);
    }

    [Fact]
    public async Task Seed_es_reproducible_y_no_duplica()
    {
        if (_skipped) return;

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_connectionString));
        await using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var seeder = new FoodAiNutritionSeeder(
            scopeFactory,
            NullLogger<FoodAiNutritionSeeder>.Instance);

        await seeder.StartAsync(CancellationToken.None);
        await seeder.StartAsync(CancellationToken.None); // segunda corrida

        await using var db = BuildDb();
        var bananaCount = await db.FoodAliases.CountAsync(a => a.Alias == "banana");
        Assert.Equal(1, bananaCount);

        INutritionProvider nutritionProvider = new DatabaseNutritionProvider(db);
        var banana = await nutritionProvider.GetNutritionAsync("banana");
        Assert.NotNull(banana);
        Assert.Equal(89m, banana.Calories);
    }
}