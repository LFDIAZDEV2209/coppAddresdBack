using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json.Nodes;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.HealthTests.Events;
using CoppAddresd.Application.Features.HealthTests.Execution;
using CoppAddresd.Application.Features.HealthTests.Scoring;
using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using CoppAddresd.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;

namespace CoppAddresd.IntegrationTests;

public sealed class AssignmentPostgresFactAttribute : FactAttribute
{
    public AssignmentPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COP_TEST_DB_CONNECTION")))
            Skip = "Requiere PostgreSQL real con COP_TEST_DB_CONNECTION; crea y elimina una BD aislada.";
    }
}

/// <summary>Ejecuta el submit real, MediatR y repositorios reales con commits separados en PostgreSQL aislado.</summary>
public sealed partial class ProgramAssignmentIntegrationTests : IAsyncLifetime
{
    private readonly string? _server = Environment.GetEnvironmentVariable("COP_TEST_DB_CONNECTION");
    private readonly string _database = "coppaddresd_assignment_test_" + Guid.NewGuid().ToString("N");
    private string _connection = null!;
    private readonly EventCounter _counter = new();
    private readonly RecommendationFailure _failure = new();
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_server)) return;
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_server) { Database = "postgres" }.ConnectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\" TEMPLATE template0", admin))
            await command.ExecuteNonQueryAsync();
        _connection = new NpgsqlConnectionStringBuilder(_server) { Database = _database }.ConnectionString;
        await using var db = NewDb();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE SCHEMA auth;
            CREATE TABLE auth."Users" ("Id" uuid PRIMARY KEY, "SecurityStamp" text NULL);
            """);
        await db.Database.MigrateAsync();
        db.ProgramTemplates.Add(new ProgramTemplate { Id = Guid.NewGuid(), Code = "program-coppaddresd-83-days",
            Name = "Programa de prueba", TotalWeeks = 12, Status = TemplateStatus.Active });
        db.NutritionPlans.Add(new NutritionPlan { Id = Guid.NewGuid(), Name = "Plan Obesidad — Déficit calórico", DurationDays = 7,
            IsTemplate = true, Status = NutritionPlanStatus.Active });
        db.ExerciseRoutines.Add(new ExerciseRoutine { Id = Guid.NewGuid(), Name = "Caminata Activa",
            Difficulty = (RoutineDifficulty)1, Status = NutritionPlanStatus.Active });
        await db.SaveChangesAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.Configure<ProgramAssignmentSettings>(s => s.Enabled = true);
        services.AddScoped(_ => NewDb());
        services.AddScoped<IHealthTestRepository, HealthTestRepository>();
        services.AddScoped<IProgramRepository, ProgramRepository>();
        services.AddScoped<IProgramAssignmentRepository, ProgramAssignmentRepository>();
        services.AddScoped<IProgramAssignmentOperationsRepository, ProgramAssignmentOperationsRepository>();
        services.AddScoped<IProgramAssignmentNoticeRepository, ProgramAssignmentNoticeRepository>();
        services.AddSingleton<IProgramAssignmentNotifier>(Substitute.For<IProgramAssignmentNotifier>());
        services.AddSingleton<TimeProvider>(_lifecycleClock);
        services.AddScoped<ProgramAssignmentJob>();
        services.AddScoped<IWellnessRepository, WellnessRepository>();
        services.AddSingleton<AssignmentEngine>();
        services.AddSingleton<IProgramNarrativeQueue, ProgramNarrativeQueue>();
        services.AddSingleton(Substitute.For<ICacheService>());
        services.AddSingleton(_counter);
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(SubmitEvaluationCommand).Assembly));
        services.AddScoped<INotificationHandler<BatteryCompletedEvent>, CommitObserver>();
        services.AddKeyedSingleton<IScoreStrategy, SumScoreStrategy>(HealthTestScoringStrategy.sum);
        services.AddKeyedSingleton<IScoreStrategy, PercentageScoreStrategy>(HealthTestScoringStrategy.percentage);
        services.AddKeyedSingleton<IScoreStrategy, SubscaleScoreStrategy>(HealthTestScoringStrategy.subscale);
        services.AddKeyedSingleton<IScoreStrategy, InventoryScoreStrategy>(HealthTestScoringStrategy.inventory);
        services.AddKeyedSingleton<IScoreStrategy, WeightedScoreStrategy>(HealthTestScoringStrategy.weighted);
        services.AddSingleton<ScoreStrategyRegistry>();
        services.AddSingleton<ScoreRangeEngine>(); services.AddSingleton<IndicatorEngine>(); services.AddSingleton<AlertEngine>();
        _services = services.BuildServiceProvider();
    }

    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connection).AddInterceptors(_failure).Options);

    public async Task DisposeAsync()
    {
        if (_services is not null) await _services.DisposeAsync();
        if (string.IsNullOrWhiteSpace(_server)) return;
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_server) { Database = "postgres" }.ConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    [AssignmentPostgresFact]
    public async Task NueveTests_EventoPostCommit_InscripcionPendienteYPropuestaAtomicas_Idempotentes()
    {
        await using var setup = NewDb();
        var patient = new PatientProfile { Id = Guid.NewGuid(), FirstName = "Prueba", LastName = "Asignación",
            Gender = "Femenino", Timezone = "America/New_York", DocumentNumber = Guid.NewGuid().ToString("N") };
        setup.PatientProfiles.Add(patient);
        var battery = await setup.HealthTestBatteries.SingleAsync(b => b.Code == "bateria-inicial");
        battery.AutoAssignOnPatientCreate = true; battery.IsActive = true;
        var parent = new HealthTestBatteryAssignment { Id = Guid.NewGuid(), PatientId = patient.Id, BatteryId = battery.Id };
        setup.HealthTestBatteryAssignments.Add(parent);
        var required = await setup.HealthTestBatteryItems.Where(i => i.BatteryId == battery.Id && i.IsRequired)
            .OrderBy(i => i.SortOrder).Select(i => i.InstrumentId).ToListAsync();
        Assert.Equal(9, required.Count);
        var versions = await setup.HealthTestVersions.Where(v => required.Contains(v.InstrumentId) && v.IsCurrent)
            .ToDictionaryAsync(v => v.InstrumentId);
        var assignments = required.Select(id => new HealthTestAssignment { Id = Guid.NewGuid(),
            PatientId = patient.Id, VersionId = versions[id].Id, BatteryAssignmentId = parent.Id }).ToArray();
        setup.HealthTestAssignments.AddRange(assignments);
        await setup.SaveChangesAsync();
        var requests = new List<SubmitEvaluationCommand>();
        foreach (var assignment in assignments)
        {
            var questions = await setup.HealthTestQuestions.AsNoTracking().Include(q => q.Options)
                .Where(q => q.VersionId == assignment.VersionId && q.IsActive).ToListAsync();
            var answers = questions.Select(q => new SubmitAnswerInput(q.Id,
                q.Type == HealthTestQuestionType.num ? null : q.Options.OrderBy(o => o.ScoreValue).FirstOrDefault()?.Id,
                q.Type == HealthTestQuestionType.num ? q.Code switch { "peso" => "100", "talla" => "165", _ => q.MinValue?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "1" } : null)).ToArray();
            requests.Add(new(assignment.Id, new(answers, "submit-" + assignment.Id)));
        }
        for (var i = 0; i < 8; i++)
        {
            using var scope = _services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(requests[i]);
        }
        await using (var check = NewDb())
        {
            Assert.Equal(8, await check.HealthTestEvaluations.CountAsync(e => e.PatientId == patient.Id && e.Status == HealthTestEvaluationStatus.completed));
            Assert.False(await check.ProgramEnrollments.AnyAsync(e => e.PatientId == patient.Id));
        }
        Assert.Equal(0, _counter.Count);
        using (var scope = _services.CreateScope()) await scope.ServiceProvider.GetRequiredService<IMediator>().Send(requests[8]);
        await using var verify = NewDb();
        var enrollment = await verify.ProgramEnrollments.SingleAsync(e => e.PatientId == patient.Id);
        Assert.Equal(ProgramEnrollmentStatus.PendingAssignment, enrollment.Status);
        Assert.Equal(0, enrollment.CurrentWeekNumber);
        Assert.Equal("America/New_York", enrollment.Timezone);
        Assert.True(await verify.StreakStates.AnyAsync(s => s.EnrollmentId == enrollment.Id));
        var weeks = await verify.ProgramWeeks.Where(w => w.EnrollmentId == enrollment.Id).ToListAsync();
        Assert.Equal(12, weeks.Count);
        Assert.All(weeks, w => { Assert.Equal(ProgramWeekStatus.Locked, w.Status); Assert.Null(w.ActivatedAt); });
        var recommendation = await verify.ProgramAssignmentRecommendations.SingleAsync(r => r.EnrollmentId == enrollment.Id);
        Assert.Equal("proposed", recommendation.State);
        var snapshot = JsonNode.Parse(recommendation.ProfileSnapshot)!;
        Assert.NotEmpty(snapshot["results"]!.AsArray());
        Assert.True(snapshot["facts"]!["conditions.obesidad.active"]!.GetValue<bool>());
        Assert.False(snapshot["facts"]!["conditions.diabetes-t2.active"]!.GetValue<bool>());
        Assert.Equal("Plan Obesidad — Déficit calórico", JsonNode.Parse(recommendation.Proposal)!["nutritionPlan"]!["name"]!.GetValue<string>());
        Assert.Equal(1, _counter.Count);
        // Dos replays concurrentes del mismo clientRequestId: sin resultados, eventos ni inscripciones extra.
        await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            using var scope = _services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(requests[8]);
        }));
        Assert.Equal(9, await verify.HealthTestEvaluations.CountAsync(e => e.PatientId == patient.Id));
        Assert.Equal(1, await verify.ProgramEnrollments.CountAsync(e => e.PatientId == patient.Id));
        Assert.Equal(1, await verify.ProgramAssignmentRecommendations.CountAsync(r => r.PatientId == patient.Id));
        Assert.Equal(1, _counter.Count);
        // El worker puede fallar sin mutar propuesta/snapshot; probar sobre repositorio real.
        var client = Substitute.For<IProgramNarrativeClient>();
        client.GenerateAsync(Arg.Any<ProgramNarrativeRequest>(), Arg.Any<CancellationToken>())
            .Returns<ProgramNarrativeResponse>(_ => throw new HttpRequestException("503"));
        var repository = new ProgramAssignmentRepository(verify);
        var processor = new ProgramNarrativeProcessor(repository, client,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgramNarrativeProcessor>.Instance);
        Assert.False(await processor.ProcessAsync(recommendation.Id, CancellationToken.None));
        var failed = await repository.GetRecommendationAsync(recommendation.Id, CancellationToken.None);
        Assert.Equal("pending-narrative", failed!.State);
        Assert.Equal(recommendation.Proposal, failed.Proposal);
        Assert.Equal(recommendation.ProfileSnapshot, failed.ProfileSnapshot);
    }

    [AssignmentPostgresFact]
    public async Task FalloTrasCrearEnrollment_NoRevierteSubmit_RecuperacionConcurrenteNoDuplica()
    {
        await using var setup = NewDb();
        var patient = new PatientProfile { Id = Guid.NewGuid(), FirstName = "Prueba", LastName = "Rollback",
            DocumentNumber = Guid.NewGuid().ToString("N") };
        setup.PatientProfiles.Add(patient);
        var battery = await setup.HealthTestBatteries.SingleAsync(b => b.Code == "bateria-inicial");
        battery.AutoAssignOnPatientCreate = true;
        var parent = new HealthTestBatteryAssignment { Id = Guid.NewGuid(), PatientId = patient.Id, BatteryId = battery.Id };
        setup.HealthTestBatteryAssignments.Add(parent);
        var instruments = await setup.HealthTestBatteryItems.Where(i => i.BatteryId == battery.Id && i.IsRequired)
            .OrderBy(i => i.SortOrder).Select(i => i.InstrumentId).ToListAsync();
        var versions = await setup.HealthTestVersions.Where(v => instruments.Contains(v.InstrumentId) && v.IsCurrent)
            .ToDictionaryAsync(v => v.InstrumentId, v => v.Id);
        var assignments = instruments.Select(id => new HealthTestAssignment { Id = Guid.NewGuid(), PatientId = patient.Id,
            BatteryAssignmentId = parent.Id, VersionId = versions[id] }).ToArray();
        setup.HealthTestAssignments.AddRange(assignments);
        foreach (var assignment in assignments.Take(8))
        {
            assignment.Status = HealthTestAssignmentStatus.completed;
            setup.HealthTestEvaluations.Add(new HealthTestEvaluation { Id = Guid.NewGuid(), AssignmentId = assignment.Id,
                PatientId = patient.Id, VersionId = assignment.VersionId, Status = HealthTestEvaluationStatus.completed,
                CompletedAt = DateTime.UtcNow });
        }
        await setup.SaveChangesAsync();
        _failure.Enabled = true;
        using (var scope = _services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new SubmitEvaluationCommand(assignments[8].Id, new([], "fallo-evento")));
            Assert.Equal(HealthTestEvaluationStatus.completed, result.Status);
        }
        await using var check = NewDb();
        Assert.Equal(9, await check.HealthTestEvaluations.CountAsync(e => e.PatientId == patient.Id && e.Status == HealthTestEvaluationStatus.completed));
        Assert.False(await check.ProgramEnrollments.AnyAsync(e => e.PatientId == patient.Id));
        Assert.False(await check.ProgramAssignmentRecommendations.AnyAsync(r => r.PatientId == patient.Id));
        _failure.Enabled = false;
        await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            using var scope = _services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(new BatteryCompletedEvent(patient.Id, parent.Id));
        }));
        Assert.Equal(1, await check.ProgramEnrollments.CountAsync(e => e.PatientId == patient.Id));
        Assert.Equal(1, await check.ProgramAssignmentRecommendations.CountAsync(r => r.PatientId == patient.Id));
    }

    private sealed class RecommendationFailure : DbCommandInterceptor
    {
        public volatile bool Enabled;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (Enabled && command.CommandText.Contains("INSERT INTO app.program_assignment_recommendations", StringComparison.Ordinal))
                throw new InvalidOperationException("Fallo simulado al guardar la recomendación, después de crear inscripción y semanas.");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class EventCounter { public int Count; }
    private sealed class CommitObserver(AppDbContext db, EventCounter counter) : INotificationHandler<BatteryCompletedEvent>
    {
        public Task Handle(BatteryCompletedEvent notification, CancellationToken ct)
        {
            Assert.Null(db.Database.CurrentTransaction);
            Interlocked.Increment(ref counter.Count);
            return Task.CompletedTask;
        }
    }
}
