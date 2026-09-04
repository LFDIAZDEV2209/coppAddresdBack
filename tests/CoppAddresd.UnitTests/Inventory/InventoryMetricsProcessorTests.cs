using CoppAddresd.Application.Features.Inventory;
using CoppAddresd.Application.Features.Inventory.Events;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Metrics;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CoppAddresd.UnitTests.Inventory;

/// <summary>
/// BD aislada para los tests del procesador de métricas y del fast-path del
/// dashboard de Inventario (Dashboard #6): crea
/// <c>coppaddresd_inv_test_&lt;timestamp&gt;</c>, aplica el esquema <c>auth</c>
/// mínimo que referencian las FKs por SQL de las migraciones y ejecuta la
/// cadena completa de migraciones (schema <c>erp</c> incluido). Requiere
/// <c>COP_TEST_DB_CONNECTION</c>; si no está definida, los tests se omiten.
/// </summary>
public sealed class InventoryMetricsProcessorTestDb : IAsyncLifetime
{
    private const string EnvVar = "COP_TEST_DB_CONNECTION";

    private readonly string _baseConnectionString = Environment.GetEnvironmentVariable(EnvVar)!;
    private readonly string _testDbName =
        $"coppaddresd_inv_test_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..44];

    public bool Skipped { get; private set; }

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_baseConnectionString))
        {
            Skipped = true;
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(_baseConnectionString)
        {
            Database = _testDbName,
        };
        ConnectionString = builder.ConnectionString;

        // Clústeres CI frescos: el rol app_user (convención del repo, al que
        // las migraciones hacen GRANT) es cluster-wide y puede no existir.
        await ExecuteOnBaseAsync(
            async (cmd, ct) =>
            {
                cmd.CommandText = """
                    DO $$
                    BEGIN
                        IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'app_user') THEN
                            CREATE ROLE app_user;
                        END IF;
                    END
                    $$;
                    """;
                await cmd.ExecuteNonQueryAsync(ct);
            }
        );

        var locale = await DetectAvailableLocaleAsync();

        // Recrea la BD desde cero (reruns seguros) y aplica las migraciones.
        await ExecuteOnBaseAsync(
            async (cmd, ct) =>
            {
                cmd.CommandText = $"""
                    DROP DATABASE IF EXISTS "{_testDbName}" WITH (FORCE);
                    CREATE DATABASE "{_testDbName}"
                        TEMPLATE template0
                        ENCODING 'UTF8'
                        LC_COLLATE '{locale}'
                        LC_CTYPE '{locale}';
                    """;
                await cmd.ExecuteNonQueryAsync(ct);
            }
        );

        // Esquema auth mínimo: las migraciones del backend crean FKs por SQL
        // hacia auth."Users"("Id") (gestionado por el Auth Service en prod).
        await ExecuteOnTestAsync(
            async (cmd, ct) =>
            {
                cmd.CommandText = """
                    CREATE SCHEMA IF NOT EXISTS auth;
                    CREATE TABLE IF NOT EXISTS auth."Users" ("Id" uuid NOT NULL PRIMARY KEY);
                    """;
                await cmd.ExecuteNonQueryAsync(ct);
            }
        );

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (Skipped)
        {
            return;
        }

        try
        {
            // Libera las conexiones en pool antes de intentar eliminar la BD.
            NpgsqlConnection.ClearAllPools();
            await ExecuteOnBaseAsync(
                async (cmd, ct) =>
                {
                    cmd.CommandText = $"DROP DATABASE IF EXISTS \"{_testDbName}\" WITH (FORCE);";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
            );
        }
        catch
        {
            // No op: la limpieza no debe tumbar la suite.
        }
    }

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    private Task ExecuteOnBaseAsync(Func<NpgsqlCommand, CancellationToken, Task> action) =>
        ExecuteAsync(_baseConnectionString, action);

    private Task ExecuteOnTestAsync(Func<NpgsqlCommand, CancellationToken, Task> action) =>
        ExecuteAsync(ConnectionString, action);

    /// <summary>
    /// Detecta un locale usable para el <c>CREATE DATABASE</c>: prefiere
    /// <c>en_US.utf8</c> (dev Linux) si existe en <c>pg_collation</c>; si no,
    /// usa el collate de la BD <c>postgres</c> del clúster (Windows/Linux
    /// mínimo). Fallback final <c>C</c>. El valor proviene del propio servidor
    /// (catálogo), no de entrada de usuario.
    /// </summary>
    private async Task<string> DetectAvailableLocaleAsync()
    {
        string? detected = null;
        await ExecuteOnBaseAsync(
            async (cmd, ct) =>
            {
                cmd.CommandText = """
                    SELECT CASE
                        WHEN EXISTS (SELECT 1 FROM pg_collation
                                     WHERE collname IN ('en_US.utf8', 'en_US.UTF-8'))
                        THEN 'en_US.utf8'
                        ELSE (SELECT datcollate FROM pg_database WHERE datname = 'postgres' LIMIT 1)
                    END;
                    """;
                detected = await cmd.ExecuteScalarAsync(ct) as string;
            }
        );

        return string.IsNullOrWhiteSpace(detected) ? "C" : detected;
    }

    private static async Task ExecuteAsync(
        string connectionString,
        Func<NpgsqlCommand, CancellationToken, Task> action
    )
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        await action(command, CancellationToken.None);
    }
}

/// <summary>
/// Fact condicionado a PostgreSQL real: si <c>COP_TEST_DB_CONNECTION</c> no
/// está definida, el test se reporta como SKIPPED en el runner (no pasa en
/// verde silenciosamente). Mismo patrón que ProgramRepositoryTests.
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
/// Escenarios del procesador de métricas y del fast-path de GetAnalyticsAsync
/// (Dashboard #6: Inventario &amp; Farmacia, Fase 1 CQRS). Los upserts son SQL
/// PostgreSQL específico (ON CONFLICT, NOW(), schema erp.), por lo que NO se
/// testean con InMemory: se ejercita el pipeline completo (cola Channel +
/// HostedService + rollup real) contra una BD aislada.
/// </summary>
public sealed class InventoryMetricsProcessorTests : IClassFixture<InventoryMetricsProcessorTestDb>
{
    private readonly InventoryMetricsProcessorTestDb _db;

    public InventoryMetricsProcessorTests(InventoryMetricsProcessorTestDb db) => _db = db;

    // ------------------------------------------------------------ Escenario 1

    [RequiresPostgresFact]
    public async Task EntryCreated_UpdatesEntriesCount()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        await RunProcessorAsync(async queue =>
        {
            await queue.EnqueueAsync(new InventoryEntryCreatedMetricEvent(
                Guid.NewGuid(), date, 250.00m,
                [new InventoryMetricLine("Producto A", "Suplementos", 2, 125.00m)]));
        });

        var rows = await WaitForMetricsAsync(date,
            r => GetMetric(r, "entries_count", "total") == 1);

        Assert.Equal(1, GetMetric(rows, "entries_count", "total"));
    }

    // ------------------------------------------------------------ Escenario 2

    [RequiresPostgresFact]
    public async Task EntryCreated_UpdatesUnitsEntered()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));

        await RunProcessorAsync(async queue =>
        {
            await queue.EnqueueAsync(new InventoryEntryCreatedMetricEvent(
                Guid.NewGuid(), date, 50.00m,
                [
                    new InventoryMetricLine("Producto A", "Suplementos", 3, 10.00m),
                    new InventoryMetricLine("Producto B", "Medicamentos", 2, 10.00m),
                ]));
        });

        var rows = await WaitForMetricsAsync(date,
            r => GetMetric(r, "units_entered", "total") == 5);

        Assert.Equal(5, GetMetric(rows, "units_entered", "total"));
    }

    // ------------------------------------------------------------ Escenario 3

    [RequiresPostgresFact]
    public async Task EntryCreated_UpdatesCostEntered()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3));

        await RunProcessorAsync(async queue =>
        {
            await queue.EnqueueAsync(new InventoryEntryCreatedMetricEvent(
                Guid.NewGuid(), date, 100.50m,
                [new InventoryMetricLine("Producto A", "Suplementos", 1, 100.50m)]));
        });

        // 100.50 → 10050 centavos (convención money-as-cents del rollup).
        var rows = await WaitForMetricsAsync(date,
            r => GetMetric(r, "cost_entered", "total") == 10050);

        Assert.Equal(10050, GetMetric(rows, "cost_entered", "total"));
    }

    // ------------------------------------------------------------ Escenario 4

    [RequiresPostgresFact]
    public async Task EntryCreated_UpdatesPerProductAndCategory()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-4));

        await RunProcessorAsync(async queue =>
        {
            await queue.EnqueueAsync(new InventoryEntryCreatedMetricEvent(
                Guid.NewGuid(), date, 41.00m,
                [
                    new InventoryMetricLine("Producto A", "Suplementos", 3, 10.00m),
                    new InventoryMetricLine("Producto B", "Medicamentos", 2, 5.50m),
                ]));
        });

        var rows = await WaitForMetricsAsync(date,
            r => GetMetric(r, "product_entries", "Producto A") == 3
                && GetMetric(r, "category_units", "Suplementos") == 3
                && GetMetric(r, "category_cost", "Suplementos") == 3000);

        Assert.Equal(3, GetMetric(rows, "product_entries", "Producto A"));
        Assert.Equal(3, GetMetric(rows, "category_units", "Suplementos"));
        Assert.Equal(3000, GetMetric(rows, "category_cost", "Suplementos"));
        Assert.Equal(2, GetMetric(rows, "product_entries", "Producto B"));
        Assert.Equal(2, GetMetric(rows, "category_units", "Medicamentos"));
    }

    // ------------------------------------------------------------ Escenario 5

    [RequiresPostgresFact]
    public async Task ExitCreated_UpdatesExitsCount()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5));

        await RunProcessorAsync(async queue =>
        {
            await queue.EnqueueAsync(new InventoryExitCreatedMetricEvent(
                Guid.NewGuid(), date,
                [
                    new InventoryMetricLine("Producto A", "Suplementos", 4, 12.00m),
                    new InventoryMetricLine("Producto B", "Medicamentos", 1, 8.00m),
                ]));
        });

        var rows = await WaitForMetricsAsync(date,
            r => GetMetric(r, "exits_count", "total") == 1
                && GetMetric(r, "units_exited", "total") == 5
                && GetMetric(r, "cost_exited", "total") == 5600);

        Assert.Equal(1, GetMetric(rows, "exits_count", "total"));
        Assert.Equal(5, GetMetric(rows, "units_exited", "total"));
        Assert.Equal(5600, GetMetric(rows, "cost_exited", "total")); // 4*12 + 1*8 = 56 → 5600 centavos
    }

    // ------------------------------------------------------------ Escenario 6

    [RequiresPostgresFact]
    public async Task TwoEntriesSameDay_Accumulates()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-6));

        await RunProcessorAsync(async queue =>
        {
            await queue.EnqueueAsync(new InventoryEntryCreatedMetricEvent(
                Guid.NewGuid(), date, 30.00m,
                [new InventoryMetricLine("Producto A", "Suplementos", 3, 10.00m)]));
            await queue.EnqueueAsync(new InventoryEntryCreatedMetricEvent(
                Guid.NewGuid(), date, 45.00m,
                [new InventoryMetricLine("Producto A", "Suplementos", 2, 22.50m)]));
        });

        // Acumula (no reemplaza): 2 entradas, 5 unidades, 75.00 → 7500 centavos.
        var rows = await WaitForMetricsAsync(date,
            r => GetMetric(r, "entries_count", "total") == 2
                && GetMetric(r, "units_entered", "total") == 5
                && GetMetric(r, "cost_entered", "total") == 7500
                && GetMetric(r, "product_entries", "Producto A") == 5);

        Assert.Equal(2, GetMetric(rows, "entries_count", "total"));
        Assert.Equal(5, GetMetric(rows, "units_entered", "total"));
        Assert.Equal(7500, GetMetric(rows, "cost_entered", "total"));
        Assert.Equal(5, GetMetric(rows, "product_entries", "Producto A"));
    }

    // ------------------------------------------------------------ Escenario 7

    [RequiresPostgresFact]
    public async Task GetAnalyticsAsync_WithRollupData_UsesFastPath()
    {
        // Rollup sembrado vía el pipeline real (entrada de 5 unidades).
        var rollupDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        await RunProcessorAsync(async queue =>
        {
            await queue.EnqueueAsync(new InventoryEntryCreatedMetricEvent(
                Guid.NewGuid(), rollupDate, 75.00m,
                [
                    new InventoryMetricLine("Producto A", "Suplementos", 3, 10.00m),
                    new InventoryMetricLine("Producto B", "Medicamentos", 2, 22.50m),
                ]));
        });
        await WaitForMetricsAsync(rollupDate,
            r => GetMetric(r, "units_entered", "total") == 5);

        // OLTP sembrado con valores DIVERGENTES (99 unidades): si el fast-path
        // no se usara y cayera al fallback OLTP, el DTO reflejaría 99.
        await using (var seed = _db.CreateDbContext())
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Sku = "FAST-01",
                Name = "Producto A",
                ProductType = "Medicamento",
                Category = "Suplementos",
                Presentation = "Frasco",
                Unit = "unidad",
                Status = "Activo",
                UnitCost = 10.00m,
                Stock = 100,
                MinimumStock = 5,
                MaximumStock = 200,
            };
            seed.Products.Add(product);
            seed.InventoryEntries.Add(new InventoryEntry
            {
                Id = Guid.NewGuid(),
                Reference = "ENT-FAST-1",
                Date = rollupDate.ToDateTime(TimeOnly.MinValue),
                Reason = "Compra",
                Responsible = "Test",
                TotalCost = 990.00m,
                CreatedAt = DateTime.UtcNow,
                Lines =
                [
                    new InventoryEntryLine
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Quantity = 99,
                        UnitCost = 10.00m,
                    },
                ],
            });
            await seed.SaveChangesAsync();
        }

        var repo = new InventoryRepository(_db.CreateDbContext());
        var dto = await repo.GetAnalyticsAsync(rollupDate, rollupDate);

        // Valores del rollup, NO del OLTP (99): prueba que el fast-path se usó.
        Assert.Equal(1, dto.Entries);
        Assert.Equal(5, dto.UnitsEntered);
        Assert.Equal(3, dto.TopMoving.Single(t => t.Name == "Producto A").Quantity);
        Assert.Equal(100, dto.Products.Single(p => p.Sku == "FAST-01").Stock);
    }

    // ------------------------------------------------------------ Escenario 8

    [RequiresPostgresFact]
    public async Task GetAnalyticsAsync_WithoutRollupData_FallsBackToOltp()
    {
        // Sin filas de rollup: la tabla queda vacía (BD aislada por escenario de fechas).
        var oltpDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-8));

        await using (var seed = _db.CreateDbContext())
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Sku = "FALL-01",
                Name = "Producto A",
                ProductType = "Medicamento",
                Category = "Suplementos",
                Presentation = "Frasco",
                Unit = "unidad",
                Status = "Activo",
                UnitCost = 10.00m,
                Stock = 50,
                MinimumStock = 5,
                MaximumStock = 200,
            };
            seed.Products.Add(product);
            seed.InventoryEntries.Add(new InventoryEntry
            {
                Id = Guid.NewGuid(),
                Reference = "ENT-FALL-1",
                Date = oltpDate.ToDateTime(TimeOnly.MinValue),
                Reason = "Compra",
                Responsible = "Test",
                TotalCost = 250.00m,
                CreatedAt = DateTime.UtcNow,
                Lines =
                [
                    new InventoryEntryLine
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Quantity = 25,
                        UnitCost = 10.00m,
                    },
                ],
            });
            seed.InventoryExits.Add(new InventoryExit
            {
                Id = Guid.NewGuid(),
                Reference = "SAL-FALL-1",
                Date = oltpDate.ToDateTime(TimeOnly.MinValue),
                Reason = "Dispensación",
                Responsible = "Test",
                CreatedAt = DateTime.UtcNow,
                Lines =
                [
                    new InventoryExitLine
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Quantity = 5,
                        UnitCost = 10.00m,
                    },
                ],
            });
            await seed.SaveChangesAsync();
        }

        // Sanity: la tabla rollup NO tiene filas para esta fecha.
        await using (var check = _db.CreateDbContext())
        {
            Assert.Equal(0, await check.InventoryDailyMetrics.CountAsync(m => m.MetricDate == oltpDate));
        }

        var repo = new InventoryRepository(_db.CreateDbContext());
        var dto = await repo.GetAnalyticsAsync(oltpDate, oltpDate);

        // Valores calculados desde OLTP (código original en memoria).
        Assert.Equal(1, dto.Entries);
        Assert.Equal(1, dto.Exits);
        Assert.Equal(25, dto.UnitsEntered);
        Assert.Equal(5, dto.UnitsExited);
        Assert.Equal(30, dto.TopMoving.Single(t => t.Name == "Producto A").Quantity);
        Assert.Equal(50, dto.Products.Single(p => p.Sku == "FALL-01").Stock);
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Ejercita el pipeline completo: cola + HostedService reales contra la BD
    /// de prueba. Encola los eventos provistos y espera a que el procesador
    /// los drene (los tests esperan el estado esperado en el rollup).
    /// </summary>
    private async Task RunProcessorAsync(Func<IInventoryMetricsQueue, Task> enqueue)
    {
        var queue = new InventoryMetricsQueue();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_db.ConnectionString));
        await using var provider = services.BuildServiceProvider();

        var processor = new InventoryMetricsProcessorHostedService(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<InventoryMetricsProcessorHostedService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await processor.StartAsync(cts.Token);
        try
        {
            await enqueue(queue);
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
    private async Task<IReadOnlyList<InventoryDailyMetric>> WaitForMetricsAsync(
        DateOnly date, Func<IReadOnlyList<InventoryDailyMetric>, bool> predicate, int timeoutMs = 15000)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_db.ConnectionString)
            .Options;

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        IReadOnlyList<InventoryDailyMetric> last = [];

        while (true)
        {
            await using var db = new AppDbContext(options);
            last = await db.InventoryDailyMetrics.AsNoTracking()
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

    private static long? GetMetric(IReadOnlyList<InventoryDailyMetric> rows, string key, string dimension)
        => rows.FirstOrDefault(x => x.MetricKey == key && x.DimensionKey == dimension)?.TotalCount;
}