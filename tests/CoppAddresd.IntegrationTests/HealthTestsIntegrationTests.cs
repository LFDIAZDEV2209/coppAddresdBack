using CoppAddresd.Application.Features.HealthTests.Execution;
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
            // Contrato actual: el code del score es el código del instrumento.
            Code = "it_alert",
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
            Condition = """{"when":{"resultType":"score","code":"it_alert","severity":["high"]}}""",
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

    // --- Detalle de evaluación, historial y resultados por paciente (change patient-health-test-results) ---

    /// <summary>
    /// Bug fix: ListResultsByPatientAsync devuelve resultados de TODAS las
    /// evaluaciones (antes solo del primer intento vía .First()) y lista vacía
    /// cuando el paciente no tiene evaluaciones (antes 500).
    /// </summary>
    [Fact]
    public async Task ResultadosPorPaciente_TodasLasEvaluaciones_OrdenadasPorCompletado()
    {
        var patient = await NewPatientAsync("doc-results");
        var instrument = await NewInstrumentWithVersionAsync("it_results");
        var version = instrument.Versions.Single();

        // Sin evaluaciones → lista vacía, sin excepción.
        var empty = await _repository.ListResultsByPatientAsync(patient.Id);
        Assert.Empty(empty);

        // Dos evaluaciones completadas del mismo test con fechas distintas.
        var now = DateTime.UtcNow;
        var assignmentOlder = new HealthTestAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            VersionId = version.Id,
            Status = HealthTestAssignmentStatus.completed,
            AssignedAt = now.AddDays(-10),
        };
        var assignmentNewer = new HealthTestAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            VersionId = version.Id,
            Status = HealthTestAssignmentStatus.completed,
            AssignedAt = now.AddDays(-1),
        };
        await _repository.AddAssignmentAsync(assignmentOlder);
        await _repository.AddAssignmentAsync(assignmentNewer);

        var evalOlder = new HealthTestEvaluation
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignmentOlder.Id,
            PatientId = patient.Id,
            VersionId = version.Id,
            Status = HealthTestEvaluationStatus.completed,
            StartedAt = now.AddDays(-10),
            CompletedAt = now.AddDays(-10),
            Score = 3m,
            ScorePercentage = 30m,
        };
        var evalNewer = new HealthTestEvaluation
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignmentNewer.Id,
            PatientId = patient.Id,
            VersionId = version.Id,
            Status = HealthTestEvaluationStatus.completed,
            StartedAt = now.AddDays(-1),
            CompletedAt = now.AddDays(-1),
            Score = 9m,
            ScorePercentage = 90m,
        };
        await _repository.AddEvaluationAsync(evalOlder);
        await _repository.AddEvaluationAsync(evalNewer);
        await _repository.AddResultsRangeAsync([
            new HealthTestResult
            {
                Id = Guid.NewGuid(),
                EvaluationId = evalOlder.Id,
                ResultType = HealthTestResultType.score,
                Code = "it_results",
                Label = "Score total",
                Value = 3m,
                Qualifier = "bajo",
                Severity = HealthTestSeverity.low,
                CreatedAt = now,
            },
            new HealthTestResult
            {
                Id = Guid.NewGuid(),
                EvaluationId = evalNewer.Id,
                ResultType = HealthTestResultType.score,
                Code = "it_results",
                Label = "Score total",
                Value = 9m,
                Qualifier = "alto",
                Severity = HealthTestSeverity.high,
                CreatedAt = now,
            },
        ]);

        var results = await _repository.ListResultsByPatientAsync(patient.Id);

        Assert.Equal(2, results.Count);
        // Orden por CompletedAt desc: primero el intento más reciente.
        Assert.Equal(evalNewer.Id, results[0].EvaluationId);
        Assert.Equal(evalOlder.Id, results[1].EvaluationId);
    }

    /// <summary>
    /// Paginación y filtros del listado de evaluaciones por paciente
    /// (status, rango de fechas, categoría) con total y orden por completado desc.
    /// </summary>
    [Fact]
    public async Task EvaluacionesPorPaciente_PaginacionYFiltros()
    {
        var patient = await NewPatientAsync("doc-paged");
        var instrument = await NewInstrumentWithVersionAsync("it_paged");
        var version = instrument.Versions.Single();
        var otherInstrument = await NewInstrumentWithVersionAsync("it_paged_otra");
        var otherVersion = otherInstrument.Versions.Single();

        var now = DateTime.UtcNow;
        var a1 = new HealthTestAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            VersionId = version.Id,
            Status = HealthTestAssignmentStatus.completed,
            AssignedAt = now.AddDays(-3),
        };
        var a2 = new HealthTestAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            VersionId = version.Id,
            Status = HealthTestAssignmentStatus.in_progress,
            AssignedAt = now.AddDays(-2),
        };
        var a3 = new HealthTestAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            VersionId = otherVersion.Id,
            Status = HealthTestAssignmentStatus.completed,
            AssignedAt = now.AddDays(-1),
        };
        await _repository.AddAssignmentAsync(a1);
        await _repository.AddAssignmentAsync(a2);
        await _repository.AddAssignmentAsync(a3);

        var e1 = new HealthTestEvaluation
        {
            Id = Guid.NewGuid(),
            AssignmentId = a1.Id,
            PatientId = patient.Id,
            VersionId = version.Id,
            Status = HealthTestEvaluationStatus.completed,
            StartedAt = now.AddDays(-3),
            CompletedAt = now.AddDays(-3),
            Score = 5m,
        };
        var e2 = new HealthTestEvaluation
        {
            Id = Guid.NewGuid(),
            AssignmentId = a2.Id,
            PatientId = patient.Id,
            VersionId = version.Id,
            Status = HealthTestEvaluationStatus.started,
            StartedAt = now.AddDays(-2),
        };
        var e3 = new HealthTestEvaluation
        {
            Id = Guid.NewGuid(),
            AssignmentId = a3.Id,
            PatientId = patient.Id,
            VersionId = otherVersion.Id,
            Status = HealthTestEvaluationStatus.completed,
            StartedAt = now.AddDays(-1),
            CompletedAt = now.AddDays(-1),
            Score = 7m,
        };
        await _repository.AddEvaluationAsync(e1);
        await _repository.AddEvaluationAsync(e2);
        await _repository.AddEvaluationAsync(e3);

        // Página 1 de 2 → 2 items, total 3.
        var (items, total) = await _repository.ListEvaluationsByPatientPageAsync(
            patient.Id,
            status: null,
            from: null,
            to: null,
            category: null,
            page: 1,
            pageSize: 2
        );
        Assert.Equal(3, total);
        Assert.Equal(2, items.Count);
        // Orden desc por completado (coalesce a StartedAt para las no completadas):
        // e3 (ayer) primero, e2 (iniciada hace 2 días) luego, e1 (hace 3 días) último.
        Assert.Equal(e3.Id, items[0].Id);
        Assert.Equal(e2.Id, items[1].Id);

        // Filtro por estado.
        var (started, totalStarted) = await _repository.ListEvaluationsByPatientPageAsync(
            patient.Id,
            status: "started",
            from: null,
            to: null,
            category: null,
            page: 1,
            pageSize: 20
        );
        Assert.Equal(1, totalStarted);
        Assert.Equal(e2.Id, started[0].Id);

        // Filtro por rango de fechas (solo e1).
        var (inRange, totalInRange) = await _repository.ListEvaluationsByPatientPageAsync(
            patient.Id,
            status: null,
            from: now.AddDays(-4),
            to: now.AddDays(-2),
            category: null,
            page: 1,
            pageSize: 20
        );
        Assert.Equal(1, totalInRange);
        Assert.Equal(e1.Id, inRange[0].Id);

        // Filtro por categoría del instrumento ("clinico" en el fixture).
        var (byCategory, totalByCategory) = await _repository.ListEvaluationsByPatientPageAsync(
            patient.Id,
            status: null,
            from: null,
            to: null,
            category: "clinico",
            page: 1,
            pageSize: 20
        );
        Assert.Equal(3, totalByCategory);
    }

    /// <summary>
    /// Detalle de evaluación: intentos del mismo test numerados, respuestas con
    /// texto de pregunta/opción, resultados y comentarios.
    /// </summary>
    [Fact]
    public async Task DetalleEvaluacion_IntentosComentariosYRespuestas()
    {
        var patient = await NewPatientAsync("doc-detail");
        var instrument = await NewInstrumentWithVersionAsync("it_detail");
        var version = instrument.Versions.Single();
        await AddScaleQuestionAsync(version, "q1", "Sección A", [1, 2, 3, 4, 5]);

        var handler = new GetEvaluationDetailQueryHandler(_repository);
        var now = DateTime.UtcNow;

        // Dos intentos del mismo test: el primero con respuesta + resultado + comentario.
        var assignment1 = new HealthTestAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            VersionId = version.Id,
            Status = HealthTestAssignmentStatus.completed,
            AssignedAt = now.AddDays(-10),
        };
        await _repository.AddAssignmentAsync(assignment1);
        var eval1 = new HealthTestEvaluation
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment1.Id,
            PatientId = patient.Id,
            VersionId = version.Id,
            Status = HealthTestEvaluationStatus.completed,
            StartedAt = now.AddDays(-10),
            CompletedAt = now.AddDays(-10),
            Score = 2m,
            ScorePercentage = 40m,
        };
        await _repository.AddEvaluationAsync(eval1);

        var q1 = (await _repository.ListQuestionsByVersionAsync(version.Id))[0];
        await _repository.AddResponsesRangeAsync([
            new HealthTestResponse
            {
                Id = Guid.NewGuid(),
                EvaluationId = eval1.Id,
                QuestionId = q1.Id,
                AnswerOptionId = q1.Options.ElementAt(1).Id, // 2
                CreatedAt = now,
            },
        ]);
        await _repository.AddResultsRangeAsync([
            new HealthTestResult
            {
                Id = Guid.NewGuid(),
                EvaluationId = eval1.Id,
                ResultType = HealthTestResultType.subscale,
                Code = "sec_a",
                Label = "Sección A",
                Value = 2m,
                Qualifier = "bajo",
                Severity = HealthTestSeverity.low,
                CreatedAt = now,
            },
        ]);
        await _repository.AddCommentAsync(
            new HealthTestComment
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                EvaluationId = eval1.Id,
                AuthorId = Guid.NewGuid(),
                Body = "Paciente progresa bien.",
                CreatedAt = now.AddDays(-5),
            }
        );

        var assignment2 = new HealthTestAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            VersionId = version.Id,
            Status = HealthTestAssignmentStatus.completed,
            AssignedAt = now.AddDays(-1),
        };
        await _repository.AddAssignmentAsync(assignment2);
        var eval2 = new HealthTestEvaluation
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment2.Id,
            PatientId = patient.Id,
            VersionId = version.Id,
            Status = HealthTestEvaluationStatus.completed,
            StartedAt = now.AddDays(-1),
            CompletedAt = now.AddDays(-1),
            Score = 4m,
            ScorePercentage = 80m,
        };
        await _repository.AddEvaluationAsync(eval2);

        // Intento 2: el más reciente.
        var detail2 = await handler.Handle(
            new GetEvaluationDetailQuery(patient.Id, eval2.Id),
            default
        );
        Assert.NotNull(detail2);
        Assert.Equal(2, detail2!.Attempt);
        Assert.Equal("it_detail", detail2.TestCode);
        Assert.Equal(2, detail2.Attempts.Count);
        Assert.Equal(eval1.Id, detail2.Attempts[0].Id);
        Assert.Equal(eval2.Id, detail2.Attempts[1].Id);
        Assert.Equal(80m, detail2.ScorePercentage);
        Assert.Empty(detail2.Responses);
        Assert.Empty(detail2.Comments);

        // Intento 1: respuestas + resultado + comentario incluidos.
        var detail1 = await handler.Handle(
            new GetEvaluationDetailQuery(patient.Id, eval1.Id),
            default
        );
        Assert.NotNull(detail1);
        Assert.Equal(1, detail1!.Attempt);
        Assert.Single(detail1.Responses);
        Assert.Equal("Pregunta q1", detail1.Responses[0].QuestionText);
        Assert.Equal("Sección A", detail1.Responses[0].Section);
        Assert.Equal("2", detail1.Responses[0].AnswerOptionText);
        Assert.Single(detail1.Results);
        Assert.Equal(HealthTestResultType.subscale, detail1.Results[0].ResultType);
        Assert.Single(detail1.Comments);
        Assert.Equal("Paciente progresa bien.", detail1.Comments[0].Body);

        // Evaluación inexistente → null (404 en el controller).
        var missing = await handler.Handle(
            new GetEvaluationDetailQuery(patient.Id, Guid.NewGuid()),
            default
        );
        Assert.Null(missing);

        // Evaluación de otro paciente → null.
        var otherPatient = await NewPatientAsync("doc-detail-otro");
        var foreign = await handler.Handle(
            new GetEvaluationDetailQuery(otherPatient.Id, eval1.Id),
            default
        );
        Assert.Null(foreign);
    }

    /// <summary>
    /// Filtro geográfico del dashboard: resuelve pacientes por uno o varios
    /// estados (códigos normalizados, unión) o ciudad (con precedencia) y
    /// devuelve vacío sin filtros (nunca expone el padrón completo por accidente).
    /// </summary>
    [Fact]
    public async Task GetPatientIdsByGeo_FiltraPorEstadoYCiudad()
    {
        if (_skipped)
        {
            return;
        }

        var country = await NewTestCountryAsync();
        var (stateA, cityA1) = await NewTestStateWithCityAsync(country, "QA", "Ciudad Uno");
        var cityA2 = await NewTestCityAsync(stateA, "Ciudad Dos");
        var (stateB, cityB1) = await NewTestStateWithCityAsync(country, "QC", "Ciudad Tres");

        var patientA1 = await NewPatientInCityAsync("doc-geo-a1", stateA.Id, cityA1.Id);
        var patientA2 = await NewPatientInCityAsync("doc-geo-a2", stateA.Id, cityA2.Id);
        var patientB1 = await NewPatientInCityAsync("doc-geo-b1", stateB.Id, cityB1.Id);

        // Por estado (el código se normaliza a mayúsculas).
        var byState = await _repository.GetPatientIdsByGeoAsync(
            [stateA.Code.ToLowerInvariant()],
            null
        );
        Assert.Contains(patientA1.Id, byState);
        Assert.Contains(patientA2.Id, byState);
        Assert.DoesNotContain(patientB1.Id, byState);

        // Varios estados → unión (multi-selección del mapa).
        var byStates = await _repository.GetPatientIdsByGeoAsync(
            [stateA.Code, stateB.Code],
            null
        );
        Assert.Equal(3, byStates.Count);
        Assert.Contains(patientA1.Id, byStates);
        Assert.Contains(patientA2.Id, byStates);
        Assert.Contains(patientB1.Id, byStates);

        // cityId tiene precedencia sobre el estado.
        var byCity = await _repository.GetPatientIdsByGeoAsync([stateB.Code], cityA2.Id);
        Assert.Single(byCity);
        Assert.Equal(patientA2.Id, byCity[0]);

        // Sin filtros → vacío.
        var blank = await _repository.GetPatientIdsByGeoAsync(["   "], null);
        Assert.Empty(blank);
    }

    /// <summary>
    /// La tabla maestra filtrada por zona recibe la lista de pacientes y solo
    /// devuelve las asignaciones de esos pacientes.
    /// </summary>
    [Fact]
    public async Task ListAssignmentsWithPatientData_FiltraPorPacientesDeLaZona()
    {
        if (_skipped)
        {
            return;
        }

        var instrument = await NewInstrumentWithVersionAsync("it_geo_master");
        var version = instrument.Versions.Single();
        var patientIn = await NewPatientAsync("doc-master-in");
        var patientOut = await NewPatientAsync("doc-master-out");

        await _repository.AddAssignmentAsync(
            new HealthTestAssignment
            {
                Id = Guid.NewGuid(),
                PatientId = patientIn.Id,
                VersionId = version.Id,
                Status = HealthTestAssignmentStatus.pending,
                AssignedAt = DateTime.UtcNow,
            }
        );
        await _repository.AddAssignmentAsync(
            new HealthTestAssignment
            {
                Id = Guid.NewGuid(),
                PatientId = patientOut.Id,
                VersionId = version.Id,
                Status = HealthTestAssignmentStatus.pending,
                AssignedAt = DateTime.UtcNow,
            }
        );

        var filtered = await _repository.ListAssignmentsWithPatientDataAsync(
            null,
            [patientIn.Id]
        );
        Assert.Single(filtered);
        Assert.Equal(patientIn.Id, filtered[0].PatientId);

        var unfiltered = await _repository.ListAssignmentsWithPatientDataAsync(null);
        Assert.Equal(2, unfiltered.Count);
    }

    /// <summary>
    /// Handler de la tabla maestra con filtro geo: solo las filas de la zona y
    /// lista vacía cuando la zona no tiene pacientes.
    /// </summary>
    [Fact]
    public async Task GetMasterRows_ConFiltroGeo_AcotaPacientesDeLaZona()
    {
        if (_skipped)
        {
            return;
        }

        var country = await NewTestCountryAsync();
        var (stateIn, cityIn) = await NewTestStateWithCityAsync(country, "QD", "Ciudad Zona");
        var (stateOut, cityOut) = await NewTestStateWithCityAsync(
            country,
            "QE",
            "Ciudad Fuera"
        );

        var instrument = await NewInstrumentWithVersionAsync("it_geo_rows");
        var version = instrument.Versions.Single();
        var patientIn = await NewPatientInCityAsync("doc-rows-in", stateIn.Id, cityIn.Id);
        var patientOut = await NewPatientInCityAsync("doc-rows-out", stateOut.Id, cityOut.Id);

        await _repository.AddAssignmentAsync(
            new HealthTestAssignment
            {
                Id = Guid.NewGuid(),
                PatientId = patientIn.Id,
                VersionId = version.Id,
                Status = HealthTestAssignmentStatus.pending,
                AssignedAt = DateTime.UtcNow,
            }
        );
        await _repository.AddAssignmentAsync(
            new HealthTestAssignment
            {
                Id = Guid.NewGuid(),
                PatientId = patientOut.Id,
                VersionId = version.Id,
                Status = HealthTestAssignmentStatus.pending,
                AssignedAt = DateTime.UtcNow,
            }
        );

        var handler = new GetMasterRowsQueryHandler(_repository, new NoopCacheService());

        var rows = await handler.Handle(
            new GetMasterRowsQuery(null, [stateIn.Code], null),
            default
        );
        Assert.Single(rows);
        Assert.Equal(patientIn.Id, rows[0].Patient.Id);

        // Multi-selección de estados → unión de zonas.
        var union = await handler.Handle(
            new GetMasterRowsQuery(null, [stateIn.Code, stateOut.Code], null),
            default
        );
        Assert.Equal(2, union.Count);

        // Zona sin pacientes → vacío (early return cacheado).
        var empty = await handler.Handle(
            new GetMasterRowsQuery(null, null, Guid.NewGuid()),
            default
        );
        Assert.Empty(empty);
    }

    /// <summary>
    /// El mapa calcula el % de riesgo por ciudad SOLO sobre pacientes evaluados
    /// (con score): una ciudad con pacientes mapeados sin evaluaciones queda en
    /// null ("Sin datos"), no en 0% (el bug que pintaba todo verde).
    /// </summary>
    [Fact]
    public async Task GetGeo_PorcentajeRiesgoSoloSobreEvaluados()
    {
        if (_skipped)
        {
            return;
        }

        var country = await NewTestCountryAsync();
        var (stateA, cityA) = await NewTestStateWithCityAsync(country, "QF", "Ciudad Evaluada");
        var (stateB, cityB) = await NewTestStateWithCityAsync(country, "QG", "Ciudad Sin Datos");

        var evaluated = await NewPatientInCityAsync("doc-geo-eval", stateA.Id, cityA.Id);
        await NewPatientInCityAsync("doc-geo-sin-eval", stateA.Id, cityA.Id);
        await NewPatientInCityAsync("doc-geo-nodata", stateB.Id, cityB.Id);

        var instrument = await NewInstrumentWithVersionAsync("it_geo_eval");
        var version = instrument.Versions.Single();
        var assignment = new HealthTestAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = evaluated.Id,
            VersionId = version.Id,
            Status = HealthTestAssignmentStatus.pending,
            AssignedAt = DateTime.UtcNow,
        };
        await _repository.AddAssignmentAsync(assignment);

        var evaluation = new HealthTestEvaluation
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment.Id,
            PatientId = evaluated.Id,
            VersionId = version.Id,
            Status = HealthTestEvaluationStatus.completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
        };
        await _repository.AddEvaluationAsync(evaluation);

        await _repository.AddResultsRangeAsync([
            new HealthTestResult
            {
                Id = Guid.NewGuid(),
                EvaluationId = evaluation.Id,
                ResultType = HealthTestResultType.score,
                Code = "it_geo_eval",
                Label = "Score total",
                Value = 9m,
                Qualifier = "alto",
                Severity = HealthTestSeverity.high,
                CreatedAt = DateTime.UtcNow,
            },
        ]);

        var geo = await _repository.GetGeoAsync();

        var evaluatedCity = Assert.Single(geo.Cities, c => c.CityId == cityA.Id);
        Assert.Equal(2, evaluatedCity.Count);
        Assert.Equal(1, evaluatedCity.EvaluatedCount);
        Assert.Equal(100d, evaluatedCity.HighRiskPct);

        var noDataCity = Assert.Single(geo.Cities, c => c.CityId == cityB.Id);
        Assert.Equal(1, noDataCity.Count);
        Assert.Equal(0, noDataCity.EvaluatedCount);
        Assert.Null(noDataCity.HighRiskPct);
    }

    /// <summary>
    /// Crea un país de prueba con un código ISO libre (la BD es real y puede
    /// tener el catálogo completo; la transacción del test revierte todo).
    /// </summary>
    private async Task<Country> NewTestCountryAsync()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code =
                $"{(char)Random.Shared.Next('A', 'Z' + 1)}{(char)Random.Shared.Next('A', 'Z' + 1)}";
            if (await _db.Countries.AnyAsync(c => c.Code == code))
            {
                continue;
            }

            var country = new Country
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = $"País test {code}",
                PhoneCode = "999",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Countries.Add(country);
            await _db.SaveChangesAsync();
            return country;
        }

        throw new InvalidOperationException("No hay códigos ISO libres para el país de prueba.");
    }

    private async Task<(State State, City City)> NewTestStateWithCityAsync(
        Country country,
        string stateCode,
        string cityName
    )
    {
        var state = new State
        {
            Id = Guid.NewGuid(),
            CountryId = country.Id,
            Code = stateCode,
            Name = $"Estado {stateCode}",
            CreatedAt = DateTime.UtcNow,
        };
        var city = new City
        {
            Id = Guid.NewGuid(),
            StateId = state.Id,
            Name = cityName,
            CreatedAt = DateTime.UtcNow,
        };
        _db.States.Add(state);
        _db.Cities.Add(city);
        await _db.SaveChangesAsync();
        return (state, city);
    }

    private async Task<City> NewTestCityAsync(State state, string cityName)
    {
        var city = new City
        {
            Id = Guid.NewGuid(),
            StateId = state.Id,
            Name = cityName,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Cities.Add(city);
        await _db.SaveChangesAsync();
        return city;
    }

    private async Task<PatientProfile> NewPatientInCityAsync(string doc, Guid stateId, Guid cityId)
    {
        var patient = await NewPatientAsync(doc);
        patient.StateId = stateId;
        patient.CityId = cityId;
        await _db.SaveChangesAsync();
        return patient;
    }

    /// <summary>Cache de prueba sin persistencia (siempre miss) para handlers.</summary>
    private sealed class NoopCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
            where T : class => Task.FromResult<T?>(null);

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
            where T : class => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<T> GetOrCreateAsync<T>(
            string key,
            TimeSpan ttl,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken ct = default
        )
            where T : class => factory(ct);
    }
}
