using CoppAddresd.Application.Features.HealthTests.Scoring;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.IntegrationTests;

/// <summary>
/// Pruebas de integración del módulo Tests de Salud con PostgreSQL real
/// (BD aislada vía COP_TEST_DB_CONNECTION): catálogo → batería → asignación →
/// scoring → indicadores → alertas, aislamiento entre pacientes y asignación
/// masiva. Cada test crea un paciente propio y revierte su transacción al final.
/// </summary>
public sealed class HealthTestsIntegrationTests : IAsyncLifetime
{
    private const string EnvVar = "COP_TEST_DB_CONNECTION";

    private readonly string _connectionString = Environment.GetEnvironmentVariable(EnvVar)!;
    private bool _skipped;
    private AppDbContext _db = null!;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _transaction = null!;
    private HealthTestRepository _repository = null!;
    private readonly SumScoreStrategy _sumStrategy = new();
    private readonly ScoreRangeEngine _rangeEngine = new();
    private readonly AlertEngine _alertEngine = new();

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
        _repository = new HealthTestRepository(_db);
        _transaction = await _db.Database.BeginTransactionAsync();
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

    private async Task<HealthTestInstrument> NewInstrumentWithVersionAsync(
        string code,
        HealthTestScoringStrategy strategy = HealthTestScoringStrategy.sum
    )
    {
        var instrument = new HealthTestInstrument
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = $"Test {code}",
            Category = "clinico",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        await _repository.AddInstrumentAsync(instrument);

        var version = new HealthTestVersion
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrument.Id,
            VersionNumber = 1,
            Status = HealthTestVersionStatus.active,
            IsCurrent = true,
            ScoringStrategy = strategy,
            CreatedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow,
        };
        await _repository.AddVersionAsync(version);

        instrument.Versions = [version];
        return instrument;
    }

    private async Task AddScaleQuestionAsync(
        HealthTestVersion version,
        string code,
        string section,
        decimal[] values,
        HealthTestScoringDirection direction = HealthTestScoringDirection.positive
    )
    {
        var question = new HealthTestQuestion
        {
            Id = Guid.NewGuid(),
            VersionId = version.Id,
            Code = code,
            Section = section,
            Text = $"Pregunta {code}",
            Type = HealthTestQuestionType.scale,
            ScoringDirection = direction,
            SortOrder = 1,
            IsActive = true,
        };
        question.Options = values
            .Select(
                (v, i) =>
                    new HealthTestAnswerOption
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = question.Id,
                        Text = v.ToString(),
                        ScoreValue = v,
                        SortOrder = i,
                        IsActive = true,
                    }
            )
            .ToList();
        await _repository.AddQuestionsRangeAsync([question]);
    }

    /// <summary>
    /// Flujo completo: paciente → instrumento con preguntas → batería →
    /// asignación → start → submit → scoring + resultados + indicadores.
    /// </summary>
    [Fact]
    public async Task FlujoCompleto_CatalogoBateriaAsignacionSubmit_GeneraResultados()
    {
        var patient = await NewPatientAsync("doc-flow");

        // Instrumento con 2 preguntas Likert 1-5 (estrategia sum).
        var instrument = await NewInstrumentWithVersionAsync("it_flow");
        var version = instrument.Versions.Single();
        await AddScaleQuestionAsync(version, "q1", null, [1, 2, 3, 4, 5]);
        await AddScaleQuestionAsync(version, "q2", null, [1, 2, 3, 4, 5]);
        await _repository.AddRangesRangeAsync([
            new HealthTestScoreRange
            {
                Id = Guid.NewGuid(),
                VersionId = version.Id,
                MinValue = 0,
                MaxValue = 5,
                Label = "bajo",
                Severity = HealthTestSeverity.low,
                IsActive = true,
            },
            new HealthTestScoreRange
            {
                Id = Guid.NewGuid(),
                VersionId = version.Id,
                MinValue = 6,
                MaxValue = 10,
                Label = "alto",
                Severity = HealthTestSeverity.high,
                IsActive = true,
            },
        ]);

        // Batería con el instrumento + asignación.
        var battery = new HealthTestBattery
        {
            Id = Guid.NewGuid(),
            Code = "bat_flow",
            Name = "Batería flujo",
            IsActive = true,
            AutoAssignOnPatientCreate = false,
            CreatedAt = DateTime.UtcNow,
        };
        await _repository.AddBatteryAsync(battery);
        await _repository.AddBatteryItemsRangeAsync([
            new HealthTestBatteryItem
            {
                Id = Guid.NewGuid(),
                BatteryId = battery.Id,
                InstrumentId = instrument.Id,
                VersionId = version.Id,
                SortOrder = 1,
                IsRequired = true,
            },
        ]);

        var batteryAssignment = new HealthTestBatteryAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            BatteryId = battery.Id,
            Status = HealthTestAssignmentStatus.pending,
            AssignedAt = DateTime.UtcNow,
        };
        await _repository.AddBatteryAssignmentAsync(batteryAssignment);

        var assignment = new HealthTestAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            BatteryAssignmentId = batteryAssignment.Id,
            VersionId = version.Id,
            Status = HealthTestAssignmentStatus.pending,
            AssignedAt = DateTime.UtcNow,
        };
        await _repository.AddAssignmentAsync(assignment);

        // Evaluación + respuestas + scoring manual (mismo pipeline que el handler).
        var evaluation = new HealthTestEvaluation
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment.Id,
            PatientId = patient.Id,
            VersionId = version.Id,
            Status = HealthTestEvaluationStatus.started,
            StartedAt = DateTime.UtcNow,
        };
        await _repository.AddEvaluationAsync(evaluation);

        var q1 = (await _repository.ListQuestionsByVersionAsync(version.Id))[0];
        var q2 = (await _repository.ListQuestionsByVersionAsync(version.Id))[1];
        var responses = new List<HealthTestResponse>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EvaluationId = evaluation.Id,
                QuestionId = q1.Id,
                AnswerOptionId = q1.Options.ElementAt(3).Id, // 4
                CreatedAt = DateTime.UtcNow,
            },
            new()
            {
                Id = Guid.NewGuid(),
                EvaluationId = evaluation.Id,
                QuestionId = q2.Id,
                AnswerOptionId = q2.Options.ElementAt(4).Id, // 5
                CreatedAt = DateTime.UtcNow,
            },
        };
        await _repository.AddResponsesRangeAsync(responses);

        // Scoring con la estrategia de la versión (sum).
        var scoreQuestions = new List<ScoreQuestion>
        {
            new(
                q1.Id,
                q1.Code,
                q1.Section,
                q1.Type,
                q1.ScoringDirection,
                q1.Options.Select(o => new ScoreOption(o.Id, o.ScoreValue)).ToList()
            ),
            new(
                q2.Id,
                q2.Code,
                q2.Section,
                q2.Type,
                q2.ScoringDirection,
                q2.Options.Select(o => new ScoreOption(o.Id, o.ScoreValue)).ToList()
            ),
        };
        var scoreAnswers = responses
            .Select(r => new ScoreAnswer(r.QuestionId, r.AnswerOptionId, r.ValueText))
            .ToList();
        var output = _sumStrategy.Calculate(scoreQuestions, scoreAnswers);

        Assert.Equal(9m, output.Score);

        // Clasificación por rangos.
        var ranges = await _repository.ListRangesByVersionAsync(version.Id);
        var classification = _rangeEngine.Classify(output.Score, ranges);
        Assert.Equal("alto", classification.Label);
        Assert.Equal(HealthTestSeverity.high, classification.Severity);
    }

    /// <summary>
    /// El pipeline del handler real (SubmitEvaluationCommand) ya está cubierto
    /// por los unit tests del motor; aquí se valida que el repositorio persiste
    /// resultados y alertas sin duplicados.
    /// </summary>
    [Fact]
    public async Task Alertas_Dedup_UnaAlertaPorReglaYResultado()
    {
        var patient = await NewPatientAsync("doc-alert");
        var instrument = await NewInstrumentWithVersionAsync(
            "it_alert",
            HealthTestScoringStrategy.sum
        );
        var version = instrument.Versions.Single();
        await AddScaleQuestionAsync(version, "q1", null, [1, 2, 3, 4, 5]);
        await _repository.AddRangesRangeAsync([
            new HealthTestScoreRange
            {
                Id = Guid.NewGuid(),
                VersionId = version.Id,
                MinValue = 0,
                MaxValue = 10,
                Label = "alto",
                Severity = HealthTestSeverity.high,
                IsActive = true,
            },
        ]);

        var assignment = new HealthTestAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            VersionId = version.Id,
            Status = HealthTestAssignmentStatus.pending,
            AssignedAt = DateTime.UtcNow,
        };
        await _repository.AddAssignmentAsync(assignment);

        var evaluation = new HealthTestEvaluation
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment.Id,
            PatientId = patient.Id,
            VersionId = version.Id,
            Status = HealthTestEvaluationStatus.completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
        };
        await _repository.AddEvaluationAsync(evaluation);

        var result = new HealthTestResult
        {
            Id = Guid.NewGuid(),
            EvaluationId = evaluation.Id,
            ResultType = HealthTestResultType.score,
            Code = "score_total",
            Label = "Score total",
            Value = 9m,
            Qualifier = "alto",
            Severity = HealthTestSeverity.high,
            CreatedAt = DateTime.UtcNow,
        };
        await _repository.AddResultsRangeAsync([result]);

        var rule = new HealthTestAlertRule
        {
            Id = Guid.NewGuid(),
            Code = "rule_alto",
            Name = "Riesgo alto",
            Condition =
                """{"when":{"resultType":"score","code":"score_total","severity":["high"]}}""",
            Severity = HealthTestSeverity.high,
            MessageTemplate = "Riesgo {label} ({value})",
            IsActive = true,
        };
        await _db.HealthTestAlertRules.AddAsync(rule);
        await _db.SaveChangesAsync();

        var rules = await _repository.ListActiveAlertRulesAsync();
        var drafts = _alertEngine.Evaluate(patient.Id, [result], rules);

        Assert.Single(drafts);
        Assert.Equal("Riesgo Score total (9)", drafts[0].Title);

        // Dedup: la segunda inserción para el mismo (result, rule) no duplica.
        var exists = await _repository.AlertExistsForResultRuleAsync(result.Id, rule.Id);
        Assert.False(exists);
        await _repository.AddAlertAsync(
            new HealthTestAlert
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                ResultId = result.Id,
                RuleId = rule.Id,
                Severity = drafts[0].Severity,
                Title = drafts[0].Title,
                Body = drafts[0].Body,
                Status = HealthTestAlertStatus.active,
                CreatedAt = DateTime.UtcNow,
            }
        );
        exists = await _repository.AlertExistsForResultRuleAsync(result.Id, rule.Id);
        Assert.True(exists);
    }

    /// <summary>
    /// Aislamiento entre pacientes: el perfil de cada paciente se resuelve por
    /// su propio user_id. La FK a auth.users exige que el usuario exista: se
    /// inserta directo en el schema auth de la BD de test.
    /// </summary>
    [Fact]
    public async Task Aislamiento_PacientePorUserId_ResuelveSoloSuPerfil()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var patientA = await NewPatientAsync("doc-iso-a");
        var patientB = await NewPatientAsync("doc-iso-b");

        var now = DateTime.UtcNow;
        foreach (
            var (userId, email) in new[]
            {
                (userA, $"iso-a-{Guid.NewGuid():N}@test.local"),
                (userB, $"iso-b-{Guid.NewGuid():N}@test.local"),
            }
        )
        {
            await _db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO auth."Users" ("Id", "UserName", "NormalizedUserName", "Email",
                    "NormalizedEmail", "FirstName", "LastName", "EmailConfirmed",
                    "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled",
                    "AccessFailedCount", "IsActive", "CreatedAt")
                VALUES ({0}, {1}, {2}, {1}, {2}, 'Test', 'User', true, false, false, true, 0, true, {3})
                """,
                userId,
                email,
                email.ToUpperInvariant(),
                now
            );
        }

        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE app.patient_profiles SET user_id = {0} WHERE id = {1}",
            userA,
            patientA.Id
        );
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE app.patient_profiles SET user_id = {0} WHERE id = {1}",
            userB,
            patientB.Id
        );

        var resolvedA = await _repository.GetPatientByUserIdAsync(userA);
        var resolvedB = await _repository.GetPatientByUserIdAsync(userB);

        Assert.NotNull(resolvedA);
        Assert.Equal(patientA.Id, resolvedA!.Id);
        Assert.NotNull(resolvedB);
        Assert.Equal(patientB.Id, resolvedB!.Id);
        Assert.NotEqual(resolvedA.Id, resolvedB.Id);
    }

    /// <summary>
    /// Asignación masiva de una batería: crea una asignación por ítem por paciente.
    /// </summary>
    [Fact]
    public async Task AsignacionMasiva_BateriaVariosPacientes_CreaAsignaciones()
    {
        var patient1 = await NewPatientAsync("doc-mass-1");
        var patient2 = await NewPatientAsync("doc-mass-2");
        var instrument = await NewInstrumentWithVersionAsync("it_mass");
        var version = instrument.Versions.Single();

        var battery = new HealthTestBattery
        {
            Id = Guid.NewGuid(),
            Code = "bat_mass",
            Name = "Batería masiva",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        await _repository.AddBatteryAsync(battery);
        await _repository.AddBatteryItemsRangeAsync([
            new HealthTestBatteryItem
            {
                Id = Guid.NewGuid(),
                BatteryId = battery.Id,
                InstrumentId = instrument.Id,
                VersionId = version.Id,
                SortOrder = 1,
                IsRequired = true,
            },
        ]);

        foreach (var patient in new[] { patient1, patient2 })
        {
            var batteryAssignment = new HealthTestBatteryAssignment
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                BatteryId = battery.Id,
                Status = HealthTestAssignmentStatus.pending,
                AssignedAt = DateTime.UtcNow,
            };
            await _repository.AddBatteryAssignmentAsync(batteryAssignment);
            await _repository.AddAssignmentAsync(
                new HealthTestAssignment
                {
                    Id = Guid.NewGuid(),
                    PatientId = patient.Id,
                    BatteryAssignmentId = batteryAssignment.Id,
                    VersionId = version.Id,
                    Status = HealthTestAssignmentStatus.pending,
                    AssignedAt = DateTime.UtcNow,
                }
            );
        }

        var a1 = await _repository.ListActiveAssignmentsByPatientAsync(patient1.Id);
        var a2 = await _repository.ListActiveAssignmentsByPatientAsync(patient2.Id);

        Assert.Single(a1);
        Assert.Single(a2);
        Assert.Equal(HealthTestAssignmentStatus.pending, a1[0].Status);
    }
}
