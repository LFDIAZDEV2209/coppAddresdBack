using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Metrics;
using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CoppAddresd.Community.UnitTests;

/// <summary>
/// Base de datos de prueba aislada para el procesador de métricas del dashboard ERP.
/// Crea <c>coppaddresd_comm_metrics_test_&lt;guid&gt;</c>, aplica la cadena completa de
/// migraciones (schema <c>community.</c>) y la elimina al terminar. Requiere
/// <c>COP_TEST_DB_CONNECTION</c> apuntando a PostgreSQL (misma convención que los tests
/// de integración); si no está definida, la fixture queda no disponible y los tests se omiten.
/// </summary>
public sealed class CommunityMetricsProcessorTestDb : IAsyncLifetime
{
    private const string EnvVar = "COP_TEST_DB_CONNECTION";
    private string? _sourceConnection;
    private string _databaseName = null!;

    /// <summary>Cadena de conexión a la BD de prueba ya creada y migrada.</summary>
    public string ConnectionString { get; private set; } = null!;

    /// <summary>Verdadero si la BD está disponible (COP_TEST_DB_CONNECTION definida).</summary>
    public bool Available { get; private set; }

    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable(EnvVar);
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        _sourceConnection = source;
        Available = true;

        var builder = new NpgsqlConnectionStringBuilder(source);
        _databaseName = $"coppaddresd_comm_metrics_test_{Guid.NewGuid():N}";
        builder.Database = "postgres";

        await using (var admin = new NpgsqlConnection(builder.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            // TEMPLATE template0: el template1 de este contenedor tiene mismatch de
            // collation (XX000) que bloquea CREATE DATABASE desde él.
            create.CommandText = $"CREATE DATABASE \"{_databaseName}\" TEMPLATE template0";
            await create.ExecuteNonQueryAsync();
        }

        builder.Database = _databaseName;
        ConnectionString = builder.ConnectionString;

        var options = new DbContextOptionsBuilder<CommunityDbContext>()
            .UseNpgsql(ConnectionString, n =>
                n.MigrationsHistoryTable("__EFMigrationsHistory", "community"))
            .Options;

        await using var db = new CommunityDbContext(options);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (!Available || _sourceConnection is null)
        {
            return;
        }

        // Libera las conexiones en pool antes de intentar eliminar la BD.
        NpgsqlConnection.ClearAllPools();

        var builder = new NpgsqlConnectionStringBuilder(_sourceConnection);
        builder.Database = "postgres";

        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        await using var drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)";
        await drop.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// Fact condicionado a PostgreSQL real: si <c>COP_TEST_DB_CONNECTION</c> no está
/// definida, el test se reporta como SKIPPED en el runner (no pasa en verde
/// silenciosamente). Mismo patrón que ProgramRepositoryTests del backend.
/// </summary>
public sealed class RequiresPostgresFactAttribute : FactAttribute
{
    public RequiresPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COP_TEST_DB_CONNECTION")))
        {
            Skip = "COP_TEST_DB_CONNECTION no definida: requiere PostgreSQL real, test omitido.";
        }
    }
}

/// <summary>
/// Scenarios del procesador de métricas del dashboard ERP (Dashboard #5). Los upserts
/// son SQL PostgreSQL específico (ON CONFLICT, NOW(), GREATEST, schema community.),
/// por lo que NO se testean con InMemory: se ejercita el pipeline completo
/// (cola Channel + HostedService + rollup real) contra una BD aislada.
/// </summary>
public sealed class CommunityMetricsProcessorTests : IClassFixture<CommunityMetricsProcessorTestDb>
{
    private readonly CommunityMetricsProcessorTestDb _db;

    public CommunityMetricsProcessorTests(CommunityMetricsProcessorTestDb db) => _db = db;

    [RequiresPostgresFact]
    public async Task PostCreated_Enqueues_IncrementsTotalAndType()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        await RunProcessorAsync(async queue =>
        {
            await queue.EnqueueAsync(new PostCreatedMetricEvent(Guid.NewGuid(), date, "video", 14));
        });

        var rows = await WaitForMetricsAsync(date,
            r => GetMetric(r, "posts_count", "total") == 1 && GetMetric(r, "posts_count", "video") == 1);

        Assert.Equal(1, GetMetric(rows, "posts_count", "total"));
        Assert.Equal(1, GetMetric(rows, "posts_count", "video"));
    }

    [RequiresPostgresFact]
    public async Task PostCreated_TwiceSameDay_Accumulates()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));

        await RunProcessorAsync(async queue =>
        {
            await queue.EnqueueAsync(new PostCreatedMetricEvent(Guid.NewGuid(), date, "video", 10));
            await queue.EnqueueAsync(new PostCreatedMetricEvent(Guid.NewGuid(), date, "video", 15));
        });

        var rows = await WaitForMetricsAsync(date,
            r => GetMetric(r, "posts_count", "total") == 2 && GetMetric(r, "posts_count", "video") == 2);

        Assert.Equal(2, GetMetric(rows, "posts_count", "total"));
        Assert.Equal(2, GetMetric(rows, "posts_count", "video"));
    }

    [RequiresPostgresFact]
    public async Task LikeAdded_ThenRemoved_IsZero()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3));

        // Fase 1: el like agregado deja likes_count/total en 1.
        await RunProcessorAsync(async queue =>
        {
            await queue.EnqueueAsync(new LikeAddedMetricEvent(Guid.NewGuid(), date, 12));
        });
        var afterAdd = await WaitForMetricsAsync(date, r => GetMetric(r, "likes_count", "total") == 1);
        Assert.Equal(1, GetMetric(afterAdd, "likes_count", "total"));

        // Fase 2: el like removido decrementa a 0 (GREATEST impide negativos).
        await RunProcessorAsync(async queue =>
        {
            await queue.EnqueueAsync(new LikeRemovedMetricEvent(Guid.NewGuid(), date));
        });
        var afterRemove = await WaitForMetricsAsync(date,
            r => r.Any(x => x.MetricKey == "likes_count" && x.DimensionKey == "total" && x.TotalCount == 0));

        Assert.Equal(0, GetMetric(afterRemove, "likes_count", "total"));
    }

    [RequiresPostgresFact]
    public async Task HourlyActivity_GetsUpdated()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-4));

        await RunProcessorAsync(async queue =>
        {
            await queue.EnqueueAsync(new CommentCreatedMetricEvent(Guid.NewGuid(), date, 14));
        });

        var rows = await WaitForMetricsAsync(date, r => GetMetric(r, "hourly_activity", "14") == 1);

        Assert.Equal(1, GetMetric(rows, "hourly_activity", "14"));
    }

    [RequiresPostgresFact]
    public async Task CommentCreated_IncrementsSeparateKey()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5));

        await RunProcessorAsync(async queue =>
        {
            await queue.EnqueueAsync(new CommentCreatedMetricEvent(Guid.NewGuid(), date, 9));
        });

        // comments_count/total = 1 y ninguna fila de posts_count (no mezcla claves).
        var rows = await WaitForMetricsAsync(date, r =>
            GetMetric(r, "comments_count", "total") == 1 && r.All(x => x.MetricKey != "posts_count"));

        Assert.Equal(1, GetMetric(rows, "comments_count", "total"));
        Assert.DoesNotContain(rows, x => x.MetricKey == "posts_count");
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Ejercita el pipeline completo: cola + HostedService reales contra la BD de
    /// prueba. Encola los eventos provistos y espera a que el procesador los drene.
    /// </summary>
    private async Task RunProcessorAsync(Func<ICommunityMetricsQueue, Task> enqueue)
    {
        var queue = new CommunityMetricsQueue();

        var services = new ServiceCollection();
        services.AddDbContext<CommunityDbContext>(o =>
            o.UseNpgsql(_db.ConnectionString, n =>
                n.MigrationsHistoryTable("__EFMigrationsHistory", "community")));
        await using var provider = services.BuildServiceProvider();

        var processor = new CommunityMetricsProcessorHostedService(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CommunityMetricsProcessorHostedService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await processor.StartAsync(cts.Token);
        try
        {
            await enqueue(queue);
            // El procesador drena la cola en background; los tests esperan el
            // estado esperado en el rollup mediante WaitForMetricsAsync.
        }
        finally
        {
            await processor.StopAsync(CancellationToken.None);
            cts.Cancel();
        }
    }

    /// <summary>
    /// Lee filas del rollup para una fecha hasta que <paramref name="predicate"/>
    /// se cumpla o expire el timeout. Devuelve la última lectura observada.
    /// </summary>
    private async Task<IReadOnlyList<CommunityDailyMetric>> WaitForMetricsAsync(
        DateOnly date, Func<IReadOnlyList<CommunityDailyMetric>, bool> predicate, int timeoutMs = 15000)
    {
        var options = new DbContextOptionsBuilder<CommunityDbContext>()
            .UseNpgsql(_db.ConnectionString, n =>
                n.MigrationsHistoryTable("__EFMigrationsHistory", "community"))
            .Options;

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        IReadOnlyList<CommunityDailyMetric> last = [];

        while (true)
        {
            await using var db = new CommunityDbContext(options);
            last = await db.CommunityDailyMetrics.AsNoTracking()
                .Where(x => x.MetricDate == date)
                .OrderBy(x => x.MetricKey)
                .ThenBy(x => x.DimensionKey)
                .ToListAsync();

            if (predicate(last) || DateTime.UtcNow >= deadline)
            {
                return last;
            }

            await Task.Delay(100);
        }
    }

    private static long? GetMetric(IReadOnlyList<CommunityDailyMetric> rows, string key, string dimension)
        => rows.FirstOrDefault(x => x.MetricKey == key && x.DimensionKey == dimension)?.TotalCount;
}