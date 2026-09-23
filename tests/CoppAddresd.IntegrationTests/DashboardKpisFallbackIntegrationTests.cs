using System.Text.Json;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Api.Seeders;
using CoppAddresd.Application.Features.Dashboard;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.IntegrationTests;

/// <summary>
/// KPIs del dashboard Home con PostgreSQL real (COP_TEST_DB_CONNECTION):
/// verifica que los bloques de tests de salud, inventario y programa caen al
/// conteo OLTP de la ventana cuando el rollup está vacío (backfill pendiente),
/// y que el rollup tiene prioridad cuando tiene filas (nunca se mezclan).
/// Todo el fixture vive en una transacción que se revierte; si la variable no
/// está definida, los tests se saltan.
/// </summary>
public sealed class DashboardKpisFallbackIntegrationTests : IAsyncLifetime
{
    private const string EnvVar = "COP_TEST_DB_CONNECTION";

    private readonly string _connectionString = Environment.GetEnvironmentVariable(EnvVar)!;
    private bool _skipped;
    private AppDbContext _db = null!;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _transaction = null!;
    private DashboardController _controller = null!;

    /// <summary>Hoy en el calendario del servidor (misma base que la ventana del controller).</summary>
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Mediodía UTC de un día: cae dentro del mismo día calendario en UTC.</summary>
    private static DateTime NoonUtc(DateOnly day) =>
        day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _skipped = true;
            return;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        _db = new AppDbContext(options);
        _transaction = await _db.Database.BeginTransactionAsync();

        var scopeFactory = new ServiceCollection()
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        _controller = new DashboardController(
            _db,
            new MetricsBackfillSeeder(scopeFactory, NullLogger<MetricsBackfillSeeder>.Instance)
        );
    }

    public async Task DisposeAsync()
    {
        if (_skipped)
        {
            return;
        }

        await _transaction.RollbackAsync();
        await _db.DisposeAsync();
    }

    private bool IsSkipped => _skipped;

    private async Task<DashboardKpisDto> GetKpisAsync(int days = 30)
    {
        var result = await _controller.GetKpis(days, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<DashboardKpisDto>(ok.Value);
    }

    /// <summary>
    /// Deja sin filas las claves de rollup de los tres bloques (dentro de la
    /// transacción del test): simula el backfill pendiente o una BD recién creada.
    /// </summary>
    private async Task ClearRollupAsync()
    {
        await _db
            .HealthTestDailyMetrics.Where(m =>
                m.MetricKey == "assignments_count" || m.MetricKey == "evaluations_count"
            )
            .ExecuteDeleteAsync();
        await _db
            .InventoryDailyMetrics.Where(m =>
                m.MetricKey == "entries_count" || m.MetricKey == "exits_count"
            )
            .ExecuteDeleteAsync();
        await _db
            .ProgramDailyMetrics.Where(m => m.MetricKey == "tasks_completed_today")
            .ExecuteDeleteAsync();
    }

    private async Task<PatientProfile> NewPatientAsync(string doc)
    {
        var patient = new PatientProfile
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = doc,
            DocumentNumber = doc,
            Status = "Activo",
            CreatedAt = DateTime.UtcNow,
        };
        _db.PatientProfiles.Add(patient);
        await _db.SaveChangesAsync();
        return patient;
    }

    private Task<Guid> FirstHealthTestVersionIdAsync() =>
        _db.HealthTestVersions.AsNoTracking().Select(v => v.Id).FirstAsync();

    private async Task SeedHealthAssignmentAsync(
        Guid patientId,
        Guid versionId,
        HealthTestAssignmentStatus status,
        DateTime? completedAt,
        DateTime assignedAt
    )
    {
        _db.HealthTestAssignments.Add(
            new HealthTestAssignment
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                VersionId = versionId,
                Status = status,
                CompletedAt = completedAt,
                AssignedAt = assignedAt,
            }
        );
        await _db.SaveChangesAsync();
    }

    private async Task SeedInventoryEntryAsync(DateOnly date)
    {
        _db.InventoryEntries.Add(
            new InventoryEntry
            {
                Id = Guid.NewGuid(),
                Reference = $"ENT-{Guid.NewGuid():N}"[..20],
                Date = date.ToDateTime(TimeOnly.MinValue),
                Reason = InventoryEntryReasons.Compra,
                CreatedAt = DateTime.UtcNow,
            }
        );
        await _db.SaveChangesAsync();
    }

    private async Task SeedInventoryExitAsync(DateOnly date)
    {
        _db.InventoryExits.Add(
            new InventoryExit
            {
                Id = Guid.NewGuid(),
                Reference = $"SAL-{Guid.NewGuid():N}"[..20],
                Date = date.ToDateTime(TimeOnly.MinValue),
                Reason = InventoryExitReasons.Venta,
                CreatedAt = DateTime.UtcNow,
            }
        );
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Siembra la cadena mínima de programa (inscripción → semana → check-in →
    /// task_completion). Cada llamada usa su propio paciente porque solo puede
    /// existir una inscripción activa por paciente.
    /// </summary>
    private async Task SeedProgramTaskAsync(Guid patientId, DateOnly localDate, DateTime completedAt)
    {
        var templateId = await _db.ProgramTemplates.AsNoTracking().Select(t => t.Id).FirstAsync();

        var enrollment = new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            TemplateId = templateId,
            Timezone = "America/Bogota",
            Status = ProgramEnrollmentStatus.Active,
            StartedAt = completedAt,
            StartLocalDate = localDate,
            CurrentWeekNumber = 1,
        };
        _db.ProgramEnrollments.Add(enrollment);

        // Snapshot congelado de la semana (jsonb; el contenido no importa aquí).
        using var snapshot = JsonDocument.Parse("[]");
        var week = new ProgramWeek
        {
            Id = Guid.NewGuid(),
            EnrollmentId = enrollment.Id,
            WeekNumber = 1,
            Status = ProgramWeekStatus.Active,
            WeekStartDateLocal = localDate,
            WeekEndDateLocal = localDate.AddDays(6),
            TasksSnapshot = snapshot.RootElement,
            TemplateVersionAtStart = 1,
        };
        _db.ProgramWeeks.Add(week);

        var weekday = (short)(
            localDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)localDate.DayOfWeek
        );
        var checkin = new DailyCheckIn
        {
            Id = Guid.NewGuid(),
            EnrollmentId = enrollment.Id,
            ProgramWeekId = week.Id,
            LocalDate = localDate,
            Weekday = weekday,
            TotalPoints = 100,
        };
        _db.DailyCheckIns.Add(checkin);

        _db.TaskCompletions.Add(
            new TaskCompletion
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollment.Id,
                ProgramWeekId = week.Id,
                DailyCheckinId = checkin.Id,
                LocalDate = localDate,
                Weekday = weekday,
                TaskCode = TaskCode.ejercicio,
                PointsAwarded = 150,
                CompletedAt = completedAt,
                SourceRefType = "manual",
            }
        );
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Rollup vacío + filas OLTP: los KPIs deben derivar los conteos de las tablas
    /// transaccionales con la semántica del backfill (status completed fechado por
    /// CompletedAt, con fallback a AssignedAt; documentos de inventario por date;
    /// task_completions por completed_at). La BD compartida puede tener filas
    /// previas en la ventana, por lo que se compara el delta antes/después de sembrar.
    /// </summary>
    [Fact]
    public async Task Kpis_SinRollup_UsaFallbackOltp()
    {
        if (IsSkipped)
            return;

        await ClearRollupAsync();
        var before = await GetKpisAsync();

        var today = Today;
        var yesterday = today.AddDays(-1);

        // Tests de salud: 2 completadas hoy, 1 ayer, 1 completada sin CompletedAt
        // (fechada por AssignedAt hoy), 1 pendiente (excluida) y 1 fuera de ventana.
        var patient = await NewPatientAsync($"kpis-ht-{Guid.NewGuid():N}");
        var versionId = await FirstHealthTestVersionIdAsync();
        await SeedHealthAssignmentAsync(
            patient.Id,
            versionId,
            HealthTestAssignmentStatus.completed,
            NoonUtc(today),
            NoonUtc(today)
        );
        await SeedHealthAssignmentAsync(
            patient.Id,
            versionId,
            HealthTestAssignmentStatus.completed,
            NoonUtc(today),
            NoonUtc(today)
        );
        await SeedHealthAssignmentAsync(
            patient.Id,
            versionId,
            HealthTestAssignmentStatus.completed,
            NoonUtc(yesterday),
            NoonUtc(yesterday)
        );
        await SeedHealthAssignmentAsync(
            patient.Id,
            versionId,
            HealthTestAssignmentStatus.completed,
            null,
            NoonUtc(today)
        );
        await SeedHealthAssignmentAsync(
            patient.Id,
            versionId,
            HealthTestAssignmentStatus.pending,
            null,
            NoonUtc(today)
        );
        await SeedHealthAssignmentAsync(
            patient.Id,
            versionId,
            HealthTestAssignmentStatus.completed,
            NoonUtc(today.AddDays(-40)),
            NoonUtc(today.AddDays(-40))
        );

        // Inventario: 2 entradas hoy y 1 salida ayer dentro de la ventana,
        // más un documento de cada tipo fuera de la ventana (excluidos).
        await SeedInventoryEntryAsync(today);
        await SeedInventoryEntryAsync(today);
        await SeedInventoryExitAsync(yesterday);
        await SeedInventoryEntryAsync(today.AddDays(-40));
        await SeedInventoryExitAsync(today.AddDays(-40));

        // Programa: 1 tarea completada hoy y 1 ayer.
        var programPatientToday = await NewPatientAsync($"kpis-prog-hoy-{Guid.NewGuid():N}");
        var programPatientYesterday = await NewPatientAsync(
            $"kpis-prog-ayer-{Guid.NewGuid():N}"
        );
        await SeedProgramTaskAsync(programPatientToday.Id, today, NoonUtc(today));
        await SeedProgramTaskAsync(programPatientYesterday.Id, yesterday, NoonUtc(yesterday));

        var after = await GetKpisAsync();

        Assert.Equal(4, after.HealthTests30d - before.HealthTests30d);
        Assert.Equal(2, after.InventoryEntries30d - before.InventoryEntries30d);
        Assert.Equal(1, after.InventoryExits30d - before.InventoryExits30d);
        Assert.Equal(2, after.ProgramTasks30d - before.ProgramTasks30d);

        // Serie diaria: mediodía UTC cae en el mismo día calendario.
        var todayPoint = after.ActivitySeries30d[^1];
        var yesterdayPoint = after.ActivitySeries30d[^2];
        Assert.Equal(3, todayPoint.Tests - before.ActivitySeries30d[^1].Tests);
        Assert.Equal(2, todayPoint.Entries - before.ActivitySeries30d[^1].Entries);
        Assert.Equal(0, todayPoint.Exits - before.ActivitySeries30d[^1].Exits);
        Assert.Equal(1, todayPoint.ProgramTasks - before.ActivitySeries30d[^1].ProgramTasks);
        Assert.Equal(1, yesterdayPoint.Tests - before.ActivitySeries30d[^2].Tests);
        Assert.Equal(1, yesterdayPoint.Exits - before.ActivitySeries30d[^2].Exits);
        Assert.Equal(
            1,
            yesterdayPoint.ProgramTasks - before.ActivitySeries30d[^2].ProgramTasks
        );
    }

    /// <summary>
    /// Rollup con filas + filas OLTP: el rollup gana y los conteos OLTP de la
    /// ventana se ignoran por completo (nunca se mezclan).
    /// </summary>
    [Fact]
    public async Task Kpis_ConRollup_PriorizaElRollupSobreOltp()
    {
        if (IsSkipped)
            return;

        await ClearRollupAsync();
        var today = Today;

        _db.HealthTestDailyMetrics.Add(
            new HealthTestDailyMetric
            {
                MetricDate = today,
                ClinicId = Guid.Empty,
                MetricKey = "assignments_count",
                DimensionKey = "completed",
                TotalCount = 5,
                LastUpdatedAt = DateTime.UtcNow,
            }
        );
        _db.InventoryDailyMetrics.AddRange(
            new InventoryDailyMetric
            {
                MetricDate = today,
                MetricKey = "entries_count",
                DimensionKey = "total",
                TotalCount = 7,
                // La columna erp.inventory_daily_metrics.last_updated_at es
                // timestamp without time zone → Kind Unspecified.
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            },
            new InventoryDailyMetric
            {
                MetricDate = today,
                MetricKey = "exits_count",
                DimensionKey = "total",
                TotalCount = 9,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            }
        );
        _db.ProgramDailyMetrics.Add(
            new ProgramDailyMetric
            {
                MetricDate = today,
                MetricKey = "tasks_completed_today",
                DimensionKey = "general",
                TotalCount = 11,
                TotalValue = 0,
                LastUpdatedAt = DateTime.UtcNow,
            }
        );
        await _db.SaveChangesAsync();

        // Filas OLTP de la ventana que NO deben sumarse al rollup.
        var patient = await NewPatientAsync($"kpis-mix-hoy-{Guid.NewGuid():N}");
        var versionId = await FirstHealthTestVersionIdAsync();
        await SeedHealthAssignmentAsync(
            patient.Id,
            versionId,
            HealthTestAssignmentStatus.completed,
            NoonUtc(today),
            NoonUtc(today)
        );
        await SeedInventoryEntryAsync(today);
        await SeedInventoryExitAsync(today);
        var programPatient = await NewPatientAsync($"kpis-mix-prog-{Guid.NewGuid():N}");
        await SeedProgramTaskAsync(programPatient.Id, today, NoonUtc(today));

        var dto = await GetKpisAsync();

        Assert.Equal(5, dto.HealthTests30d);
        Assert.Equal(7, dto.InventoryEntries30d);
        Assert.Equal(9, dto.InventoryExits30d);
        Assert.Equal(11, dto.ProgramTasks30d);

        var todayPoint = dto.ActivitySeries30d[^1];
        Assert.Equal(5, todayPoint.Tests);
        Assert.Equal(7, todayPoint.Entries);
        Assert.Equal(9, todayPoint.Exits);
        Assert.Equal(11, todayPoint.ProgramTasks);
    }
}
