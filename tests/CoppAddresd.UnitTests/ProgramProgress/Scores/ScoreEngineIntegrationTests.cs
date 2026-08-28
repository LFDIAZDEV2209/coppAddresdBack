using CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoppAddresd.UnitTests.ProgramProgress.Scores;

/// <summary>
/// Tests de integración del motor de puntajes contra PostgreSQL real
/// (COP_TEST_DB_CONNECTION, patrón T-08): AC-19 (mezcla de 7 días),
/// AC-20 (bandas de transformación), AC-21 (neutros sin datos) y AC-22
/// (autoría clínica de líneas base). Afirma además las filas persistidas
/// (compute-on-read, SPEC §13.3).
/// </summary>
public sealed class ScoreEngineIntegrationTests(ProgramRepositoryTestDb fixture)
    : IClassFixture<ProgramRepositoryTestDb>, IAsyncLifetime
{
    private static readonly (TaskCode Code, int Points)[] TaskSeeds =
    [
        (TaskCode.podcast, 80),
        (TaskCode.vitals, 120),
        (TaskCode.nut, 150),
        (TaskCode.ejercicio, 150),
        (TaskCode.nutribiotico, 80),
        (TaskCode.emocional, 120),
    ];

    private Guid _templateId;
    private Guid _patientId;
    private Guid _enrollmentId;
    private Guid _unitKg;
    private Guid _metricWeight;
    private Guid _metricBmi;
    private Guid _metricGlucose;
    private Guid _clinicianUserId;
    private DateOnly _monday;

    public async Task InitializeAsync()
    {
        if (fixture.Skipped)
        {
            return;
        }

        await using var db = fixture.CreateDbContext();
        await ResetTablesAsync(db);
        await SeedDefaultWeightsAsync(db);
        _templateId = await SeedDefaultTemplateAsync(db);
        _patientId = await CreatePatientAsync(db, "Score", "Engine");
        _clinicianUserId = await CreateAuthUserAsync(db);
        (_unitKg, _metricWeight, _metricBmi, _metricGlucose) = await SeedClinicalCatalogAsync(db);
        _monday = ThisMonday();

        // Inscripción que arranca el LUNES de la semana actual: el período del
        // Índice de Salud es la ventana rodante de 7 días y la semana 1 del
        // Índice de Transformación es [monday, monday+6] (determinista).
        await using var enrollDb = fixture.CreateDbContext();
        var repo = new ProgramRepository(enrollDb, Configuration());
        var enrollment = await repo.EnrollAsync(_patientId, _templateId, "America/Bogota", _monday);
        _enrollmentId = enrollment.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------- AC-19

    /// <summary>
    /// AC-19: 7 días mixtos (3 perfectos, 2 parciales, 1 rescatado por
    /// congelamiento, 1 perdido) + base clínica con medición + ánimo +
    /// ejercicio. El Índice de Salud sigue las bandas de SPEC §13.4 y se
    /// persiste UNA fila para el período.
    /// </summary>
    [RequiresPostgresFact]
    public async Task GetOrComputeHealthScore_MezclaAC19_CumpleBandasYPersiste()
    {
        var todayLocal = PatientLocalToday();
        var periodStart = todayLocal.AddDays(-6);

        await using (var db = fixture.CreateDbContext())
        {
            var week1 = await db.ProgramWeeks.AsNoTracking()
                .SingleAsync(w => w.EnrollmentId == _enrollmentId && w.WeekNumber == 1);

            // Días: D0..D2 perfectos, D3..D4 parciales, D5 rescatado por
            // congelamiento (SIN check-in: el freeze rescata días sin
            // completaciones, SPEC §13.4.1), D6 perdido.
            SeedCheckin(db, week1.Id, periodStart, perfect: true);
            SeedCheckin(db, week1.Id, periodStart.AddDays(1), perfect: true);
            SeedCheckin(db, week1.Id, periodStart.AddDays(2), perfect: true);
            SeedCheckin(db, week1.Id, periodStart.AddDays(3), perfect: false);
            SeedCheckin(db, week1.Id, periodStart.AddDays(4), perfect: false);

            // Congelamiento consumido el día 5 (rescata ese día, sin check-in).
            db.StreakFreezes.Add(new StreakFreeze
            {
                EnrollmentId = _enrollmentId,
                Kind = StreakFreezeKind.Consumed,
                UsedOnLocalDate = periodStart.AddDays(5),
                GrantedReason = "TestFreeze",
            });

            // Ejercicio completado 3 de los 7 días → round(3/7*100) = 43. Los días
            // con ejercicio tienen check-in (FK de task_completions); el día 5
            // rescatado y el 6 perdido quedan sin completaciones.
            foreach (var offset in new[] { 0, 2, 4 })
            {
                SeedEjercicio(db, week1.Id, periodStart.AddDays(offset));
            }

            // Ánimo: [4] → (4-1)/4*100 = 75.
            db.EmotionalRecords.Add(new EmotionalRecord
            {
                PatientId = _patientId,
                ProgramEnrollmentId = _enrollmentId,
                RecordedLocalDate = periodStart.AddDays(2),
                MoodScore = 4,
            });

            // Línea base weight 82.5 → medición 80.0 (-3.03% favorable, entre 1% y
            // 5%) → banda 75 (SPEC §13.4.2: >5% sería 100).
            SeedBaselineAndMeasurement(db, _metricWeight, 82.5m, 80.0m, periodStart);

            await db.SaveChangesAsync();
        }

        await using var readDb = fixture.CreateDbContext();
        var repo = new ProgramRepository(readDb, Configuration());
        var health = await repo.GetOrComputeHealthScoreAsync(
            _patientId, ScoreTrigger.OnRead, ct: CancellationToken.None);

        Assert.NotNull(health);
        // adherence = round((3*1.0 + 2*0.6 + 1*0.4 + 1*0.0)/7*100) = 66
        Assert.Equal(66, health.Dimensions.Adherence);
        Assert.Equal(75, health.Dimensions.Clinical);   // -3.03% favorable → banda 75
        Assert.Equal(0, health.Dimensions.Nutrition);   // sin tabla de hábitos → 0
        Assert.Equal(75, health.Dimensions.Psychology); // ánimo 4 → 75
        Assert.Equal(43, health.Dimensions.Exercise);   // 3/7 días → 43

        // Ponderado: 66*0.30 + 75*0.30 + 0*0.20 + 75*0.10 + 43*0.10
        // = 19.8 + 22.5 + 0 + 7.5 + 4.3 = 54.1 → 54.
        Assert.Equal(54, health.Current);

        // UNA fila persistida para el período (compute-on-read, SPEC §13.3).
        var persisted = await readDb.HealthScores.AsNoTracking()
            .Where(h => h.PatientId == _patientId)
            .ToListAsync();
        Assert.Single(persisted);
        Assert.Equal(periodStart, persisted[0].PeriodStart);
        Assert.Equal(todayLocal, persisted[0].PeriodEnd);
        Assert.Equal(54, persisted[0].Score);
        Assert.Equal(ScoreTrend.stable, persisted[0].Trend); // sin previous
    }

    // ---------------------------------------------------------------- AC-20

    /// <summary>
    /// AC-20: 3 métricas (weight/bmi/glucose, todas LowerIsBetter) con
    /// |Δ%| = 18%, 7% y 12% → Transformación = round((100+75+90)/3) = 88,
    /// con el detalle JSONB y UNA fila persistida para la semana 1.
    /// </summary>
    [RequiresPostgresFact]
    public async Task GetOrComputeTransformationScore_AC20_Devuelve88ConDetalle()
    {
        await using (var db = fixture.CreateDbContext())
        {
            SeedBaselineAndMeasurement(db, _metricWeight, 100m, 82m, _monday);   // -18% → 100
            SeedBaselineAndMeasurement(db, _metricBmi, 100m, 93m, _monday);      // -7%  → 75
            SeedBaselineAndMeasurement(db, _metricGlucose, 100m, 88m, _monday);  // -12% → 90
            await db.SaveChangesAsync();
        }

        await using var readDb = fixture.CreateDbContext();
        var repo = new ProgramRepository(readDb, Configuration());
        var transformation = await repo.GetOrComputeTransformationScoreAsync(
            _patientId, ScoreTrigger.OnRead, ct: CancellationToken.None);

        Assert.NotNull(transformation);
        Assert.Equal(88, transformation.Current);
        Assert.Equal(1, transformation.Week); // current_week_number de la inscripción
        Assert.Equal("stable", transformation.Trend); // sin score_previous → stable
        Assert.Null(transformation.Previous);

        Assert.Equal(100, transformation.Detail["weight"].Score);
        Assert.Equal(75, transformation.Detail["bmi"].Score);
        Assert.Equal(90, transformation.Detail["glucose"].Score);
        Assert.Equal(-18m, transformation.Detail["weight"].Delta);
        Assert.Equal(-18m, transformation.Detail["weight"].DeltaPct);
        Assert.True(transformation.Detail["weight"].Favorable);

        // UNA fila persistida para la semana 1.
        var persisted = await readDb.TransformationScores.AsNoTracking()
            .Where(t => t.PatientId == _patientId)
            .ToListAsync();
        Assert.Single(persisted);
        Assert.Equal(1, persisted[0].WeekNumber);
        Assert.Equal(88, persisted[0].Score);
        Assert.Equal(ScoreTrend.stable, persisted[0].OverallTrend);
    }

    // ---------------------------------------------------------------- AC-21

    /// <summary>
    /// AC-21: paciente sin bases, sin check-ins, sin ánimo y sin hábitos →
    /// neutros sin datos (clinical 50, nutrition 0, psychology 60, exercise 0,
    /// adherence 0) y Transformación 0 con detail {}. La respuesta nunca
    /// falla y se persiste el ponderado.
    /// </summary>
    [RequiresPostgresFact]
    public async Task Scores_SinDatos_DevuelveNeutrosYTransformacion0()
    {
        await using var readDb = fixture.CreateDbContext();
        var repo = new ProgramRepository(readDb, Configuration());

        var health = await repo.GetOrComputeHealthScoreAsync(
            _patientId, ScoreTrigger.OnRead, ct: CancellationToken.None);
        var transformation = await repo.GetOrComputeTransformationScoreAsync(
            _patientId, ScoreTrigger.OnRead, ct: CancellationToken.None);

        Assert.NotNull(health);
        Assert.Equal(0, health.Dimensions.Adherence);
        Assert.Equal(50, health.Dimensions.Clinical);
        Assert.Equal(0, health.Dimensions.Nutrition);
        Assert.Equal(60, health.Dimensions.Psychology);
        Assert.Equal(0, health.Dimensions.Exercise);
        // 0*0.30 + 50*0.30 + 0*0.20 + 60*0.10 + 0*0.10 = 15 + 6 = 21
        Assert.Equal(21, health.Current);

        Assert.NotNull(transformation);
        Assert.Equal(0, transformation.Current);
        Assert.Empty(transformation.Detail);

        // Ambas filas quedan persistidas.
        Assert.Equal(1, await readDb.HealthScores.CountAsync(h => h.PatientId == _patientId));
        Assert.Equal(1, await readDb.TransformationScores.CountAsync(t => t.PatientId == _patientId));
    }

    /// <summary>Compute-on-read: una segunda lectura con fila fresca NO recalcula ni duplica.</summary>
    [RequiresPostgresFact]
    public async Task Scores_SegundaLectura_DevuelveFilaPersistidaSinDuplicar()
    {
        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());

        var first = await repo.GetOrComputeHealthScoreAsync(_patientId, ScoreTrigger.OnRead, ct: CancellationToken.None);
        var second = await repo.GetOrComputeHealthScoreAsync(_patientId, ScoreTrigger.OnRead, ct: CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.Current, second!.Current);
        Assert.Equal(1, await db.HealthScores.CountAsync(h => h.PatientId == _patientId));
    }

    // ---------------------------------------------------------------- AC-22 (integración)

    /// <summary>
    /// AC-22 (flujo exitoso): un clínico fija una línea base con <c>set_by</c>
    /// de un usuario auth y la guardia la acepta; una segunda escritura
    /// actualiza la fila (UPSERT por patient_id+metric_id).
    /// </summary>
    [RequiresPostgresFact]
    public async Task UpsertBaseline_Clinico_FijaYActualizaLaBase()
    {
        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());

        var created = await repo.UpsertClinicalBaselineAsync(
            Write(_metricWeight, 82.5m, targetValue: 75m), callerRoles: ["Physician"],
            ct: CancellationToken.None);

        Assert.Equal(_patientId, created.PatientId);
        Assert.Equal("weight", created.MetricCode);
        Assert.Equal(82.5m, created.Value);
        Assert.Equal(75m, created.TargetValue);
        Assert.Equal(_clinicianUserId, created.SetBy);

        var updated = await repo.UpsertClinicalBaselineAsync(
            Write(_metricWeight, 80m, targetValue: null), callerRoles: ["Admin"],
            ct: CancellationToken.None);

        Assert.Equal(80m, updated.Value);
        Assert.Null(updated.TargetValue);

        var all = await repo.ListClinicalBaselinesAsync(_patientId, ct: CancellationToken.None);
        Assert.Single(all);
    }

    /// <summary>AC-22 (defensivo): el paciente no puede auto-asignarse una base → 403.</summary>
    [RequiresPostgresFact]
    public async Task UpsertBaseline_LlamadorPaciente_LanzaForbidden()
    {
        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => repo.UpsertClinicalBaselineAsync(
                Write(_metricWeight, 82.5m), callerRoles: ["Patient"], ct: CancellationToken.None));

        Assert.Contains("BASELINE_SET_BY_REQUIRES_CLINICIAN", ex.Message);
    }

    /// <summary>AC-22 (defensivo): target_value inválido (<= 0 o fuera de límites) → 422.</summary>
    [RequiresPostgresFact]
    public async Task UpsertBaseline_TargetInvalido_LanzaUnprocessable()
    {
        await using var db = fixture.CreateDbContext();
        var repo = new ProgramRepository(db, Configuration());

        // target <= 0
        var ex1 = await Assert.ThrowsAsync<UnprocessableEntityException>(
            () => repo.UpsertClinicalBaselineAsync(
                Write(_metricWeight, 82.5m, targetValue: 0m), callerRoles: ["Physician"],
                ct: CancellationToken.None));
        Assert.Contains("TARGET_VALUE_MUST_BE_POSITIVE", ex1.Message);

        // target fuera del límite duro (1_000_000)
        var ex2 = await Assert.ThrowsAsync<UnprocessableEntityException>(
            () => repo.UpsertClinicalBaselineAsync(
                Write(_metricWeight, 82.5m, targetValue: 2_000_000m), callerRoles: ["Physician"],
                ct: CancellationToken.None));
        Assert.Contains("TARGET_VALUE_OUT_OF_BOUNDS", ex2.Message);

        // target fuera del rango de referencia de la métrica (±10% de 10..100)
        db.MeasurementReferenceRanges.Add(new MeasurementReferenceRange
        {
            MetricId = _metricWeight,
            UnitId = _unitKg,
            MinValue = 10m,
            MaxValue = 100m,
            Priority = 1,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var ex3 = await Assert.ThrowsAsync<UnprocessableEntityException>(
            () => repo.UpsertClinicalBaselineAsync(
                Write(_metricWeight, 82.5m, targetValue: 150m), callerRoles: ["Physician"],
                ct: CancellationToken.None));
        Assert.Contains("TARGET_VALUE_OUT_OF_BOUNDS", ex3.Message);
    }

    // ---------------------------------------------------------------- Helpers

    private ClinicalBaselineWrite Write(Guid metricId, decimal value, decimal? targetValue = null)
        => new(
            PatientId: _patientId,
            MetricId: metricId,
            Value: value,
            UnitId: _unitKg,
            FavorableDirection: FavorableDirection.LowerIsBetter,
            MeasuredAt: _monday,
            SetBy: _clinicianUserId,
            TargetValue: targetValue);

    private void SeedCheckin(AppDbContext db, Guid weekId, DateOnly date, bool perfect)
        => db.DailyCheckIns.Add(new DailyCheckIn
        {
            Id = Guid.NewGuid(),
            EnrollmentId = _enrollmentId,
            ProgramWeekId = weekId,
            LocalDate = date,
            Weekday = ToIsoWeekday(date),
            TotalPoints = perfect ? 750 : 300,
            BonusAwarded = perfect ? 50 : 0,
            IsPerfectDay = perfect,
        });

    private void SeedEjercicio(AppDbContext db, Guid weekId, DateOnly date)
    {
        // El check-in se agregó al contexto sin guardar: se resuelve del
        // ChangeTracker (.Local), no de la BD. Id explícito en ambos para que
        // la FK no dependa de la generación de claves de EF.
        var checkin = db.DailyCheckIns.Local.Single(c => c.LocalDate == date);
        db.TaskCompletions.Add(new TaskCompletion
        {
            Id = Guid.NewGuid(),
            EnrollmentId = _enrollmentId,
            ProgramWeekId = weekId,
            DailyCheckinId = checkin.Id,
            LocalDate = date,
            Weekday = ToIsoWeekday(date),
            TaskCode = TaskCode.ejercicio,
            PointsAwarded = 150,
            CompletedAt = DateTime.UtcNow,
            SourceRefType = "manual",
        });
    }

    private void SeedBaselineAndMeasurement(
        AppDbContext db, Guid metricId, decimal baseline, decimal current, DateOnly localDate)
    {
        db.ClinicalBaselines.Add(new ClinicalBaseline
        {
            PatientId = _patientId,
            MetricId = metricId,
            Value = baseline,
            UnitId = _unitKg,
            FavorableDirection = FavorableDirection.LowerIsBetter,
            MeasuredAt = localDate,
            SetBy = _clinicianUserId,
        });

        db.ClinicalMeasurements.Add(new ClinicalMeasurement
        {
            PatientId = _patientId,
            MetricId = metricId,
            Value = current,
            UnitId = _unitKg,
            ObservedAt = LocalDateToUtc(localDate),
            Source = "professional",
        });
    }

    private static async Task SeedDefaultWeightsAsync(AppDbContext db)
    {
        db.HealthScoreWeights.AddRange(
            new HealthScoreWeight { Dimension = ScoreDimension.adherence, Weight = 0.30m },
            new HealthScoreWeight { Dimension = ScoreDimension.clinical, Weight = 0.30m },
            new HealthScoreWeight { Dimension = ScoreDimension.nutrition, Weight = 0.20m },
            new HealthScoreWeight { Dimension = ScoreDimension.psychology, Weight = 0.10m },
            new HealthScoreWeight { Dimension = ScoreDimension.exercise, Weight = 0.10m });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> CreatePatientAsync(AppDbContext db, string firstName, string lastName)
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

    private static async Task<Guid> CreateAuthUserAsync(AppDbContext db)
    {
        var userId = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO auth.\"Users\" (\"Id\") VALUES ({0}) ON CONFLICT DO NOTHING;", userId);
        return userId;
    }

    private static async Task<(Guid Unit, Guid Weight, Guid Bmi, Guid Glucose)> SeedClinicalCatalogAsync(AppDbContext db)
    {
        var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Code = "kg", Name = "Kilogramo", Symbol = "kg" };
        var weight = new MeasurementMetric
        {
            Id = Guid.NewGuid(), Code = "weight", Name = "Peso",
            DefaultUnitId = unit.Id, Category = "body_comp",
        };
        var bmi = new MeasurementMetric
        {
            Id = Guid.NewGuid(), Code = "bmi", Name = "IMC",
            DefaultUnitId = unit.Id, Category = "body_comp",
        };
        var glucose = new MeasurementMetric
        {
            Id = Guid.NewGuid(), Code = "glucose", Name = "Glucosa",
            DefaultUnitId = unit.Id, Category = "metabolic",
        };
        db.UnitOfMeasures.Add(unit);
        db.MeasurementMetrics.AddRange(weight, bmi, glucose);
        await db.SaveChangesAsync();
        return (unit.Id, weight.Id, bmi.Id, glucose.Id);
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
                template.DayTemplates.Add(new WeeklyDayTemplate
                {
                    Weekday = weekday,
                    TaskCode = TaskSeeds[i].Code,
                    Points = TaskSeeds[i].Points,
                    SortOrder = i + 1,
                });
            }
        }

        db.ProgramTemplates.Add(template);
        await db.SaveChangesAsync();
        return template.Id;
    }

    private static async Task ResetTablesAsync(AppDbContext db)
        => await db.Database.ExecuteSqlRawAsync("""
            TRUNCATE app.task_completions, app.xp_ledger, app.daily_checkins, app.streak_states,
                     app.streak_freezes, app.emotional_records, app.program_weeks,
                     app.program_enrollments, app.adaptation_recommendations,
                     app.weekly_day_templates, app.program_templates, app.patient_profiles,
                     app.health_scores, app.transformation_scores, app.clinical_baselines,
                     app.clinical_measurements, app.measurement_metrics, app.unit_of_measures,
                     app.health_score_weights, app.measurement_reference_ranges CASCADE;
            """);

    private static DateOnly ThisMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-daysSinceMonday);
    }

    private static DateOnly PatientLocalToday()
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Bogota")));

    private static DateTime LocalDateToUtc(DateOnly date)
        => TimeZoneInfo.ConvertTimeToUtc(
            date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            TimeZoneInfo.FindSystemTimeZoneById("America/Bogota"));

    private static short ToIsoWeekday(DateOnly date)
        => (short)(((int)date.DayOfWeek + 6) % 7 + 1);

    private static IConfiguration Configuration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Program:Streak:FreezeGrantEveryPerfectDays"] = "7",
            })
            .Build();
}