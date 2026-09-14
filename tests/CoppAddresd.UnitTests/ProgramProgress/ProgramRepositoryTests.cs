using System.Text.Json;
using CoppAddresd.Api.Seeders;
using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ReconcileStreaks;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CoppAddresd.UnitTests.ProgramProgress;

/// <summary>
/// BD aislada para los tests del repositorio de programa: crea
/// <c>coppaddresd_prog_test_&lt;timestamp&gt;</c> (T-08), aplica el esquema
/// <c>auth</c> mínimo que referencian las FKs por SQL de las migraciones y
/// ejecuta la cadena completa de migraciones. Requiere
/// <c>COP_TEST_DB_CONNECTION</c>; si no está definida, los tests se omiten.
/// </summary>
public sealed class ProgramRepositoryTestDb : IAsyncLifetime
{
    private const string EnvVar = "COP_TEST_DB_CONNECTION";

    private readonly string _baseConnectionString = Environment.GetEnvironmentVariable(EnvVar)!;
    private readonly string _testDbName =
        $"coppaddresd_prog_test_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..44];

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
        // Se crea si falta ANTES de migrar; sin privilegios de superusuario la
        // suite fallaría igualmente en los GRANT, así que el intento es seguro.
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

        // Locale: 'en_US.utf8' no existe en todo clúster (Windows usa nombres
        // tipo 'English_United States.1252'; Linux mínimo solo C/C.UTF-8). Se
        // detecta en pg_collation y, si falta, se usa el collate de la BD
        // "postgres" del clúster (template0 con un locale disponible).
        var locale = await DetectAvailableLocaleAsync();

        // Recrea la BD desde cero (reruns seguros) y aplica las migraciones.
        // TEMPLATE template0: template1 arrastra un collation version mismatch
        // (XX000) tras upgrades de PostgreSQL; template0 es pristino y se
        // especifica la misma collation/encoding que la BD de dev.
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
/// Tests del repositorio de programa (T-08): cubren AC-01..AC-05, AC-10, AC-13
/// y AC-15 de la SPEC §10.2 + máquina de estados, plantillas y adaptaciones.
/// Cada test arranca con la BD truncada y el seed mínimo (plantilla por defecto
/// vía <see cref="ProgramProgressSeeder"/> + un paciente).
/// </summary>
public sealed class ProgramRepositoryTests(ProgramRepositoryTestDb fixture)
    : IClassFixture<ProgramRepositoryTestDb>,
        IAsyncLifetime
{
    private static readonly (TaskCode Code, int Points)[] TaskSeeds =
    [
        (TaskCode.podcast, 80),
        (TaskCode.vitals, 120),
        (TaskCode.nut, 150),
        (TaskCode.ejercicio, 150),
        (TaskCode.nutraceutico, 80),
        (TaskCode.emocional, 120),
    ];

    private Guid _templateId;
    private Guid _patientId;
    private DateOnly _monday;

    public async Task InitializeAsync()
    {
        if (fixture.Skipped)
        {
            return;
        }

        await using var db = fixture.CreateDbContext();
        await ResetProgramTablesAsync(db);
        _templateId = await SeedDefaultTemplateAsync(db);
        _patientId = await CreatePatientAsync(db, "Ana", "Prueba");
        _monday = ThisMonday();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------- AC-01

    /// <summary>
    /// Re-inscripción (retiro → enroll): la purga de puntajes en la MISMA
    /// transacción de EnrollAsync elimina las filas de la corrida anterior
    /// (health_scores + transformation_scores son por PACIENTE y sus semanas
    /// colisionarían con la nueva corrida, SPEC §13.7.3). Integration-only:
    /// la purga es una operación EF dentro del repositorio (no fakeable).
    /// </summary>
    [RequiresPostgresFact]
    public async Task EnrollAsync_ReInscripcion_PurgaPuntajesDeLaCorridaAnterior()
    {
        var patientId = await CreatePatientAsync(fixture.CreateDbContext(), "Purga", "Puntajes");
        var enrollmentId = await EnrollAsync(patientId: patientId);

        // Puntajes de la corrida 1 (salud con período dentro de la semana 1 y
        // transformación de la semana 3 — la que mapearía a semana futura).
        await using (var db = fixture.CreateDbContext())
        {
            db.HealthScores.Add(
                new HealthScore
                {
                    Id = Guid.NewGuid(),
                    PatientId = patientId,
                    Score = 21,
                    ScoreAdherence = 1,
                    ScoreClinical = 2,
                    ScoreNutrition = 3,
                    ScorePsychology = 4,
                    ScoreExercise = 5,
                    Trend = ScoreTrend.stable,
                    PeriodStart = _monday,
                    PeriodEnd = _monday.AddDays(1),
                    CalculatedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                }
            );
            db.TransformationScores.Add(
                new TransformationScore
                {
                    Id = Guid.NewGuid(),
                    PatientId = patientId,
                    Score = 88,
                    WeekNumber = 3,
                    Detail = JsonSerializer.SerializeToElement(new { }),
                    OverallTrend = ScoreTrend.stable,
                    CalculatedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                }
            );
            await db.SaveChangesAsync();
        }

        // Retiro → re-inscripción: la nueva corrida arranca sin historial viejo.
        await using (var db = fixture.CreateDbContext())
        {
            var repo = new ProgramRepository(db, Configuration());
            await repo.WithdrawAsync(enrollmentId);
        }

        var secondEnrollmentId = await EnrollAsync(patientId: patientId);
        Assert.NotEqual(enrollmentId, secondEnrollmentId);

        await using var verifyDb = fixture.CreateDbContext();
        Assert.Equal(
            0,
            await verifyDb.HealthScores.CountAsync(h => h.PatientId == patientId)
        );
        Assert.Equal(
            0,
            await verifyDb.TransformationScores.CountAsync(t => t.PatientId == patientId)
        );
    }

    [RequiresPostgresFact]
    public async Task CompleteTask_PrimeraVez_OtorgaXpExactamenteUnaVez()
    {
        var enrollmentId = await EnrollAsync();
        var tuesday = _monday.AddDays(1);

        var result = await CompleteAsync(enrollmentId, tuesday, TaskCode.podcast, "ac01-1");

        Assert.Equal(CompleteTaskOutcome.Created, result.Outcome);
        Assert.Equal(80, result.PointsAwarded);
        Assert.Equal(80, result.XpBalanceAfter);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(
            1,
            await db.TaskCompletions.CountAsync(t =>
                t.EnrollmentId == enrollmentId
                && t.LocalDate == tuesday
                && t.TaskCode == TaskCode.podcast
            )
        );
        Assert.Equal(
            1,
            await db.XpLedgerEntries.CountAsync(x =>
                x.EnrollmentId == enrollmentId && x.Reason == XpReason.TaskCompletion
            )
        );
        Assert.Equal(
            80,
            await db
                .XpLedgerEntries.Where(x => x.EnrollmentId == enrollmentId)
                .MaxAsync(x => x.BalanceAfter)
        );
    }

    // ---------------------------------------------------------------- AC-02

    [RequiresPostgresFact]
    public async Task CompleteTask_ReplayMismoClientRequestId_SinFilasNuevas()
    {
        var enrollmentId = await EnrollAsync();
        var tuesday = _monday.AddDays(1);

        var first = await CompleteAsync(enrollmentId, tuesday, TaskCode.podcast, "ac02-1");
        var replay = await CompleteAsync(enrollmentId, tuesday, TaskCode.podcast, "ac02-1");

        Assert.Equal(CompleteTaskOutcome.Created, first.Outcome);
        Assert.Equal(CompleteTaskOutcome.Replay, replay.Outcome);
        Assert.Equal(first.TaskCompletionId, replay.TaskCompletionId);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(
            1,
            await db.TaskCompletions.CountAsync(t =>
                t.EnrollmentId == enrollmentId && t.LocalDate == tuesday
            )
        );
        Assert.Equal(
            1,
            await db.XpLedgerEntries.CountAsync(x =>
                x.EnrollmentId == enrollmentId && x.Reason == XpReason.TaskCompletion
            )
        );
        Assert.Equal(80, await db.XpLedgerEntries.MaxAsync(x => x.BalanceAfter));
    }

    // ---------------------------------------------------------------- AC-03

    [RequiresPostgresFact]
    public async Task CompleteTask_MismaClaveDiferenteRequestId_DevuelveExistenteSinDobleXp()
    {
        var enrollmentId = await EnrollAsync();
        var tuesday = _monday.AddDays(1);

        await CompleteAsync(enrollmentId, tuesday, TaskCode.podcast, "ac03-a");
        var replay = await CompleteAsync(enrollmentId, tuesday, TaskCode.podcast, "ac03-b");

        Assert.Equal(CompleteTaskOutcome.Replay, replay.Outcome);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(
            1,
            await db.TaskCompletions.CountAsync(t =>
                t.EnrollmentId == enrollmentId && t.LocalDate == tuesday
            )
        );
        Assert.Equal(80, await db.XpLedgerEntries.MaxAsync(x => x.BalanceAfter));
    }

    // ---------------------------------------------------------------- AC-04

    [RequiresPostgresFact]
    public async Task CompleteTask_MismoRequestIdDiferenteFecha_DevuelveIdempotencyKeyReused()
    {
        var enrollmentId = await EnrollAsync();
        var tuesday = _monday.AddDays(1);
        var wednesday = _monday.AddDays(2);

        await CompleteAsync(enrollmentId, tuesday, TaskCode.podcast, "ac04-key");
        var conflicted = await CompleteAsync(enrollmentId, wednesday, TaskCode.vitals, "ac04-key");

        Assert.Equal(CompleteTaskOutcome.IdempotencyKeyReused, conflicted.Outcome);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(
            0,
            await db.TaskCompletions.CountAsync(t =>
                t.EnrollmentId == enrollmentId && t.LocalDate == wednesday
            )
        );
        Assert.Equal(80, await db.XpLedgerEntries.MaxAsync(x => x.BalanceAfter));
    }

    // ---------------------------------------------------------------- AC-05

    [RequiresPostgresFact]
    public async Task CompleteTask_TodasLasTareasDelDia_DiaPerfectoYBonusUnico()
    {
        var enrollmentId = await EnrollAsync();
        var tuesday = _monday.AddDays(1);

        CompleteTaskResult? last = null;
        foreach (var (code, _) in TaskSeeds)
        {
            last = await CompleteAsync(enrollmentId, tuesday, code, $"ac05-{code}");
        }

        Assert.NotNull(last);
        Assert.True(last!.IsPerfectDay);
        Assert.Equal(50, last.DailyBonusAwarded);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(
            6,
            await db.TaskCompletions.CountAsync(t =>
                t.EnrollmentId == enrollmentId && t.LocalDate == tuesday
            )
        );
        var checkin = await db
            .DailyCheckIns.AsNoTracking()
            .SingleAsync(c => c.EnrollmentId == enrollmentId && c.LocalDate == tuesday);
        Assert.True(checkin.IsPerfectDay);
        Assert.Equal(50, checkin.BonusAwarded);
        Assert.Equal(
            1,
            await db.XpLedgerEntries.CountAsync(x =>
                x.EnrollmentId == enrollmentId && x.Reason == XpReason.DailyBonus
            )
        );
        // 700 puntos base + 50 bonus.
        Assert.Equal(750, await db.XpLedgerEntries.MaxAsync(x => x.BalanceAfter));
        Assert.Equal(
            1,
            await db
                .StreakStates.AsNoTracking()
                .Where(s => s.EnrollmentId == enrollmentId)
                .Select(s => s.CurrentStreak)
                .SingleAsync()
        );
    }

    // ---------------------------------------------------------------- §7.2 replay idéntico

    [RequiresPostgresFact]
    public async Task CompleteTask_Replay_DevuelveMismoDayPointsMaxQueLaPrimeraEscritura()
    {
        var enrollmentId = await EnrollAsync();
        var tuesday = _monday.AddDays(1);

        CompleteTaskResult? first = null;
        foreach (var (code, _) in TaskSeeds)
        {
            first = await CompleteAsync(enrollmentId, tuesday, code, $"rp-{code}");
        }

        var replay = await CompleteAsync(enrollmentId, tuesday, TaskCode.podcast, "replay-2");

        Assert.Equal(CompleteTaskOutcome.Created, first!.Outcome);
        Assert.Equal(CompleteTaskOutcome.Replay, replay.Outcome);
        Assert.Equal(first.DayPointsMax, replay.DayPointsMax);
        // 700 base + 50 bonus de día perfecto (SPEC §7.2: 750 en ambos caminos).
        Assert.Equal(750, replay.DayPointsMax);
    }

    // ------------------------------------- Bonus de día gobernado por catálogo (SPEC §14)

    /// <summary>
    /// La regla DAY_BONUS con base ≠ 50 manda: el máximo del día (snapshot y
    /// completación) y <c>DailyBonusAmount</c> siguen la base del catálogo, no
    /// el literal histórico +50 (SPEC §6.5/§14). El máximo usa la BASE de la
    /// regla, no el otorgamiento efectivo (que podría incluir multiplicador).
    /// </summary>
    [RequiresPostgresFact]
    public async Task BonusDayBonus_ReglaBase75_SnapshotYCompletacionSiguenLaRegla()
    {
        await SeedDayBonusRuleAsync(r =>
        {
            r.BaseXp = 75;
            r.Active = true;
            r.ValidUntil = null;
        });

        try
        {
            var enrollmentId = await EnrollAsync();
            var tuesday = _monday.AddDays(1);

            await using (var db = fixture.CreateDbContext())
            {
                var repo = new ProgramRepository(db, Configuration());
                var snapshot = await repo.GetSnapshotAsync(enrollmentId, tuesday);

                Assert.NotNull(snapshot);
                // 700 (base del día) + 75 (base de la regla DAY_BONUS).
                Assert.Equal(775, snapshot!.TodayPointsMax);
                Assert.Equal(75, snapshot.DailyBonusAmount);
            }

            CompleteTaskResult? last = null;
            foreach (var (code, _) in TaskSeeds)
            {
                last = await CompleteAsync(enrollmentId, tuesday, code, $"bonus75-{code}");
            }

            Assert.NotNull(last);
            Assert.Equal(75, last!.DailyBonusAwarded);
            Assert.Equal(775, last.DayPointsMax);
        }
        finally
        {
            // Aislamiento: no dejar la regla modificada para otros tests.
            await DeleteDayBonusRuleAsync();
        }
    }

    /// <summary>
    /// Sin regla DAY_BONUS (o inactiva/vencida) → fallback 50 (SPEC §14.3):
    /// idéntico al comportamiento histórico del literal +50, tanto en el
    /// snapshot como en la completación.
    /// </summary>
    [RequiresPostgresFact]
    public async Task BonusDayBonus_SinRegla_Fallback50EnSnapshotYCompletacion()
    {
        await DeleteDayBonusRuleAsync();

        var enrollmentId = await EnrollAsync();
        var tuesday = _monday.AddDays(1);

        await using (var db = fixture.CreateDbContext())
        {
            var repo = new ProgramRepository(db, Configuration());
            var snapshot = await repo.GetSnapshotAsync(enrollmentId, tuesday);

            Assert.NotNull(snapshot);
            Assert.Equal(750, snapshot!.TodayPointsMax);
            Assert.Equal(50, snapshot.DailyBonusAmount);
        }

        CompleteTaskResult? last = null;
        foreach (var (code, _) in TaskSeeds)
        {
            last = await CompleteAsync(enrollmentId, tuesday, code, $"bonus50-{code}");
        }

        Assert.NotNull(last);
        Assert.Equal(50, last!.DailyBonusAwarded);
        Assert.Equal(750, last.DayPointsMax);
    }

    /// <summary>
    /// Regla DAY_BONUS con <c>Active = false</c> → fallback 50 (SPEC §14.3):
    /// la inactividad equivale a la ausencia, en snapshot y completación.
    /// </summary>
    [RequiresPostgresFact]
    public async Task BonusDayBonus_ReglaInactiva_Fallback50()
    {
        await SeedDayBonusRuleAsync(r =>
        {
            r.BaseXp = 75;
            r.Active = false;
        });

        try
        {
            var enrollmentId = await EnrollAsync();
            var tuesday = _monday.AddDays(1);

            await using (var db = fixture.CreateDbContext())
            {
                var repo = new ProgramRepository(db, Configuration());
                var snapshot = await repo.GetSnapshotAsync(enrollmentId, tuesday);

                Assert.NotNull(snapshot);
                Assert.Equal(750, snapshot!.TodayPointsMax);
                Assert.Equal(50, snapshot.DailyBonusAmount);
            }

            CompleteTaskResult? last = null;
            foreach (var (code, _) in TaskSeeds)
            {
                last = await CompleteAsync(enrollmentId, tuesday, code, $"inactive-{code}");
            }

            Assert.NotNull(last);
            Assert.Equal(50, last!.DailyBonusAwarded);
            Assert.Equal(750, last.DayPointsMax);
        }
        finally
        {
            // Aislamiento: no dejar la regla inactiva para otros tests.
            await DeleteDayBonusRuleAsync();
        }
    }

    /// <summary>
    /// Regla DAY_BONUS vencida (<c>ValidUntil</c> = ayer) → fallback 50
    /// (SPEC §14.3): la ventana de vigencia se evalúa contra el día UTC del
    /// servidor, igual que el otorgamiento.
    /// </summary>
    [RequiresPostgresFact]
    public async Task BonusDayBonus_ReglaVencida_Fallback50()
    {
        await SeedDayBonusRuleAsync(r =>
        {
            r.BaseXp = 75;
            r.Active = true;
            r.ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));
            r.ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        });

        try
        {
            var enrollmentId = await EnrollAsync();
            var tuesday = _monday.AddDays(1);

            await using (var db = fixture.CreateDbContext())
            {
                var repo = new ProgramRepository(db, Configuration());
                var snapshot = await repo.GetSnapshotAsync(enrollmentId, tuesday);

                Assert.NotNull(snapshot);
                Assert.Equal(750, snapshot!.TodayPointsMax);
                Assert.Equal(50, snapshot.DailyBonusAmount);
            }

            CompleteTaskResult? last = null;
            foreach (var (code, _) in TaskSeeds)
            {
                last = await CompleteAsync(enrollmentId, tuesday, code, $"expired-{code}");
            }

            Assert.NotNull(last);
            Assert.Equal(50, last!.DailyBonusAwarded);
            Assert.Equal(750, last.DayPointsMax);
        }
        finally
        {
            await DeleteDayBonusRuleAsync();
        }
    }

    /// <summary>
    /// Regla DAY_BONUS vigente con <c>BaseXp = null</c> → 50 (SPEC §14.3:
    /// <c>base_xp ?? defaultPoints</c>): el máximo y la tarjeta usan el mismo
    /// fallback que el otorgamiento.
    /// </summary>
    [RequiresPostgresFact]
    public async Task BonusDayBonus_BaseXpNula_Fallback50()
    {
        await SeedDayBonusRuleAsync(r =>
        {
            r.BaseXp = null;
            r.Active = true;
            r.ValidUntil = null;
        });

        try
        {
            var enrollmentId = await EnrollAsync();
            var tuesday = _monday.AddDays(1);

            await using (var db = fixture.CreateDbContext())
            {
                var repo = new ProgramRepository(db, Configuration());
                var snapshot = await repo.GetSnapshotAsync(enrollmentId, tuesday);

                Assert.NotNull(snapshot);
                Assert.Equal(750, snapshot!.TodayPointsMax);
                Assert.Equal(50, snapshot.DailyBonusAmount);
            }

            CompleteTaskResult? last = null;
            foreach (var (code, _) in TaskSeeds)
            {
                last = await CompleteAsync(enrollmentId, tuesday, code, $"bxn-{code}");
            }

            Assert.NotNull(last);
            Assert.Equal(50, last!.DailyBonusAwarded);
            Assert.Equal(750, last.DayPointsMax);
        }
        finally
        {
            await DeleteDayBonusRuleAsync();
        }
    }

    /// <summary>
    /// Regla DAY_BONUS con <c>Multiplier = 2</c>: el MÁXIMO del día usa la
    /// BASE de la regla (700 + 75 = 775) mientras el otorgamiento efectivo
    /// refleja el multiplicador (floor(75 × 2) = 150). Prueba que el máximo
    /// nunca adopta el grant multiplicado (semántica histórica de la base
    /// plana).
    /// </summary>
    [RequiresPostgresFact]
    public async Task BonusDayBonus_Multiplicador2_MaxUsaBaseYGrantReflejaMultiplicador()
    {
        await SeedDayBonusRuleAsync(r =>
        {
            r.BaseXp = 75;
            r.Multiplier = 2m;
            r.Active = true;
            r.ValidUntil = null;
        });

        try
        {
            var enrollmentId = await EnrollAsync();
            var tuesday = _monday.AddDays(1);

            await using (var db = fixture.CreateDbContext())
            {
                var repo = new ProgramRepository(db, Configuration());
                var snapshot = await repo.GetSnapshotAsync(enrollmentId, tuesday);

                Assert.NotNull(snapshot);
                // El máximo y la tarjeta exponen la BASE (75), no el grant.
                Assert.Equal(775, snapshot!.TodayPointsMax);
                Assert.Equal(75, snapshot.DailyBonusAmount);
            }

            CompleteTaskResult? last = null;
            foreach (var (code, _) in TaskSeeds)
            {
                last = await CompleteAsync(enrollmentId, tuesday, code, $"mult2-{code}");
            }

            Assert.NotNull(last);
            Assert.Equal(150, last!.DailyBonusAwarded);
            Assert.Equal(775, last.DayPointsMax);
        }
        finally
        {
            await DeleteDayBonusRuleAsync();
        }
    }

    // ---------------------------------------------------------------- AC-10

    [RequiresPostgresFact]
    public async Task CompleteTask_50Concurrentes_UnaSolaFilaYUnaSolaXp()
    {
        var enrollmentId = await EnrollAsync();
        var tuesday = _monday.AddDays(1);

        var tasks = Enumerable
            .Range(0, 50)
            .Select(i => CompleteAsync(enrollmentId, tuesday, TaskCode.podcast, $"ac10-{i}"));
        var results = await Task.WhenAll(tasks);

        Assert.Single(results, r => r.Outcome == CompleteTaskOutcome.Created);
        Assert.Equal(49, results.Count(r => r.Outcome == CompleteTaskOutcome.Replay));

        await using var db = fixture.CreateDbContext();
        Assert.Equal(
            1,
            await db.TaskCompletions.CountAsync(t =>
                t.EnrollmentId == enrollmentId
                && t.LocalDate == tuesday
                && t.TaskCode == TaskCode.podcast
            )
        );
        Assert.Equal(
            1,
            await db.XpLedgerEntries.CountAsync(x =>
                x.EnrollmentId == enrollmentId && x.Reason == XpReason.TaskCompletion
            )
        );
        Assert.Equal(80, await db.XpLedgerEntries.MaxAsync(x => x.BalanceAfter));
    }

    // ---------------------------------------------------------------- AC-13

    [RequiresPostgresFact]
    public async Task CompleteTask_NutSinPlan_ContenidoNuloYConXp()
    {
        var enrollmentId = await EnrollAsync();
        var tuesday = _monday.AddDays(1);

        var result = await CompleteAsync(enrollmentId, tuesday, TaskCode.nut, "ac13-1");

        await using var db = fixture.CreateDbContext();
        var completion = await db
            .TaskCompletions.AsNoTracking()
            .SingleAsync(t =>
                t.EnrollmentId == enrollmentId
                && t.LocalDate == tuesday
                && t.TaskCode == TaskCode.nut
            );
        Assert.Null(completion.NutritionPlanId);
        Assert.Null(completion.NutritionPlanDayNumber);
        Assert.Equal(150, completion.PointsAwarded);
        Assert.Equal(150, result.XpBalanceAfter);
        Assert.Equal(CompleteTaskOutcome.Created, result.Outcome);
    }

    // ---------------------------------------------------------------- AC-15

    [RequiresPostgresFact]
    public async Task EdicionPlantillaMitadDeSemana_SnapshotActualInmutable_ProximaUsaNuevoValor()
    {
        var enrollmentId = await EnrollAsync();
        var tuesday = _monday.AddDays(1);
        await CompleteAsync(enrollmentId, tuesday, TaskCode.podcast, "ac15-1");

        // Edición mid-week: podcast del martes (weekday 2) pasa de 80 a 999.
        await using (var editDb = fixture.CreateDbContext())
        {
            var repo = new ProgramRepository(editDb, Configuration());
            var template = await repo.GetTemplateAsync(_templateId);
            Assert.NotNull(template);
            template!
                .DayTemplates.Single(d => d.Weekday == 2 && d.TaskCode == TaskCode.podcast)
                .Points = 999;
            await repo.UpsertTemplateAsync(template, template.DayTemplates.ToList());
        }

        // La semana 1 de la inscripción actual conserva su snapshot congelado.
        await using (var verifyDb = fixture.CreateDbContext())
        {
            var week = await verifyDb
                .ProgramWeeks.AsNoTracking()
                .SingleAsync(w => w.EnrollmentId == enrollmentId && w.WeekNumber == 1);
            var tuesdayPodcast = ParseSnapshot(week.TasksSnapshot)
                .Single(t => t.Weekday == 2 && t.TaskCode == TaskCode.podcast.ToString());
            Assert.Equal(80, tuesdayPodcast.Points);
        }

        // Una nueva inscripción toma el snapshot con el nuevo valor.
        var secondPatient = await CreatePatientAsync(fixture.CreateDbContext(), "Bruno", "Prueba");
        var enrollmentId2 = await EnrollAsync(patientId: secondPatient);
        await using (var verifyDb = fixture.CreateDbContext())
        {
            var week = await verifyDb
                .ProgramWeeks.AsNoTracking()
                .SingleAsync(w => w.EnrollmentId == enrollmentId2 && w.WeekNumber == 1);
            var tuesdayPodcast = ParseSnapshot(week.TasksSnapshot)
                .Single(t => t.Weekday == 2 && t.TaskCode == TaskCode.podcast.ToString());
            Assert.Equal(999, tuesdayPodcast.Points);
        }
    }

    [RequiresPostgresFact]
    public async Task EdicionPlantilla_SeAplicaAlActivarLaSemana2DeLaMismaInscripcion()
    {
        var enrollmentId = await EnrollAsync();
        var tuesday = _monday.AddDays(1);
        await CompleteAsync(enrollmentId, tuesday, TaskCode.podcast, "a15-1");

        // Edición mid-week: podcast del martes (weekday 2) pasa de 80 a 999.
        await using (var editDb = fixture.CreateDbContext())
        {
            var repo = new ProgramRepository(editDb, Configuration());
            var template = await repo.GetTemplateAsync(_templateId);
            Assert.NotNull(template);
            template!
                .DayTemplates.Single(d => d.Weekday == 2 && d.TaskCode == TaskCode.podcast)
                .Points = 999;
            await repo.UpsertTemplateAsync(template, template.DayTemplates.ToList());
        }

        // La MISMA inscripción avanza a la semana 2 (fechas siguientes): el
        // snapshot de la semana 2 se toma de la plantilla vigente al activar
        // (SPEC §4.5), mientras la semana 1 conserva su snapshot congelado.
        var week2Monday = _monday.AddDays(7);
        var result = await CompleteAsync(enrollmentId, week2Monday, TaskCode.vitals, "a15-2");
        Assert.Equal(CompleteTaskOutcome.Created, result.Outcome);

        await using var verifyDb = fixture.CreateDbContext();
        var week2 = await verifyDb
            .ProgramWeeks.AsNoTracking()
            .SingleAsync(w => w.EnrollmentId == enrollmentId && w.WeekNumber == 2);
        Assert.Equal(ProgramWeekStatus.Active, week2.Status);
        var tuesdayPodcastWeek2 = ParseSnapshot(week2.TasksSnapshot)
            .Single(t => t.Weekday == 2 && t.TaskCode == TaskCode.podcast.ToString());
        Assert.Equal(999, tuesdayPodcastWeek2.Points);

        var week1 = await verifyDb
            .ProgramWeeks.AsNoTracking()
            .SingleAsync(w => w.EnrollmentId == enrollmentId && w.WeekNumber == 1);
        Assert.Equal(ProgramWeekStatus.Completed, week1.Status);
        var tuesdayPodcastWeek1 = ParseSnapshot(week1.TasksSnapshot)
            .Single(t => t.Weekday == 2 && t.TaskCode == TaskCode.podcast.ToString());
        Assert.Equal(80, tuesdayPodcastWeek1.Points);
    }

    // ---------------------------------------------------------------- Estado / reglas

    [RequiresPostgresFact]
    public async Task PausaResumeRetiro_MaquinaDeEstados_YBloqueoDeCompletacion()
    {
        var enrollmentId = await EnrollAsync();
        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());

        var paused = await repo.PauseAsync(enrollmentId);
        Assert.Equal(ProgramEnrollmentStatus.Paused, paused.Status);
        Assert.NotNull(paused.PausedAt);

        var inactive = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            CompleteAsync(enrollmentId, _monday, TaskCode.podcast, "st-1")
        );
        Assert.Contains("ENROLLMENT_INACTIVE", inactive.Message);

        var resumed = await repo.ResumeAsync(enrollmentId);
        Assert.Equal(ProgramEnrollmentStatus.Active, resumed.Status);
        Assert.Null(resumed.PausedAt);

        var withdrawn = await repo.WithdrawAsync(enrollmentId);
        Assert.Equal(ProgramEnrollmentStatus.Withdrawn, withdrawn.Status);
        Assert.NotNull(withdrawn.WithdrawnAt);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            repo.WithdrawAsync(enrollmentId)
        );
    }

    [RequiresPostgresFact]
    public async Task CompleteTask_TareaNoProgramadaEnSnapshot_LanzaTaskNotScheduled()
    {
        var sparseTemplateId = await SeedSparseTemplateAsync();
        var enrollmentId = await EnrollAsync(templateId: sparseTemplateId);
        var monday = _monday;

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            CompleteAsync(enrollmentId, monday, TaskCode.nut, "ns-1")
        );
        Assert.Contains("TASK_NOT_SCHEDULED", ex.Message);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(
            0,
            await db.TaskCompletions.CountAsync(t =>
                t.EnrollmentId == enrollmentId && t.LocalDate == monday
            )
        );
    }

    [RequiresPostgresFact]
    public async Task CompleteTask_FechaFueraDelPrograma_LanzaDateOutsideActiveWeek()
    {
        var enrollmentId = await EnrollAsync();
        var beyondProgram = _monday.AddDays((83 * 7) + 10);

        var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            CompleteAsync(enrollmentId, beyondProgram, TaskCode.podcast, "out-1")
        );
        Assert.Contains("DATE_OUTSIDE_ACTIVE_WEEK", ex.Message);
    }

    [RequiresPostgresFact]
    public async Task EnrollAsync_CreaInscripcionSemanasYRacha()
    {
        var enrollmentId = await EnrollAsync();

        await using var db = fixture.CreateDbContext();
        var enrollment = await db
            .ProgramEnrollments.AsNoTracking()
            .SingleAsync(e => e.Id == enrollmentId);
        Assert.Equal(ProgramEnrollmentStatus.Active, enrollment.Status);
        Assert.Equal(1, enrollment.CurrentWeekNumber);
        Assert.Equal(_monday, enrollment.StartLocalDate);

        Assert.Equal(83, await db.ProgramWeeks.CountAsync(w => w.EnrollmentId == enrollmentId));
        var week1 = await db
            .ProgramWeeks.AsNoTracking()
            .SingleAsync(w => w.EnrollmentId == enrollmentId && w.WeekNumber == 1);
        Assert.Equal(ProgramWeekStatus.Active, week1.Status);
        Assert.Equal(_monday, week1.WeekStartDateLocal);
        Assert.Equal(_monday.AddDays(6), week1.WeekEndDateLocal);
        Assert.Equal(42, ParseSnapshot(week1.TasksSnapshot).Count);

        Assert.True(await db.StreakStates.AnyAsync(s => s.EnrollmentId == enrollmentId));
    }

    [RequiresPostgresFact]
    public async Task EnrollAsync_SegundaInscripcionActiva_LanzaPatienteYaInscrito()
    {
        await EnrollAsync();

        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            repo.EnrollAsync(_patientId, _templateId, "America/Bogota", _monday)
        );
        Assert.Contains("PATIENT_ALREADY_ENROLLED", ex.Message);
    }

    // ---------------------------------------------------------------- Lecturas

    [RequiresPostgresFact]
    public async Task GetSnapshot_DevuelveEstadoCompletoDelDia()
    {
        var enrollmentId = await EnrollAsync();
        var tuesday = _monday.AddDays(1);
        await CompleteAsync(enrollmentId, tuesday, TaskCode.podcast, "snap-1");

        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());
        var snapshot = await repo.GetSnapshotAsync(enrollmentId, tuesday);

        Assert.NotNull(snapshot);
        Assert.Equal(enrollmentId, snapshot!.EnrollmentId);
        Assert.Equal(1, snapshot.Template.CurrentWeekNumber);
        Assert.Equal(ProgramWeekStatus.Active, snapshot.Template.CurrentWeekStatus);
        Assert.Equal(6, snapshot.TodayTasks.Count);
        Assert.Equal(
            "Completed",
            snapshot.TodayTasks.Single(t => t.TaskCode == TaskCode.podcast).Status
        );
        Assert.Equal(
            "Pending",
            snapshot.TodayTasks.Single(t => t.TaskCode == TaskCode.vitals).Status
        );
        Assert.Equal(80, snapshot.TodayPoints);
        Assert.Equal(750, snapshot.TodayPointsMax);
        Assert.True(snapshot.TodayBonusAvailable);
        Assert.Equal(80, snapshot.Xp.Balance);
        Assert.Equal("Explorador", snapshot.Xp.Level);
        Assert.Equal(500, snapshot.Xp.NextLevelAt);
        // Racha por umbral (SPEC §17, B): con StreakMinTasks default 1, completar
        // 1 tarea ya inicia la racha (no se requiere día perfecto).
        Assert.Equal(1, snapshot.Streak.Current);
        Assert.Equal(7, snapshot.Calendar.Count);
    }

    /// <summary>
    /// Decay en lectura (SPEC §17, B): si el último día calificado quedó a más
    /// de un día de la fecha de lectura, la corrida está vencida → current 0
    /// aunque el escalar siga almacenado (misma regla que ComputeStreakRuns).
    /// longest/freezes NO se decaen.
    /// </summary>
    [RequiresPostgresFact]
    public async Task GetSnapshot_RachaVencida_DevuelveCurrent0ConservandoLongestYFreezes()
    {
        var enrollmentId = await EnrollAsync();
        var today = _monday;
        await SeedStreakStateAsync(
            enrollmentId,
            current: 2,
            longest: 9,
            freezes: 3,
            lastActive: today.AddDays(-3)
        );

        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());
        var snapshot = await repo.GetSnapshotAsync(enrollmentId, today);

        Assert.NotNull(snapshot);
        Assert.Equal(0, snapshot!.Streak.Current);
        Assert.Equal(9, snapshot.Streak.Longest);
        Assert.Equal(3, snapshot.Streak.FreezesRemaining);
        // El próximo hito se recalcula desde la racha efectiva (0 → 7 días).
        Assert.Equal(7, snapshot.NextMilestoneDays);
    }

    /// <summary>
    /// Con el último día calificado AYER de la fecha de lectura la racha sigue
    /// vigente: el escalar se conserva (ventana hoy/ayer, misma regla que
    /// ComputeStreakRuns) y el próximo hito parte del valor real.
    /// </summary>
    [RequiresPostgresFact]
    public async Task GetSnapshot_RachaVigenteAyer_ConservaElEscalar()
    {
        var enrollmentId = await EnrollAsync();
        var today = _monday;
        await SeedStreakStateAsync(
            enrollmentId,
            current: 2,
            longest: 2,
            freezes: 0,
            lastActive: today.AddDays(-1)
        );

        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());
        var snapshot = await repo.GetSnapshotAsync(enrollmentId, today);

        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot!.Streak.Current);
        // Próximo hito desde 2 → 5 días restantes para el bloque de 7.
        Assert.Equal(5, snapshot.NextMilestoneDays);
    }

    [RequiresPostgresFact]
    public async Task GetCalendar_VentanaDevuelveDiasYResumen()
    {
        var enrollmentId = await EnrollAsync();
        var monday = _monday;
        await CompleteAsync(enrollmentId, monday, TaskCode.podcast, "cal-1");
        await CompleteAsync(enrollmentId, monday, TaskCode.vitals, "cal-2");
        await CompleteAsync(enrollmentId, monday.AddDays(1), TaskCode.podcast, "cal-3");

        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());
        var calendar = await repo.GetCalendarAsync(enrollmentId, monday, monday.AddDays(13));

        Assert.Equal(14, calendar.Days.Count);
        var firstDay = calendar.Days.Single(d => d.LocalDate == monday);
        Assert.Equal(1, firstDay.WeekNumber);
        Assert.Equal(200, firstDay.Points);
        Assert.Contains("podcast", firstDay.CompletedTaskCodes);
        Assert.Contains("vitals", firstDay.CompletedTaskCodes);
        Assert.Equal(280, calendar.Summary.TotalXp);
        Assert.Equal(0, calendar.Summary.PerfectDays);
    }

    [RequiresPostgresFact]
    public async Task GetPath_DevuelveLas83Semanas()
    {
        var enrollmentId = await EnrollAsync();
        var monday = _monday;
        await CompleteAsync(enrollmentId, monday, TaskCode.podcast, "path-1");
        await CompleteAsync(enrollmentId, monday, TaskCode.vitals, "path-2");

        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());
        var path = await repo.GetPathAsync(enrollmentId);

        Assert.Equal(83, path.Weeks.Count);
        Assert.Equal(ProgramWeekStatus.Active, path.Weeks[0].Status);
        Assert.Equal(ProgramWeekStatus.Locked, path.Weeks[1].Status);
        Assert.Equal(monday, path.Weeks[0].WeekStartDateLocal);
        Assert.Equal(monday.AddDays(6), path.Weeks[0].WeekEndDateLocal);
        Assert.Equal(200, path.Weeks[0].Points);
        Assert.Null(path.Weeks[0].IsPerfectWeek);
        Assert.Equal(0, path.Weeks[1].Points);
    }

    // ---------------------------------------------------------------- Plantillas

    [RequiresPostgresFact]
    public async Task Plantillas_UpsertCreaActualiza_YReemplazoDeTareas()
    {
        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());

        // Crear.
        var nuevo = new ProgramTemplate
        {
            Code = "test-sparse",
            Name = "Plantilla de prueba",
            TotalWeeks = 12,
            Status = TemplateStatus.Draft,
            Version = 1,
        };
        var tareas = new List<WeeklyDayTemplate>
        {
            new()
            {
                Weekday = 1,
                TaskCode = TaskCode.podcast,
                Points = 100,
                SortOrder = 1,
            },
            new()
            {
                Weekday = 1,
                TaskCode = TaskCode.vitals,
                Points = 120,
                SortOrder = 2,
            },
        };
        var created = await repo.UpsertTemplateAsync(nuevo, tareas);
        Assert.NotEqual(Guid.Empty, created.Id);

        // Listar + leer con sus filas.
        var (items, total) = await repo.ListTemplatesAsync(null, null, 1, 20);
        Assert.Equal(2, total);
        var loaded = await repo.GetTemplateAsync(created.Id);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.DayTemplates.Count);

        // Reemplazo en bloque de las tareas por día.
        var reemplazo = new List<WeeklyDayTemplate>
        {
            new()
            {
                Weekday = 1,
                TaskCode = TaskCode.podcast,
                Points = 150,
                SortOrder = 1,
            },
            new()
            {
                Weekday = 1,
                TaskCode = TaskCode.nut,
                Points = 150,
                SortOrder = 2,
            },
            new()
            {
                Weekday = 2,
                TaskCode = TaskCode.ejercicio,
                Points = 200,
                SortOrder = 1,
            },
        };
        var replaced = await repo.ReplaceWeekdayTasksAsync(created.Id, reemplazo);
        Assert.Equal(3, replaced.Count);

        var reloaded = await repo.GetTemplateAsync(created.Id);
        Assert.Equal(3, reloaded!.DayTemplates.Count);
        Assert.Contains(
            reloaded.DayTemplates,
            d => d.Weekday == 2 && d.TaskCode == TaskCode.ejercicio && d.Points == 200
        );
    }

    // ---------------------------------------------------------------- Adaptaciones

    [RequiresPostgresFact]
    public async Task DecideAdaptacion_PendingAprobadaAplicada_YTransicionesInvalidas()
    {
        var enrollmentId = await EnrollAsync();
        await using var db = fixture.CreateDbContext();

        var adaptation = new AdaptationRecommendation
        {
            Id = Guid.NewGuid(),
            EnrollmentId = enrollmentId,
            Kind = AdaptationKind.DifficultyChange,
            TargetEntityType = AdaptationTargetEntityType.ProgramEnrollments,
            TargetEntityId = enrollmentId,
            Payload = JsonSerializer.SerializeToElement(new { motivo = "prueba" }),
            Reason = "Test de transición",
            Status = AdaptationStatus.Pending,
            RequiresApproval = true,
        };
        db.AdaptationRecommendations.Add(adaptation);
        await db.SaveChangesAsync();

        var repo = new ProgramRepository(db, Configuration());

        var approved = await repo.DecideAdaptationAsync(
            adaptation.Id,
            AdaptationDecisionAction.Approve
        );
        Assert.Equal(AdaptationStatus.Approved, approved.Status);
        Assert.NotNull(approved.DecidedAt);

        // AC-17: el Apply pasa auditActionOnApply y la fila semántica se escribe
        // en la MISMA transacción que la transición (verificable en BD real).
        var actorId = Guid.NewGuid();
        var applied = await repo.DecideAdaptationAsync(
            adaptation.Id,
            AdaptationDecisionAction.Apply,
            actorId,
            auditActionOnApply: "AdaptationApplied"
        );
        Assert.Equal(AdaptationStatus.Applied, applied.Status);
        Assert.NotNull(applied.AppliedAt);

        // La fila semántica se distingue de las filas del trigger (UPDATE del DML).
        var audit = await db
            .ActivityLogs.AsNoTracking()
            .SingleAsync(l =>
                l.RecordId == adaptation.Id.ToString() && l.Action == AuditAction.AdaptationApplied
            );
        Assert.Equal(actorId, audit.UserId);

        // Reject desde Applied no está permitido (solo Pending).
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            repo.DecideAdaptationAsync(adaptation.Id, AdaptationDecisionAction.Reject)
        );
    }

    [RequiresPostgresFact]
    public async Task DecideAdaptacion_RejectSoloDesdePending_YListado()
    {
        var enrollmentId = await EnrollAsync();
        await using var db = fixture.CreateDbContext();

        var adaptation = new AdaptationRecommendation
        {
            Id = Guid.NewGuid(),
            EnrollmentId = enrollmentId,
            Kind = AdaptationKind.RoutineContentRefresh,
            TargetEntityType = AdaptationTargetEntityType.ExerciseRoutines,
            TargetEntityId = Guid.NewGuid(),
            Payload = JsonSerializer.SerializeToElement(new { }),
            Reason = "Test",
            Status = AdaptationStatus.Pending,
            RequiresApproval = false,
        };
        db.AdaptationRecommendations.Add(adaptation);
        await db.SaveChangesAsync();

        var repo = new ProgramRepository(db, Configuration());

        // Apply desde Pending no está permitido (solo Approved).
        var invalidApply = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            repo.DecideAdaptationAsync(adaptation.Id, AdaptationDecisionAction.Apply)
        );
        Assert.Contains("ADAPTATION_STATE", invalidApply.Message);

        var rejected = await repo.DecideAdaptationAsync(
            adaptation.Id,
            AdaptationDecisionAction.Reject
        );
        Assert.Equal(AdaptationStatus.Rejected, rejected.Status);

        // Rechazar dos veces tampoco.
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            repo.DecideAdaptationAsync(adaptation.Id, AdaptationDecisionAction.Reject)
        );

        // Listado filtrado por estado.
        var (items, total) = await repo.ListAdaptationsAsync(
            enrollmentId,
            AdaptationStatus.Rejected,
            1,
            20
        );
        Assert.Equal(1, total);
        Assert.Single(items);
    }

    [RequiresPostgresFact]
    public async Task SupersedePending_NuevaRecomendacionReemplazaALaAnterior()
    {
        var enrollmentId = await EnrollAsync();
        await using var db = fixture.CreateDbContext();

        var older = new AdaptationRecommendation
        {
            Id = Guid.NewGuid(),
            EnrollmentId = enrollmentId,
            Kind = AdaptationKind.DifficultyChange,
            TargetEntityType = AdaptationTargetEntityType.ProgramEnrollments,
            TargetEntityId = enrollmentId,
            Payload = JsonSerializer.SerializeToElement(new { }),
            Reason = "Versión anterior",
            Status = AdaptationStatus.Pending,
            RequiresApproval = true,
        };
        var newer = new AdaptationRecommendation
        {
            Id = Guid.NewGuid(),
            EnrollmentId = enrollmentId,
            Kind = AdaptationKind.DifficultyChange,
            TargetEntityType = AdaptationTargetEntityType.ProgramEnrollments,
            TargetEntityId = enrollmentId,
            Payload = JsonSerializer.SerializeToElement(new { }),
            Reason = "Versión nueva",
            Status = AdaptationStatus.Pending,
            RequiresApproval = true,
        };
        db.AdaptationRecommendations.AddRange(older, newer);
        await db.SaveChangesAsync();

        var repo = new ProgramRepository(db, Configuration());
        var superseded = await repo.SupersedePendingAsync(
            enrollmentId,
            AdaptationKind.DifficultyChange,
            enrollmentId,
            newer.Id
        );

        // Solo la anterior queda Superseded; la nueva sigue Pending (SPEC §5.6).
        Assert.Equal(1, superseded);
        var olderReloaded = await db
            .AdaptationRecommendations.AsNoTracking()
            .SingleAsync(a => a.Id == older.Id);
        var newerReloaded = await db
            .AdaptationRecommendations.AsNoTracking()
            .SingleAsync(a => a.Id == newer.Id);
        Assert.Equal(AdaptationStatus.Superseded, olderReloaded.Status);
        Assert.NotNull(olderReloaded.UpdatedAt);
        Assert.Equal(AdaptationStatus.Pending, newerReloaded.Status);
    }

    [RequiresPostgresFact]
    public async Task SemanaPerfecta_CompletaSemanaAvanzaYOtorgaCongelamiento()
    {
        var enrollmentId = await EnrollAsync();

        // 6 tareas × 7 días de la semana 1 (42 completaciones → semana perfecta).
        for (var dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var date = _monday.AddDays(dayOffset);
            foreach (var (code, _) in TaskSeeds)
            {
                await CompleteAsync(enrollmentId, date, code, $"pw-{dayOffset}-{code}");
            }
        }

        await using var db = fixture.CreateDbContext();
        var week1 = await db
            .ProgramWeeks.AsNoTracking()
            .SingleAsync(w => w.EnrollmentId == enrollmentId && w.WeekNumber == 1);
        Assert.Equal(
            7,
            await db.DailyCheckIns.CountAsync(c => c.ProgramWeekId == week1.Id && c.IsPerfectDay)
        );
        Assert.Equal(ProgramWeekStatus.Completed, week1.Status);
        Assert.NotNull(week1.CompletedAt);

        var week2 = await db
            .ProgramWeeks.AsNoTracking()
            .SingleAsync(w => w.EnrollmentId == enrollmentId && w.WeekNumber == 2);
        Assert.Equal(ProgramWeekStatus.Active, week2.Status);
        Assert.Equal(42, ParseSnapshot(week2.TasksSnapshot).Count);

        var enrollment = await db
            .ProgramEnrollments.AsNoTracking()
            .SingleAsync(e => e.Id == enrollmentId);
        Assert.Equal(2, enrollment.CurrentWeekNumber);
        Assert.Equal(ProgramEnrollmentStatus.Active, enrollment.Status);

        // OQ-3: 1 congelamiento cada 7 días perfectos, tope 3.
        var streak = await db
            .StreakStates.AsNoTracking()
            .SingleAsync(s => s.EnrollmentId == enrollmentId);
        Assert.Equal(7, streak.CurrentStreak);
        Assert.Equal(1, streak.FreezesRemaining);
        Assert.Equal(
            1,
            await db.StreakFreezes.CountAsync(f =>
                f.EnrollmentId == enrollmentId && f.Kind == StreakFreezeKind.Granted
            )
        );
    }

    // ------------------------------------------------ BLOCKER: rollover imperfecto (§6.7/§5.2)

    [RequiresPostgresFact]
    public async Task SemanaParcial_RolloverCompletaSemana1YActivaSemana2SinBloqueo()
    {
        // Arrange: inscripción + semana 1 PARCIAL (3 tareas, semana imperfecta;
        // la semana 1 nace Active, no Locked).
        var enrollmentId = await EnrollAsync();
        var monday = _monday;
        await CompleteAsync(enrollmentId, monday, TaskCode.podcast, "rp-1");
        await CompleteAsync(enrollmentId, monday, TaskCode.vitals, "rp-2");
        await CompleteAsync(enrollmentId, monday, TaskCode.nut, "rp-3");

        // Act: avanza a fechas de la semana 2 y completa una tarea.
        var week2Monday = monday.AddDays(7);
        var result = await CompleteAsync(enrollmentId, week2Monday, TaskCode.podcast, "rp-4");

        // Assert: sin DATE_OUTSIDE_ACTIVE_WEEK; la semana 1 queda Completed y la 2 Active.
        Assert.Equal(CompleteTaskOutcome.Created, result.Outcome);

        await using var db = fixture.CreateDbContext();
        var week1 = await db
            .ProgramWeeks.AsNoTracking()
            .SingleAsync(w => w.EnrollmentId == enrollmentId && w.WeekNumber == 1);
        Assert.Equal(ProgramWeekStatus.Completed, week1.Status);
        Assert.NotNull(week1.CompletedAt);

        var week2 = await db
            .ProgramWeeks.AsNoTracking()
            .SingleAsync(w => w.EnrollmentId == enrollmentId && w.WeekNumber == 2);
        Assert.Equal(ProgramWeekStatus.Active, week2.Status);
        Assert.Equal(42, ParseSnapshot(week2.TasksSnapshot).Count);

        var enrollment = await db
            .ProgramEnrollments.AsNoTracking()
            .SingleAsync(e => e.Id == enrollmentId);
        Assert.Equal(2, enrollment.CurrentWeekNumber);
    }

    // ---------------------------------------------------------------- AC-06

    [RequiresPostgresFact]
    public async Task Racha_DiasPerfectosConsecutivos_IncrementaRacha()
    {
        var enrollmentId = await EnrollAsync();
        var monday = _monday;

        foreach (var (code, _) in TaskSeeds)
        {
            await CompleteAsync(enrollmentId, monday, code, $"ac06-d1-{code}");
        }

        foreach (var (code, _) in TaskSeeds)
        {
            await CompleteAsync(enrollmentId, monday.AddDays(1), code, $"ac06-d2-{code}");
        }

        await using var db = fixture.CreateDbContext();
        var streak = await db
            .StreakStates.AsNoTracking()
            .SingleAsync(s => s.EnrollmentId == enrollmentId);
        Assert.Equal(2, streak.CurrentStreak);
        Assert.Equal(2, streak.LongestStreak);
        Assert.Equal(monday.AddDays(1), streak.LastActiveDate);
    }

    // ---------------------------------------------------------------- AC-07

    [RequiresPostgresFact]
    public async Task Racha_DiaPerdido_ReseteaYRegistraFechaDeQuiebre()
    {
        var enrollmentId = await EnrollAsync();
        var monday = _monday;

        // Día D perfecto (racha 1).
        foreach (var (code, _) in TaskSeeds)
        {
            await CompleteAsync(enrollmentId, monday, code, $"ac07-d1-{code}");
        }

        // D+1 (martes) sin completaciones; D+2 (miércoles) vuelve perfecto.
        foreach (var (code, _) in TaskSeeds)
        {
            await CompleteAsync(enrollmentId, monday.AddDays(2), code, $"ac07-d3-{code}");
        }

        await using var db = fixture.CreateDbContext();
        var streak = await db
            .StreakStates.AsNoTracking()
            .SingleAsync(s => s.EnrollmentId == enrollmentId);
        // Modelo de corridas (SPEC §17, enmienda): el día de vuelta inicia
        // corrida NUEVA → current = 1 (antes 0; el reconciliador computaba 1
        // y reescribía el valor cada noche — deriva live↔job corregida).
        Assert.Equal(1, streak.CurrentStreak);
        Assert.Equal(monday.AddDays(1), streak.LastBreakDate);
        // Sin castigo: la XP nunca se revoca (solo entradas positivas).
        Assert.Equal(
            0,
            await db.XpLedgerEntries.CountAsync(x => x.EnrollmentId == enrollmentId && x.Amount < 0)
        );
    }

    // ---------------------------------------------------------------- AC-09

    [RequiresPostgresFact]
    public async Task Racha_DiaPerdidoConsumeCongelamiento_InventarioDecrementaYRachaSePreserva()
    {
        var enrollmentId = await EnrollAsync();
        var monday = _monday;

        // 7 días perfectos consecutivos (Lun..Dom de la semana 1) → racha 7 + 1 congelamiento.
        for (var dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var date = monday.AddDays(dayOffset);
            foreach (var (code, _) in TaskSeeds)
            {
                await CompleteAsync(enrollmentId, date, code, $"ac09-d{dayOffset}-{code}");
            }
        }

        // D+8 (lunes de la semana 2) se salta; D+9 (martes) día perfecto → consume.
        foreach (var (code, _) in TaskSeeds)
        {
            await CompleteAsync(enrollmentId, monday.AddDays(8), code, $"ac09-d9-{code}");
        }

        await using var db = fixture.CreateDbContext();
        var streak = await db
            .StreakStates.AsNoTracking()
            .SingleAsync(s => s.EnrollmentId == enrollmentId);
        // Modelo de corridas (SPEC §17, enmienda): hueco de 1 día con rescate
        // → el día rescatado extiende la corrida almacenada y hoy la continúa:
        // 7 + 2 = 9 (antes "racha preservada" en 7 sin contar los días
        // rescatado/hoy — el reconciliador computaba 9).
        Assert.Equal(9, streak.CurrentStreak);
        Assert.Equal(0, streak.FreezesRemaining); // inventario decrementado (1 → 0)
        Assert.Equal(1, streak.FreezesUsedTotal);

        var consumed = await db
            .StreakFreezes.AsNoTracking()
            .SingleAsync(f =>
                f.EnrollmentId == enrollmentId && f.Kind == StreakFreezeKind.Consumed
            );
        Assert.Equal(monday.AddDays(7), consumed.UsedOnLocalDate); // D+8
        // El consumo no re-otorga: solo la fila Consumed, sin Granted extra.
        Assert.Equal(
            1,
            await db.StreakFreezes.CountAsync(f =>
                f.EnrollmentId == enrollmentId && f.Kind == StreakFreezeKind.Granted
            )
        );
    }

    // ------------------------------------ PARITY live ↔ reconciliador (SPEC §17, enmienda)

    // El reconciliador nocturno (ReconcileStreaksAsync + ComputeStreakRuns) es
    // la fuente de verdad de (current, longest, lastActive). Cada escenario
    // conduce el path LIVE (completaciones reales vía CompleteTaskAsync) y
    // luego verifica 0 discrepancias: el valor almacenado por el live DEBE
    // coincidir con el modelo de corridas (antes el live guardaba 0 tras un
    // quiebre no rescatado y el job reescribía 1 cada 3 UTC).

    /// <summary>
    /// Caso real reportado: racha de 5 (Lun..Vie), Sáb+Dom perdidos sin
    /// congelamiento, 1 tarea el Lun siguiente → el live almacena 1 (corrida
    /// nueva) y el reconciliador NO encuentra deriva.
    /// </summary>
    [RequiresPostgresFact]
    public async Task Racha_Parity_CasoReal5DiasQuiebre_ReiniciaEn1_SinDeriva()
    {
        var enrollmentId = await EnrollAsync();
        var monday = _monday;

        for (var dayOffset = 0; dayOffset < 5; dayOffset++)
        {
            foreach (var (code, _) in TaskSeeds)
            {
                await CompleteAsync(enrollmentId, monday.AddDays(dayOffset), code, $"par-a-d{dayOffset}-{code}");
            }
        }

        // Sáb (D5) + Dom (D6) perdidos (sin congelamientos: 5 % 7 ≠ 0 → sin
        // inventario); Lun (D7) con UNA tarea (umbral 1 → calificado).
        await CompleteAsync(enrollmentId, monday.AddDays(7), TaskCode.podcast, "par-a-lun");

        await AssertStoredStreakAsync(enrollmentId, 1, 5, monday.AddDays(7), monday.AddDays(6));
        await AssertReconcileCleanAsync(enrollmentId, 1, 5, monday.AddDays(7));
    }

    /// <summary>
    /// Hueco de EXACTAMENTE 1 día con rescate: el día rescatado extiende la
    /// corrida almacenada y hoy la continúa → stored + 2 (5→7). El
    /// reconciliador cuenta el día rescatado como calificado
    /// (StreakFreezes.UsedOnLocalDate) → paridad.
    /// </summary>
    [RequiresPostgresFact]
    public async Task Racha_Parity_Hueco1DiaConRescate_7_SinDeriva()
    {
        var enrollmentId = await EnrollAsync();
        var monday = _monday;

        for (var dayOffset = 0; dayOffset < 5; dayOffset++)
        {
            foreach (var (code, _) in TaskSeeds)
            {
                await CompleteAsync(enrollmentId, monday.AddDays(dayOffset), code, $"par-b-d{dayOffset}-{code}");
            }
        }

        await SeedGrantedFreezeAsync(enrollmentId);

        // Sáb (D5) perdido → rescatable (plantilla sin códigos esenciales =
        // sin restricción); Dom (D6) perfecto: hueco de 1 día → 5 + 2 = 7.
        foreach (var (code, _) in TaskSeeds)
        {
            await CompleteAsync(enrollmentId, monday.AddDays(6), code, $"par-b-dom-{code}");
        }

        await AssertStoredStreakAsync(enrollmentId, 7, 7, monday.AddDays(6), null);
        await AssertReconcileCleanAsync(enrollmentId, 7, 7, monday.AddDays(6));
    }

    /// <summary>
    /// Hueco MULTI-día con rescate (política v1: la ventana evaluada es solo
    /// ayer): el día rescatado inicia una corrida NUEVA → 2 (rescatado + hoy);
    /// la corrida vieja (5) terminó en lastActive y no es contigua.
    /// </summary>
    [RequiresPostgresFact]
    public async Task Racha_Parity_HuecoMultiDiaConRescate_2_SinDeriva()
    {
        var enrollmentId = await EnrollAsync();
        var monday = _monday;

        for (var dayOffset = 0; dayOffset < 5; dayOffset++)
        {
            foreach (var (code, _) in TaskSeeds)
            {
                await CompleteAsync(enrollmentId, monday.AddDays(dayOffset), code, $"par-c-d{dayOffset}-{code}");
            }
        }

        await SeedGrantedFreezeAsync(enrollmentId);

        // Sáb (D5) + Dom (D6) perdidos; Lun (D7) perfecto → ayer (D6) se
        // rescata (solo la última ventana) → hueco de 2 días → current = 2.
        foreach (var (code, _) in TaskSeeds)
        {
            await CompleteAsync(enrollmentId, monday.AddDays(7), code, $"par-c-lun-{code}");
        }

        await AssertStoredStreakAsync(enrollmentId, 2, 5, monday.AddDays(7), null);
        await AssertReconcileCleanAsync(enrollmentId, 2, 5, monday.AddDays(7));
    }

    /// <summary>
    /// Quiebre sin congelamiento → reinicio en 1 (corrida nueva) y
    /// lastBreakDate = ayer; el reconciliador no encuentra deriva.
    /// </summary>
    [RequiresPostgresFact]
    public async Task Racha_Parity_QuiebreSinCongelamiento_ReiniciaEn1_SinDeriva()
    {
        var enrollmentId = await EnrollAsync();
        var monday = _monday;

        for (var dayOffset = 0; dayOffset < 2; dayOffset++)
        {
            foreach (var (code, _) in TaskSeeds)
            {
                await CompleteAsync(enrollmentId, monday.AddDays(dayOffset), code, $"par-d-d{dayOffset}-{code}");
            }
        }

        // D2 + D3 perdidos (sin congelamientos); D4 vuelve → current = 1.
        foreach (var (code, _) in TaskSeeds)
        {
            await CompleteAsync(enrollmentId, monday.AddDays(4), code, $"par-d-vuelta-{code}");
        }

        await AssertStoredStreakAsync(enrollmentId, 1, 2, monday.AddDays(4), monday.AddDays(3));
        await AssertReconcileCleanAsync(enrollmentId, 1, 2, monday.AddDays(4));
    }

    /// <summary>
    /// Doble quiebre (quiebre → vuelta → quiebre → vuelta): la paridad
    /// live↔reconciliador se mantiene en CADA paso (corridas de 1).
    /// </summary>
    [RequiresPostgresFact]
    public async Task Racha_Parity_DobleQuiebre_ParidadEnCadaPaso()
    {
        var enrollmentId = await EnrollAsync();
        var monday = _monday;

        // D0 calificado → current 1.
        await CompleteAsync(enrollmentId, monday, TaskCode.podcast, "par-e-d0");
        await AssertStoredStreakAsync(enrollmentId, 1, 1, monday, null);
        await AssertReconcileCleanAsync(enrollmentId, 1, 1, monday);

        // D1 perdido; D2 vuelve → 1 (corrida nueva).
        await CompleteAsync(enrollmentId, monday.AddDays(2), TaskCode.podcast, "par-e-d2");
        await AssertStoredStreakAsync(enrollmentId, 1, 1, monday.AddDays(2), monday.AddDays(1));
        await AssertReconcileCleanAsync(enrollmentId, 1, 1, monday.AddDays(2));

        // D3 perdido; D4 vuelve → 1 (corrida nueva).
        await CompleteAsync(enrollmentId, monday.AddDays(4), TaskCode.podcast, "par-e-d4");
        await AssertStoredStreakAsync(enrollmentId, 1, 1, monday.AddDays(4), monday.AddDays(3));
        await AssertReconcileCleanAsync(enrollmentId, 1, 1, monday.AddDays(4));
    }

    /// <summary>
    /// Idempotencia mismo día: una segunda completación del MISMO día no
    /// cambia la racha; el reconciliador sigue sin encontrar deriva.
    /// </summary>
    [RequiresPostgresFact]
    public async Task Racha_Parity_MismoDia_Idempotente_SinDeriva()
    {
        var enrollmentId = await EnrollAsync();
        var monday = _monday;

        await CompleteAsync(enrollmentId, monday, TaskCode.podcast, "par-f-1");
        await CompleteAsync(enrollmentId, monday, TaskCode.vitals, "par-f-2"); // mismo día, otra tarea

        await AssertStoredStreakAsync(enrollmentId, 1, 1, monday, null);
        await AssertReconcileCleanAsync(enrollmentId, 1, 1, monday);
    }

    /// <summary>
    /// Reproducción EXACTA de F1: corrida D0..D4 (5) → el job nocturno zeroea
    /// current CONSERVANDO lastActive (corrida vieja, ComputeStreakRuns solo
    /// es válida si termina hoy/ayer) → D5 perdido, D6 vuelve con rescate de
    /// D5. El current se DERIVA DEL MODELO (D0..D4 + D5 rescatado + D6 = 7),
    /// no del escalar (stored + 2 = 2 habría perdido STREAK_7 para siempre:
    /// una vez por inscripción).
    /// </summary>
    [RequiresPostgresFact]
    public async Task Racha_Parity_F1_JobZeroeadoConRescate_CurrentDelModelo7()
    {
        var enrollmentId = await EnrollAsync();
        var monday = _monday;

        for (var dayOffset = 0; dayOffset < 5; dayOffset++)
        {
            foreach (var (code, _) in TaskSeeds)
            {
                await CompleteAsync(enrollmentId, monday.AddDays(dayOffset), code, $"par-f1-d{dayOffset}-{code}");
            }
        }

        await SeedGrantedFreezeAsync(enrollmentId);
        await CraftZeroedStreakAsync(enrollmentId); // efecto del job (03:00 UTC)

        // D6 (hoy) = lastActive(D4) + 2 → hueco de 1 día; D5 (ayer) se rescata.
        foreach (var (code, _) in TaskSeeds)
        {
            await CompleteAsync(enrollmentId, monday.AddDays(6), code, $"par-f1-vuelta-{code}");
        }

        await AssertStoredStreakAsync(enrollmentId, 7, 7, monday.AddDays(6), null);
        await AssertReconcileCleanAsync(enrollmentId, 7, 7, monday.AddDays(6));
    }

    /// <summary>
    /// Fila legacy (path pre-fix) con stored = 0 y lastActive vigente sobre
    /// una corrida real de 5: la completación CONTIGUA siguiente deriva del
    /// modelo (corrida que termina en lastActive = 5 + 1 = 6), no de
    /// stored + 1 = 1. Cura la fila en un paso live.
    /// </summary>
    [RequiresPostgresFact]
    public async Task Racha_Parity_Legacy0_Contiguo_CurrentDelModelo6()
    {
        var enrollmentId = await EnrollAsync();
        var monday = _monday;

        for (var dayOffset = 0; dayOffset < 5; dayOffset++)
        {
            foreach (var (code, _) in TaskSeeds)
            {
                await CompleteAsync(enrollmentId, monday.AddDays(dayOffset), code, $"par-l0-d{dayOffset}-{code}");
            }
        }

        await CraftZeroedStreakAsync(enrollmentId); // fila legada (lastActive = D4 intacto)

        // D5 contiguo: fast path stored > 0 NO aplica → modelo 5 + 1 = 6.
        foreach (var (code, _) in TaskSeeds)
        {
            await CompleteAsync(enrollmentId, monday.AddDays(5), code, $"par-l0-d5-{code}");
        }

        await AssertStoredStreakAsync(enrollmentId, 6, 6, monday.AddDays(5), null);
        await AssertReconcileCleanAsync(enrollmentId, 6, 6, monday.AddDays(5));
    }

    /// <summary>
    /// Fila zeroeada por el job + vuelta SIN rescate (sin congelamientos):
    /// reinicia en 1 (ayer no calificó) y el reconciliador coincide.
    /// </summary>
    [RequiresPostgresFact]
    public async Task Racha_Parity_JobZeroeado_SinRescate_ReiniciaEn1()
    {
        var enrollmentId = await EnrollAsync();
        var monday = _monday;

        await CompleteAsync(enrollmentId, monday, TaskCode.podcast, "par-z-d0");
        await CraftZeroedStreakAsync(enrollmentId);

        // D1 perdido (sin congelamientos); D2 vuelve → 1.
        await CompleteAsync(enrollmentId, monday.AddDays(2), TaskCode.podcast, "par-z-d2");

        await AssertStoredStreakAsync(enrollmentId, 1, 1, monday.AddDays(2), monday.AddDays(1));
        await AssertReconcileCleanAsync(enrollmentId, 1, 1, monday.AddDays(2));
    }

    // ------------------------------------ Hitos de racha extendidos (módulo "cofres")

    [RequiresPostgresFact]
    public async Task Racha_Hito7_OtorgaStreak7UnaVezYSnapshotExponeCofres()
    {
        // 7 días perfectos consecutivos → racha 7 → hito STREAK_7 (una vez) y
        // el snapshot expone el trail de cofres con la verdad del libro mayor.
        var enrollmentId = await EnrollAsync();
        var monday = _monday;

        for (var dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var date = monday.AddDays(dayOffset);
            foreach (var (code, _) in TaskSeeds)
            {
                await CompleteAsync(enrollmentId, date, code, $"chest-d{dayOffset}-{code}");
            }
        }

        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.XpLedgerEntries.CountAsync(
            x => x.EnrollmentId == enrollmentId && x.Reason == XpReason.STREAK_7));
        Assert.Equal(100, await db.XpLedgerEntries
            .Where(x => x.EnrollmentId == enrollmentId && x.Reason == XpReason.STREAK_7)
            .Select(x => x.Amount)
            .SingleAsync());

        var repo = new ProgramRepository(db, Configuration());
        var snapshot = await repo.GetSnapshotAsync(enrollmentId, monday.AddDays(7));
        Assert.NotNull(snapshot);
        Assert.Equal(7, snapshot!.Streak.Current);

        // Trail completo del catálogo extendido, ordenado por días.
        var chests = snapshot.StreakChests!;
        Assert.Equal(8, chests.Count);
        Assert.Equal(new[] { 7, 11, 14, 22, 30, 50, 75, 100 }, chests.Select(c => c.Days).ToArray());
        Assert.Equal(new[] { 100, 200, 300, 500, 800, 1500, 2500, 5000 }, chests.Select(c => c.Xp).ToArray());

        // Solo el cofre de 7 días está otorgado (verdad del libro mayor).
        Assert.True(chests[0].Granted);
        Assert.NotNull(chests[0].GrantedAt);
        Assert.Equal(100, chests[0].Xp);
        Assert.True(chests.Skip(1).All(c => !c.Granted && c.GrantedAt is null));
    }

    [RequiresPostgresFact]
    public async Task Racha_Hito14_OtorgaStreak14UnaVezYNoActivaMultiplicador()
    {
        // 14 días perfectos consecutivos: cruza los hitos 7, 11 y 14. El x2 del
        // hito 11 dura 24h, así que al llegar al día 14 ya venció → STREAK_14 se
        // otorga con su XP base (300, sin multiplicador) y solo una vez.
        var enrollmentId = await EnrollAsync();
        var monday = _monday;

        for (var dayOffset = 0; dayOffset < 14; dayOffset++)
        {
            var date = monday.AddDays(dayOffset);
            foreach (var (code, _) in TaskSeeds)
            {
                await CompleteAsync(enrollmentId, date, code, $"chest14-d{dayOffset}-{code}");
            }
        }

        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.XpLedgerEntries.CountAsync(
            x => x.EnrollmentId == enrollmentId && x.Reason == XpReason.STREAK_14));
        Assert.Equal(300, await db.XpLedgerEntries
            .Where(x => x.EnrollmentId == enrollmentId && x.Reason == XpReason.STREAK_14)
            .Select(x => x.Amount)
            .SingleAsync());
        // Los hitos 7 y 11 se cruzaron en el camino, también una sola vez.
        Assert.Equal(1, await db.XpLedgerEntries.CountAsync(
            x => x.EnrollmentId == enrollmentId && x.Reason == XpReason.STREAK_7));
        Assert.Equal(1, await db.XpLedgerEntries.CountAsync(
            x => x.EnrollmentId == enrollmentId && x.Reason == XpReason.STREAK_11));
    }

    [RequiresPostgresFact]
    public async Task TareaEmocional_PersisteRegistroEmocionalYAnimoDelDia()
    {
        var enrollmentId = await EnrollAsync();
        var tuesday = _monday.AddDays(1);

        await using (var db = fixture.CreateDbContext())
        {
            var repo = new ProgramRepository(db, Configuration());
            await repo.CompleteTaskAsync(
                new CompleteTaskInput(
                    enrollmentId,
                    tuesday,
                    TaskCode.emocional,
                    "emo-1",
                    null,
                    MoodScore: 4,
                    Barriers: "fatiga",
                    null
                )
            );
        }

        await using var verifyDb = fixture.CreateDbContext();
        var completion = await verifyDb
            .TaskCompletions.AsNoTracking()
            .SingleAsync(t => t.EnrollmentId == enrollmentId && t.LocalDate == tuesday);
        Assert.NotNull(completion.EmotionalRecordId);

        var emotional = await verifyDb
            .EmotionalRecords.AsNoTracking()
            .SingleAsync(r =>
                r.ProgramEnrollmentId == enrollmentId && r.RecordedLocalDate == tuesday
            );
        Assert.Equal(4, emotional.MoodScore);
        Assert.Equal("fatiga", emotional.Barriers);
        Assert.Equal(_patientId, emotional.PatientId);

        var checkin = await verifyDb
            .DailyCheckIns.AsNoTracking()
            .SingleAsync(c => c.EnrollmentId == enrollmentId && c.LocalDate == tuesday);
        Assert.Equal((short?)4, checkin.MoodScore);
        Assert.Equal("fatiga", checkin.Barriers);
    }

    [RequiresPostgresFact]
    public async Task TareaEmocional_SinMoodScore_LanzaErrorSinFabricarDatoClinico()
    {
        var enrollmentId = await EnrollAsync();
        var tuesday = _monday.AddDays(1);

        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());

        var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            repo.CompleteTaskAsync(
                new CompleteTaskInput(
                    enrollmentId,
                    tuesday,
                    TaskCode.emocional,
                    "emo-nomood",
                    null,
                    MoodScore: null,
                    Barriers: null,
                    ContentFingerprint: null
                )
            )
        );
        Assert.Contains("MOOD_SCORE_REQUIRED", ex.Message);

        // Sin datos clínicos fabricados: ni registro emocional ni completación.
        Assert.Equal(
            0,
            await db.EmotionalRecords.CountAsync(r =>
                r.ProgramEnrollmentId == enrollmentId && r.RecordedLocalDate == tuesday
            )
        );
        Assert.Equal(
            0,
            await db.TaskCompletions.CountAsync(t =>
                t.EnrollmentId == enrollmentId && t.LocalDate == tuesday
            )
        );
    }

    // ---------------------------------------------------------------- B4: auditoría semántica

    [RequiresPostgresFact]
    public async Task WriteAuditRow_AdaptationApplied_PersisteYSePuedeLeer()
    {
        // BLOCKER B4-1: audit.activity_logs.action ensanchado a varchar(32);
        // 'AdaptationApplied' debe persistir y poder leerse de vuelta.
        var recordId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await using (var db = fixture.CreateDbContext())
        {
            var repo = new ProgramRepository(db, Configuration());
            await repo.WriteAuditRowAsync(
                "AdaptationApplied",
                "app",
                "adaptation_recommendations",
                recordId,
                actorId
            );
        }

        await using var verifyDb = fixture.CreateDbContext();
        var row = await verifyDb
            .ActivityLogs.AsNoTracking()
            .SingleAsync(l => l.RecordId == recordId.ToString());
        Assert.Equal(AuditAction.AdaptationApplied, row.Action);
        Assert.Equal("app", row.SchemaName);
        Assert.Equal("adaptation_recommendations", row.TableName);
        Assert.Equal(recordId.ToString(), row.RecordId);
        Assert.Equal(actorId, row.UserId);
    }

    // ---------------------------------------------------------------- B4: frontera de zona horaria

    [RequiresPostgresFact]
    public async Task ZonaUTCPlus_Tokyo_HoyLocalSeAceptaYRachaQuedaEnFechaLocal()
    {
        // CRITICAL B4-2/AC-14: paciente en Asia/Tokyo (UTC+9). El "hoy" local se
        // computa en la zona del paciente (SPEC §6.11), no en UTC: puede ser
        // MAÑANA en UTC. Una fecha que es hoy local pero mañana en UTC se
        // acepta para completar, y la racha queda en la fecha local.
        var enrollmentId = await EnrollAsync(timezone: "Asia/Tokyo");

        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());
        var localToday =
            await repo.GetPatientLocalTodayAsync(enrollmentId)
            ?? throw new InvalidOperationException("Inscripción sin zona.");

        var utcToday = DateOnly.FromDateTime(DateTime.UtcNow);
        // Asia/Tokyo nunca está detrás de UTC: hoy local >= hoy UTC, y a última
        // hora del día UTC ya es D+1 en Tokio (mañana en UTC).
        Assert.True(localToday >= utcToday, $"hoy local {localToday} < hoy UTC {utcToday}");
        Assert.True(
            localToday <= utcToday.AddDays(1),
            $"hoy local {localToday} > hoy UTC+1 {utcToday.AddDays(1)}"
        );

        // Día perfecto en la fecha local de hoy (aunque sea mañana en UTC).
        CompleteTaskResult? last = null;
        foreach (var (code, _) in TaskSeeds)
        {
            last = await repo.CompleteTaskAsync(
                new CompleteTaskInput(
                    enrollmentId,
                    localToday,
                    code,
                    $"tokyo-{code}",
                    null,
                    // La tarea emocional exige moodScore real (SPEC §3.10).
                    MoodScore: code == TaskCode.emocional ? (short?)4 : null,
                    Barriers: null,
                    ContentFingerprint: null
                )
            );
        }

        Assert.Equal(CompleteTaskOutcome.Created, last!.Outcome);
        // Streak math paciente-local: la racha registra la fecha local, no UTC.
        var streak = await db
            .StreakStates.AsNoTracking()
            .SingleAsync(s => s.EnrollmentId == enrollmentId);
        Assert.Equal(1, streak.CurrentStreak);
        Assert.Equal(localToday, streak.LastActiveDate);
    }

    // ---------------------------------------------------------------- B4: contenido pendiente (SPEC §7.1)

    [RequiresPostgresFact]
    public async Task GetSnapshot_TareaPodcastPendiente_MuestraContenidoFallbackDePlantilla()
    {
        // WARNING B4-3 (SPEC §4.4 y §7.1): una tarea podcast PENDIENTE muestra el
        // contenido multimedia del fallback template-level
        // (weekly_day_templates.media_id), no solo las completadas.
        var media = new MediaItem
        {
            Id = Guid.NewGuid(),
            Title = "Podcast semana 1 - martes",
            Author = "Equipo clínico",
            MediaType = MediaType.Podcast,
            Category = MediaCategory.Biologia,
            StorageKey = $"media/podcasts/{Guid.NewGuid():N}.mp3",
            DurationSecs = 492,
            Status = MediaStatus.Published,
            SortOrder = 1,
            Day = 1,
            Month = 1,
        };
        await using (var seedDb = fixture.CreateDbContext())
        {
            seedDb.MediaItems.Add(media);
            await seedDb.SaveChangesAsync();

            var tuesdayTemplate = await seedDb.WeeklyDayTemplates.SingleAsync(d =>
                d.TemplateId == _templateId && d.Weekday == 2 && d.TaskCode == TaskCode.podcast
            );
            tuesdayTemplate.MediaId = media.Id;
            await seedDb.SaveChangesAsync();
        }

        var enrollmentId = await EnrollAsync();
        var tuesday = _monday.AddDays(1);

        // Sin completar NADA: el podcast del martes queda Pending y debe traer
        // su contenido resuelto desde el fallback de plantilla.
        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());
        var snapshot = await repo.GetSnapshotAsync(enrollmentId, tuesday);

        Assert.NotNull(snapshot);
        var pending = snapshot!.TodayTasks.Single(t => t.TaskCode == TaskCode.podcast);
        Assert.Equal("Pending", pending.Status);
        Assert.NotNull(pending.Content);
        Assert.Equal(media.Id, pending.Content!.MediaId);
        Assert.Equal("Podcast semana 1 - martes", pending.Content.Title);
        Assert.Equal(492, pending.Content.DurationSecs);
    }

    // ---------------------------------------------------------------- Vital Signs Tracking (vital-signs-tracking)

    [RequiresPostgresFact]
    public async Task CompleteTask_Vitals_6Campos_7Filas_Y_FKApuntaAlAncla()
    {
        var patientId = await CreatePatientAsync(fixture.CreateDbContext(), "VS", "Seis");
        var enrollmentId = await EnrollAsync(patientId: patientId);
        var tuesday = _monday.AddDays(1);

        var result = await CompleteVitalsAsync(
            enrollmentId, tuesday, "vs-1",
            new VitalsPayload(72, 120, 80, 98, 130m, 70m, 36.5m, null));

        Assert.Equal(CompleteTaskOutcome.Created, result.Outcome);

        await using var db = fixture.CreateDbContext();
        // 6 campos → 7 filas (systolic + diastolic split) en app.clinical_measurements.
        Assert.Equal(
            7,
            await db.ClinicalMeasurements.CountAsync(m => m.PatientId == patientId)
        );

        var codes = await db.ClinicalMeasurements
            .Where(m => m.PatientId == patientId)
            .Select(m => m.Metric.Code)
            .ToListAsync();
        Assert.Contains("heart_rate", codes);
        Assert.Contains("systolic_bp", codes);
        Assert.Contains("diastolic_bp", codes);
        Assert.Contains("o2_saturation", codes);
        Assert.Contains("glucose_fasting", codes);
        Assert.Contains("weight", codes);
        Assert.Contains("temperature_c", codes);

        // EncounterId null (monitoreo autónomo, ADR-002) y Source = 'mobile'.
        Assert.True(await db.ClinicalMeasurements
            .Where(m => m.PatientId == patientId)
            .AllAsync(m => m.EncounterId == null && m.Source == "mobile"));

        // El completion referencia al ancla (primera fila = heart_rate, orden fijo).
        var completion = await db.TaskCompletions.SingleAsync(t =>
            t.EnrollmentId == enrollmentId && t.LocalDate == tuesday && t.TaskCode == TaskCode.vitals);
        var anchor = await db.ClinicalMeasurements.SingleAsync(m =>
            m.PatientId == patientId && m.Metric.Code == "heart_rate");
        Assert.Equal(anchor.Id, completion.VitalSignsBatchId);
    }

    [RequiresPostgresFact]
    public async Task CompleteTask_Vitals_SinPayload_0Filas()
    {
        var patientId = await CreatePatientAsync(fixture.CreateDbContext(), "VS", "Vacio");
        var enrollmentId = await EnrollAsync(patientId: patientId);
        var tuesday = _monday.AddDays(1);

        await CompleteVitalsAsync(enrollmentId, tuesday, "vs-empty", null);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(
            0,
            await db.ClinicalMeasurements.CountAsync(m => m.PatientId == patientId)
        );
    }

    [RequiresPostgresFact]
    public async Task CompleteTask_Vitals_ReplayMismoClientRequestId_SinFilasDuplicadas()
    {
        var patientId = await CreatePatientAsync(fixture.CreateDbContext(), "VS", "Replay");
        var enrollmentId = await EnrollAsync(patientId: patientId);
        var tuesday = _monday.AddDays(1);

        await CompleteVitalsAsync(enrollmentId, tuesday, "vs-replay",
            new VitalsPayload(72, 120, 80, 98, 130m, 70m, 36.5m, null));
        await CompleteVitalsAsync(enrollmentId, tuesday, "vs-replay",
            new VitalsPayload(72, 120, 80, 98, 130m, 70m, 36.5m, null));

        await using var db = fixture.CreateDbContext();
        // Replay idempotente (misma clientRequestId) → no duplica filas (7, no 14).
        Assert.Equal(
            7,
            await db.ClinicalMeasurements.CountAsync(m => m.PatientId == patientId)
        );
    }

    [RequiresPostgresFact]
    public async Task CompleteTask_Vitals_MetricaInactiva_Lanza422_Y_0Filas()
    {
        var patientId = await CreatePatientAsync(fixture.CreateDbContext(), "VS", "Unseed");
        var enrollmentId = await EnrollAsync(patientId: patientId);
        var tuesday = _monday.AddDays(1);

        // Simula "no sembrada": desactiva una métrica del catálogo.
        await using (var db = fixture.CreateDbContext())
        {
            var metric = await db.MeasurementMetrics.SingleAsync(m => m.Code == "temperature_c");
            metric.IsActive = false;
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateDbContext())
        {
            var repo = new ProgramRepository(db, Configuration());
            var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
                repo.CompleteTaskAsync(new CompleteTaskInput(
                    enrollmentId, tuesday, TaskCode.vitals, "vs-unseed", null,
                    null, null, null, Vitals: new VitalsPayload(72, 120, 80, 98, 130m, 70m, 36.5m, null))));
            Assert.Contains("VITALS_METRIC_NOT_SEEDED", ex.Message);
        }

        await using (var db = fixture.CreateDbContext())
        {
            Assert.Equal(0, await db.ClinicalMeasurements.CountAsync(m => m.PatientId == patientId));
        }

        // Restaura la métrica para no afectar otros tests de la clase.
        await using (var db = fixture.CreateDbContext())
        {
            var metric = await db.MeasurementMetrics.SingleAsync(m => m.Code == "temperature_c");
            metric.IsActive = true;
            await db.SaveChangesAsync();
        }
    }

    [RequiresPostgresFact]
    public async Task CompleteTask_Vitals_TemperaturaC_UsaUnidadCelsius()
    {
        var patientId = await CreatePatientAsync(fixture.CreateDbContext(), "VS", "Unit");
        var enrollmentId = await EnrollAsync(patientId: patientId);
        var tuesday = _monday.AddDays(1);

        await CompleteVitalsAsync(enrollmentId, tuesday, "vs-unit",
            new VitalsPayload(null, null, null, null, null, null, 36.5m, null));

        await using var db = fixture.CreateDbContext();
        var celsius = await db.UnitOfMeasures.SingleAsync(u => u.Code == "celsius");
        var tempRow = await db.ClinicalMeasurements.SingleAsync(m =>
            m.PatientId == patientId && m.Metric.Code == "temperature_c");
        // UnitId se resuelve desde DefaultUnitId de la métrica (celsius).
        Assert.Equal(celsius.Id, tempRow.UnitId);
    }

    [RequiresPostgresFact]
    public async Task GetSnapshot_RecentVitals_PobladoTrasVitals()
    {
        var patientId = await CreatePatientAsync(fixture.CreateDbContext(), "VS", "Snap");
        var enrollmentId = await EnrollAsync(patientId: patientId);
        var tuesday = _monday.AddDays(1);
        await CompleteVitalsAsync(enrollmentId, tuesday, "vs-snap",
            new VitalsPayload(72, 120, 80, 98, 130m, 70m, 36.5m, null));

        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());
        var snapshot = await repo.GetSnapshotAsync(enrollmentId, tuesday);

        Assert.NotNull(snapshot);
        var vitalsTask = snapshot!.TodayTasks.Single(t => t.TaskCode == TaskCode.vitals);
        Assert.NotNull(vitalsTask.Content);
        Assert.NotNull(vitalsTask.Content!.RecentVitals);
        Assert.Equal(72, vitalsTask.Content.RecentVitals!.HeartRate);
        Assert.Equal(130m, vitalsTask.Content.RecentVitals.Glucose);
    }

    [RequiresPostgresFact]
    public async Task GetSnapshot_RecentVitals_NuloSinMediciones()
    {
        // Paciente aislado sin mediciones → recentVitals null.
        var patientId = await CreatePatientAsync(fixture.CreateDbContext(), "VS", "Null");
        var enrollmentId = await EnrollAsync(patientId: patientId);
        var tuesday = _monday.AddDays(1);
        await CompleteAsync(enrollmentId, tuesday, TaskCode.podcast, "vs-snap-null");

        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());
        var snapshot = await repo.GetSnapshotAsync(enrollmentId, tuesday);

        Assert.NotNull(snapshot);
        var vitalsTask = snapshot!.TodayTasks.Single(t => t.TaskCode == TaskCode.vitals);
        Assert.NotNull(vitalsTask.Content);
        Assert.Null(vitalsTask.Content!.RecentVitals);
    }

    [RequiresPostgresFact]
    public async Task FK_VitalSignsBatch_ApuntaACatalogClinicalMeasurements()
    {
        // Verificación de esquema real: el FK retargeteado (D2) apunta a
        // app.clinical_measurements(id), no al legacy app.vital_signs.
        await using var db = fixture.CreateDbContext();
        var referencedTable = await db.Database
            .SqlQuery<string>($"""
                SELECT ccu.table_name AS "Value"
                FROM information_schema.table_constraints tc
                JOIN information_schema.constraint_column_usage ccu
                  ON tc.constraint_name = ccu.constraint_name
                WHERE tc.constraint_type = 'FOREIGN KEY'
                  AND tc.table_schema = 'app'
                  AND tc.table_name = 'task_completions'
                  -- EF Core emite nombres de constraint entre comillas: el
                  -- nombre real conserva mayúsculas (FK_...), por eso la
                  -- comparación es case-insensitive.
                  AND LOWER(tc.constraint_name) = 'fk_task_completions_clinical_measurements_vital_signs_batch_id'
                """)
            .SingleOrDefaultAsync();
        Assert.Equal("clinical_measurements", referencedTable);
    }

    // ---------------------------------------------------------------- Helpers

    private async Task<Guid> EnrollAsync(
        Guid? templateId = null,
        DateOnly? start = null,
        Guid? patientId = null,
        string? timezone = null
    )
    {
        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());
        var enrollment = await repo.EnrollAsync(
            patientId ?? _patientId,
            templateId ?? _templateId,
            timezone ?? "America/Bogota",
            start ?? _monday
        );
        return enrollment.Id;
    }

    private async Task<CompleteTaskResult> CompleteAsync(
        Guid enrollmentId,
        DateOnly date,
        TaskCode code,
        string? requestId = null
    )
    {
        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());
        return await repo.CompleteTaskAsync(
            new CompleteTaskInput(
                enrollmentId,
                date,
                code,
                requestId,
                null,
                // La tarea emocional exige moodScore real (SPEC §3.10): el repo
                // ya no fabrica el default 3 (MOOD_SCORE_REQUIRED).
                MoodScore: code == TaskCode.emocional ? (short?)4 : null,
                Barriers: null,
                ContentFingerprint: null
            )
        );
    }

    private async Task<CompleteTaskResult> CompleteVitalsAsync(
        Guid enrollmentId,
        DateOnly date,
        string requestId,
        VitalsPayload? vitals
    )
    {
        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());
        return await repo.CompleteTaskAsync(
            new CompleteTaskInput(
                enrollmentId,
                date,
                TaskCode.vitals,
                requestId,
                null,
                null,
                Barriers: null,
                ContentFingerprint: null,
                Vitals: vitals
            )
        );
    }

    private static async Task ResetProgramTablesAsync(AppDbContext db) =>
        await db.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE app.task_completions, app.xp_ledger, app.daily_checkins, app.streak_states,
                     app.streak_freezes, app.emotional_records, app.program_weeks,
                     app.program_enrollments, app.adaptation_recommendations,
                     app.weekly_day_templates, app.program_templates, app.patient_profiles CASCADE;
            """
        );

    /// <summary>
    /// Crea (si falta) o actualiza la regla DAY_BONUS del catálogo aplicando
    /// <paramref name="configure"/> sobre la fila. La regla creada arranca
    /// vigente con base null (fallback 50) y multiplicador 1.0; cada test
    /// configura su escenario completo (base/active/ventana/multiplier).
    /// Idempotente e independiente del orden de ejecución entre tests.
    /// </summary>
    private async Task SeedDayBonusRuleAsync(Action<XpRule> configure)
    {
        await using var db = fixture.CreateDbContext();
        var rule = await db.XpRules.SingleOrDefaultAsync(r => r.Code == XpRuleCodes.DayBonus);
        if (rule is null)
        {
            rule = new XpRule
            {
                Id = Guid.NewGuid(),
                Code = XpRuleCodes.DayBonus,
                Name = "Bonus día perfecto",
                Category = "adherence",
                BaseXp = null,
                Multiplier = 1.0m,
                Active = true,
                ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                CreatedAt = DateTime.UtcNow,
            };
            db.XpRules.Add(rule);
        }

        configure(rule);
        await db.SaveChangesAsync();
    }

    /// <summary>Elimina la regla DAY_BONUS (fallback 50, SPEC §14.3). Idempotente.</summary>
    private async Task DeleteDayBonusRuleAsync()
    {
        await using var db = fixture.CreateDbContext();
        await db.XpRules.Where(r => r.Code == XpRuleCodes.DayBonus).ExecuteDeleteAsync();
    }

    private static async Task<Guid> SeedDefaultTemplateAsync(AppDbContext db)
    {
        var template = new ProgramTemplate
        {
            Id = Guid.NewGuid(),
            Code = "default-83w",
            Name = "Programa 83 semanas",
            TotalWeeks = 83,
            Status = TemplateStatus.Active,
            Version = 1,
        };
        for (short weekday = 1; weekday <= 7; weekday++)
        {
            for (var i = 0; i < TaskSeeds.Length; i++)
            {
                template.DayTemplates.Add(
                    new WeeklyDayTemplate
                    {
                        Weekday = weekday,
                        TaskCode = TaskSeeds[i].Code,
                        Points = TaskSeeds[i].Points,
                        SortOrder = i + 1,
                    }
                );
            }
        }

        db.ProgramTemplates.Add(template);
        await db.SaveChangesAsync();
        return template.Id;
    }

    /// <summary>
    /// Plantilla "sparse": el lunes (weekday 1) solo tiene podcast y vitals;
    /// el resto de días las 6 tareas. Sirve para el 409 TASK_NOT_SCHEDULED.
    /// </summary>
    private async Task<Guid> SeedSparseTemplateAsync()
    {
        await using var db = fixture.CreateDbContext();
        var template = new ProgramTemplate
        {
            Id = Guid.NewGuid(),
            Code = "sparse",
            Name = "Plantilla sparse",
            TotalWeeks = 12,
            Status = TemplateStatus.Active,
            Version = 1,
        };
        for (short weekday = 1; weekday <= 7; weekday++)
        {
            var codes =
                weekday == 1
                    ? new[] { TaskCode.podcast, TaskCode.vitals }
                    : TaskSeeds.Select(s => s.Code).ToArray();
            var sort = 1;
            foreach (var code in codes)
            {
                template.DayTemplates.Add(
                    new WeeklyDayTemplate
                    {
                        Weekday = weekday,
                        TaskCode = code,
                        Points = TaskSeeds.Single(s => s.Code == code).Points,
                        SortOrder = sort++,
                    }
                );
            }
        }

        db.ProgramTemplates.Add(template);
        await db.SaveChangesAsync();
        return template.Id;
    }

    private static async Task<Guid> CreatePatientAsync(
        AppDbContext db,
        string firstName,
        string lastName
    )
    {
        var patient = new PatientProfile
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
        };
        db.PatientProfiles.Add(patient);
        await db.SaveChangesAsync();
        return patient.Id;
    }

    /// <summary>
    /// Valor almacenado por el path LIVE tras un escenario (parity suite):
    /// (current, longest, lastActive, lastBreak) esperados.
    /// </summary>
    private async Task AssertStoredStreakAsync(
        Guid enrollmentId,
        int expectedCurrent,
        int expectedLongest,
        DateOnly expectedLast,
        DateOnly? expectedLastBreak)
    {
        await using var db = fixture.CreateDbContext();
        var streak = await db
            .StreakStates.AsNoTracking()
            .SingleAsync(s => s.EnrollmentId == enrollmentId);
        Assert.Equal(expectedCurrent, streak.CurrentStreak);
        Assert.Equal(expectedLongest, streak.LongestStreak);
        Assert.Equal(expectedLast, streak.LastActiveDate);
        Assert.Equal(expectedLastBreak, streak.LastBreakDate);
    }

    /// <summary>
    /// Ejecuta el reconciliador nocturno sobre el estado live y exige 0
    /// discrepancias + 0 filas corregidas, y que el valor almacenado sea
    /// exactamente el del modelo de corridas (parity live↔reconciliador).
    /// </summary>
    private async Task AssertReconcileCleanAsync(
        Guid enrollmentId,
        int expectedCurrent,
        int expectedLongest,
        DateOnly expectedLast)
    {
        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());
        var summary = await repo.ReconcileStreaksAsync();

        Assert.Empty(summary.Discrepancies);
        Assert.Equal(0, summary.Fixed);

        var streak = await db
            .StreakStates.AsNoTracking()
            .SingleAsync(s => s.EnrollmentId == enrollmentId);
        Assert.Equal(expectedCurrent, streak.CurrentStreak);
        Assert.Equal(expectedLongest, streak.LongestStreak);
        Assert.Equal(expectedLast, streak.LastActiveDate);
    }

    /// <summary>
    /// Siembra un congelamiento OTORGADO (fila + inventario) para los
    /// escenarios de rescate de la parity suite.
    /// </summary>
    private async Task SeedGrantedFreezeAsync(Guid enrollmentId, int count = 1)
    {
        await using var db = fixture.CreateDbContext();
        var streak = await db
            .StreakStates.SingleAsync(s => s.EnrollmentId == enrollmentId);
        for (var i = 0; i < count; i++)
        {
            db.StreakFreezes.Add(
                new StreakFreeze
                {
                    EnrollmentId = enrollmentId,
                    Kind = StreakFreezeKind.Granted,
                    GrantedReason = "TestSeed",
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        streak.FreezesRemaining += count;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Simula el efecto del job nocturno (o una fila legada del path pre-fix):
    /// <c>current_streak = 0</c> CONSERVANDO <c>last_active_date</c> (el
    /// reconciliador zeroea cuando la corrida terminó antes de ayer).
    /// </summary>
    private async Task CraftZeroedStreakAsync(Guid enrollmentId)
    {
        await using var db = fixture.CreateDbContext();
        await db
            .StreakStates.Where(s => s.EnrollmentId == enrollmentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CurrentStreak, 0));
    }

    /// <summary>
    /// Fija el estado de racha de la inscripción (la fila la crea EnrollAsync):
    /// escalar, longest, congelamientos y último día calificado. ExecuteUpdate
    /// para no arrastrar tracking de navegaciones.
    /// </summary>
    private async Task SeedStreakStateAsync(
        Guid enrollmentId,
        int current,
        int longest,
        int freezes,
        DateOnly? lastActive
    )
    {
        await using var db = fixture.CreateDbContext();
        await db
            .StreakStates.Where(s => s.EnrollmentId == enrollmentId)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(x => x.CurrentStreak, current)
                    .SetProperty(x => x.LongestStreak, longest)
                    .SetProperty(x => x.FreezesRemaining, freezes)
                    .SetProperty(x => x.LastActiveDate, lastActive)
            );
    }

    private static IReadOnlyList<(short Weekday, string TaskCode, int Points)> ParseSnapshot(
        JsonElement snapshot
    )
    {
        var result = new List<(short, string, int)>();
        foreach (var item in snapshot.EnumerateArray())
        {
            result.Add(
                (
                    (short)item.GetProperty("weekday").GetInt32(),
                    item.GetProperty("task_code").GetString() ?? string.Empty,
                    item.GetProperty("points").GetInt32()
                )
            );
        }

        return result;
    }

    private static DateOnly ThisMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-daysSinceMonday);
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Program:Streak:FreezeGrantEveryPerfectDays"] = "7",
                }
            )
            .Build();
}

/// <summary>
/// Fact condicionado a PostgreSQL real: si <c>COP_TEST_DB_CONNECTION</c> no
/// está definida, el test se reporta como SKIPPED en el runner (no pasa en
/// verde silenciosamente como hacía el patrón <c>if (fixture.Skipped) return;</c>).
/// El skip se evalúa en discovery (proceso estable durante la corrida), con la
/// misma variable que usa <see cref="ProgramRepositoryTestDb"/>.
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
/// Regla PURA de racha efectiva (decay en lectura): el escalar almacenado solo
/// es válido si el último día calificado es HOY o AYER local — la misma regla
/// que <c>ComputeStreakRuns</c>. Sin BD: tabla de verdad completa.
/// </summary>
public sealed class EffectiveCurrentStreakTests
{
    private static readonly DateOnly Today = new(2026, 9, 14);

    [Fact]
    public void EffectiveCurrentStreak_UltimoDiaHoy_ConservaElEscalar() =>
        Assert.Equal(5, ProgramRepository.EffectiveCurrentStreak(5, Today, Today));

    [Fact]
    public void EffectiveCurrentStreak_UltimoDiaAyer_ConservaElEscalar() =>
        Assert.Equal(5, ProgramRepository.EffectiveCurrentStreak(5, Today.AddDays(-1), Today));

    [Fact]
    public void EffectiveCurrentStreak_UltimoDiaAnteayer_RompeLaRacha() =>
        Assert.Equal(0, ProgramRepository.EffectiveCurrentStreak(5, Today.AddDays(-2), Today));

    [Fact]
    public void EffectiveCurrentStreak_SinUltimoDia_RompeLaRacha() =>
        Assert.Equal(0, ProgramRepository.EffectiveCurrentStreak(5, null, Today));

    [Fact]
    public void EffectiveCurrentStreak_EscalarCero_QuedaEnCero() =>
        Assert.Equal(0, ProgramRepository.EffectiveCurrentStreak(0, Today, Today));
}
