using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Metrics;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.UnitTests.ProgramProgress;

/// <summary>
/// Backfill/recomputo del rollup de Biometría (<c>app.biometria_daily_metrics</c>,
/// CQRS Channel Pattern — módulo ProgramProgress): ejercita
/// <see cref="BiometriaRollupSql.RecomputeAllAsync"/> contra PostgreSQL real
/// (el SQL usa CASE/CTE/ON CONFLICT específicos de PostgreSQL, no testeables con
/// InMemory). Reutiliza la BD aislada del fixture de ProgramProgress
/// (<c>COP_TEST_DB_CONNECTION</c>); sin la variable los tests se omiten.
///
/// Cada test siembra mediciones en una fecha distinta para no interferir con
/// los demás (el recomputo es global por set-based).
/// </summary>
public sealed class BiometriaRollupBackfillTests : IClassFixture<ProgramRepositoryTestDb>
{
    private readonly ProgramRepositoryTestDb _fixture;

    public BiometriaRollupBackfillTests(ProgramRepositoryTestDb fixture) => _fixture = fixture;

    // ------------------------------------------------------------ Escenario 1

    [RequiresPostgresFact]
    public async Task RecomputeAll_ImcDistribution_ClasificaBucketsOmsYComunidad()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));
        var observedAt = ObservedAt(date);

        await using (var db = _fixture.CreateDbContext())
        {
            var catalog = await EnsureCatalogAsync(db);
            var patient = await AddPatientAsync(db, "Femenino");

            // Un valor por bucket + dos en Normal + un 0 que el processor ignora.
            foreach (var value in new[] { 17.0m, 22.0m, 22.5m, 27.0m, 32.0m, 40.0m, 0m })
            {
                AddMeasurement(db, patient, catalog.BmiId, catalog.UnitId, value, observedAt);
            }

            await db.SaveChangesAsync();
        }

        await RecomputeAsync();

        await using var check = _fixture.CreateDbContext();
        var rows = await ReadMetricsAsync(check, date);

        Assert.Equal(1, Row(rows, "imc_distribution", "Bajo peso").TotalCount);
        Assert.Equal(17.0m, Row(rows, "imc_distribution", "Bajo peso").TotalValue);
        Assert.Equal(2, Row(rows, "imc_distribution", "Normal").TotalCount);
        Assert.Equal(44.5m, Row(rows, "imc_distribution", "Normal").TotalValue);
        Assert.Equal(1, Row(rows, "imc_distribution", "Sobrepeso").TotalCount);
        Assert.Equal(1, Row(rows, "imc_distribution", "Obesidad I").TotalCount);
        Assert.Equal(1, Row(rows, "imc_distribution", "Obesidad II-III").TotalCount);

        // community_avg: cuenta y suma los 6 valores > 0 (el 0 queda fuera).
        Assert.Equal(6, Row(rows, "community_avg", "imc").TotalCount);
        Assert.Equal(160.5m, Row(rows, "community_avg", "imc").TotalValue);
        Assert.DoesNotContain(
            rows,
            r => r.MetricKey == "community_avg" && r.DimensionKey == "grasa"
        );
        Assert.DoesNotContain(
            rows,
            r => r.MetricKey == "community_avg" && r.DimensionKey == "glucosa"
        );
    }

    // ------------------------------------------------------------ Escenario 2

    [RequiresPostgresFact]
    public async Task RecomputeAll_GrasaDistribution_ClasificaPorSexoConCortesDelDashboard()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-11));
        var observedAt = ObservedAt(date);

        await using (var db = _fixture.CreateDbContext())
        {
            var catalog = await EnsureCatalogAsync(db);

            // " Masculino " prueba trim + case-insensitive del processor;
            // gender null cae a female (mismo fallback del processor).
            var male = await AddPatientAsync(db, " Masculino ");
            var female = await AddPatientAsync(db, null);

            // Cortes del dashboard (ProgramRepository.ClassifyGrasa):
            // <=18 Óptimo / <=24 Normal / <=29 Alto / resto Obesidad (male).
            foreach (var value in new[] { 18m, 19m, 24m, 25m, 29m, 30m })
            {
                AddMeasurement(db, male, catalog.BodyFatId, catalog.UnitId, value, observedAt);
            }

            // <=23 / <=31 / <=37 / resto Obesidad (female).
            foreach (var value in new[] { 23m, 24m, 31m, 32m, 37m, 38m })
            {
                AddMeasurement(db, female, catalog.BodyFatId, catalog.UnitId, value, observedAt);
            }

            // Fila obsoleta de la escala atlética previa: la purga de la clave
            // debe eliminarla antes de recomputar (el upsert no la tocaría).
            db.BiometriaDailyMetrics.Add(
                new BiometriaDailyMetric
                {
                    MetricDate = date,
                    MetricKey = "grasa_distribution",
                    DimensionKey = "male_Esencial",
                    TotalCount = 99,
                    TotalValue = 0m,
                    LastUpdatedAt = DateTime.UtcNow,
                }
            );

            await db.SaveChangesAsync();
        }

        await RecomputeAsync();

        await using var check = _fixture.CreateDbContext();
        var rows = await ReadMetricsAsync(check, date);

        Assert.Equal(1, Row(rows, "grasa_distribution", "male_Óptimo").TotalCount);
        Assert.Equal(2, Row(rows, "grasa_distribution", "male_Normal").TotalCount);
        Assert.Equal(2, Row(rows, "grasa_distribution", "male_Alto").TotalCount);
        Assert.Equal(1, Row(rows, "grasa_distribution", "male_Obesidad").TotalCount);

        Assert.Equal(1, Row(rows, "grasa_distribution", "female_Óptimo").TotalCount);
        Assert.Equal(2, Row(rows, "grasa_distribution", "female_Normal").TotalCount);
        Assert.Equal(2, Row(rows, "grasa_distribution", "female_Alto").TotalCount);
        Assert.Equal(1, Row(rows, "grasa_distribution", "female_Obesidad").TotalCount);

        // La purga elimina la categoría huérfana de la escala anterior.
        Assert.DoesNotContain(
            rows,
            r =>
                r.MetricKey == "grasa_distribution"
                && r.DimensionKey.Contains("Esencial", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            rows,
            r => r.MetricKey == "grasa_distribution" && r.DimensionKey == "male_Esencial"
        );

        Assert.Equal(12, Row(rows, "community_avg", "grasa").TotalCount);
        Assert.Equal(330.0m, Row(rows, "community_avg", "grasa").TotalValue);
    }

    // ------------------------------------------------------------ Escenario 3

    [RequiresPostgresFact]
    public async Task RecomputeAll_GlucosaDistribution_ClasificaAdaYExcluyeSinDato()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-12));
        var observedAt = ObservedAt(date);

        await using (var db = _fixture.CreateDbContext())
        {
            var catalog = await EnsureCatalogAsync(db);
            var patient = await AddPatientAsync(db, "Masculino");

            foreach (var value in new[] { 99m, 100m, 125.9m, 126m, 200m })
            {
                AddMeasurement(db, patient, catalog.GlucoseId, catalog.UnitId, value, observedAt);
            }

            await db.SaveChangesAsync();
        }

        await RecomputeAsync();

        await using var check = _fixture.CreateDbContext();
        var rows = await ReadMetricsAsync(check, date);

        Assert.Equal(1, Row(rows, "glucosa_distribution", "Normal").TotalCount);
        Assert.Equal(2, Row(rows, "glucosa_distribution", "Prediabetes").TotalCount);
        Assert.Equal(2, Row(rows, "glucosa_distribution", "Elevada").TotalCount);
        Assert.Equal(5, Row(rows, "community_avg", "glucosa").TotalCount);
        Assert.Equal(650.9m, Row(rows, "community_avg", "glucosa").TotalValue);

        // "Sin dato" (vitals sin glucosa) es SOLO de evento: el backfill no lo escribe.
        Assert.DoesNotContain(
            rows,
            r => r.MetricKey == "glucosa_distribution" && r.DimensionKey == "Sin dato"
        );
    }

    // ------------------------------------------------------------ Escenario 4

    [RequiresPostgresFact]
    public async Task RecomputeAll_EsIdempotente()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-13));
        var observedAt = ObservedAt(date);

        await using (var db = _fixture.CreateDbContext())
        {
            var catalog = await EnsureCatalogAsync(db);
            var patient = await AddPatientAsync(db, "Femenino");

            AddMeasurement(db, patient, catalog.BmiId, catalog.UnitId, 24.0m, observedAt);
            AddMeasurement(db, patient, catalog.BodyFatId, catalog.UnitId, 30.0m, observedAt);
            AddMeasurement(db, patient, catalog.GlucoseId, catalog.UnitId, 90.0m, observedAt);

            await db.SaveChangesAsync();
        }

        await RecomputeAsync();

        await using (var first = _fixture.CreateDbContext())
        {
            var rowsFirst = await ReadMetricsAsync(first, date);
            // 1 bucket IMC + 1 grasa + 1 glucosa + 3 community_avg.
            Assert.Equal(6, rowsFirst.Count);

            await RecomputeAsync();

            await using var second = _fixture.CreateDbContext();
            var rowsSecond = await ReadMetricsAsync(second, date);
            Assert.Equal(rowsFirst.Count, rowsSecond.Count);

            for (var i = 0; i < rowsFirst.Count; i++)
            {
                Assert.Equal(rowsFirst[i].MetricKey, rowsSecond[i].MetricKey);
                Assert.Equal(rowsFirst[i].DimensionKey, rowsSecond[i].DimensionKey);
                Assert.Equal(rowsFirst[i].TotalCount, rowsSecond[i].TotalCount);
                Assert.Equal(rowsFirst[i].TotalValue, rowsSecond[i].TotalValue);
            }
        }
    }

    // ------------------------------------------------------------ Escenario 5

    [RequiresPostgresFact]
    public async Task RecomputeAll_NoTocaLasClavesSoloDeEvento()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14));

        await using (var db = _fixture.CreateDbContext())
        {
            var catalog = await EnsureCatalogAsync(db);
            var patient = await AddPatientAsync(db, "Femenino");
            AddMeasurement(db, patient, catalog.GlucoseId, catalog.UnitId, 80m, ObservedAt(date));

            // Filas que SOLO puede escribir el processor de eventos.
            db.BiometriaDailyMetrics.AddRange(
                new BiometriaDailyMetric
                {
                    MetricDate = date,
                    MetricKey = "glucosa_distribution",
                    DimensionKey = "Sin dato",
                    TotalCount = 3,
                    TotalValue = 0m,
                    LastUpdatedAt = DateTime.UtcNow,
                },
                new BiometriaDailyMetric
                {
                    MetricDate = date,
                    MetricKey = "city_patient_count",
                    DimensionKey = "city-1",
                    TotalCount = 2,
                    TotalValue = 0m,
                    LastUpdatedAt = DateTime.UtcNow,
                }
            );

            await db.SaveChangesAsync();
        }

        await RecomputeAsync();

        await using var check = _fixture.CreateDbContext();
        var rows = await ReadMetricsAsync(check, date);

        Assert.Equal(3, Row(rows, "glucosa_distribution", "Sin dato").TotalCount);
        Assert.Equal(2, Row(rows, "city_patient_count", "city-1").TotalCount);
        Assert.Equal(1, Row(rows, "glucosa_distribution", "Normal").TotalCount);
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    private sealed record CatalogIds(Guid UnitId, Guid BmiId, Guid BodyFatId, Guid GlucoseId);

    /// <summary>Recomputo set-based compartido (mismo entry point que el backfill real).</summary>
    private async Task RecomputeAsync()
    {
        await using var db = _fixture.CreateDbContext();
        await BiometriaRollupSql.RecomputeAllAsync(db);
    }

    /// <summary>Unidad + métricas bmi/body_fat/glucose_fasting (idempotente entre escenarios).</summary>
    private static async Task<CatalogIds> EnsureCatalogAsync(AppDbContext db)
    {
        var unit = await db.UnitOfMeasures.FirstOrDefaultAsync(u => u.Code == "test_unit");
        if (unit is null)
        {
            unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Code = "test_unit",
                Name = "Unidad de prueba",
                Symbol = "u",
                IsActive = true,
            };
            db.UnitOfMeasures.Add(unit);
            await db.SaveChangesAsync();
        }

        return new CatalogIds(
            unit.Id,
            await EnsureMetricAsync(db, "bmi", unit.Id, "body_comp"),
            await EnsureMetricAsync(db, "body_fat", unit.Id, "body_comp"),
            await EnsureMetricAsync(db, "glucose_fasting", unit.Id, "metabolic")
        );
    }

    private static async Task<Guid> EnsureMetricAsync(
        AppDbContext db,
        string code,
        Guid unitId,
        string category
    )
    {
        var metric = await db.MeasurementMetrics.FirstOrDefaultAsync(m => m.Code == code);
        if (metric is not null)
        {
            return metric.Id;
        }

        metric = new MeasurementMetric
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = code,
            DefaultUnitId = unitId,
            Category = category,
            IsActive = true,
        };
        db.MeasurementMetrics.Add(metric);
        await db.SaveChangesAsync();
        return metric.Id;
    }

    private static async Task<Guid> AddPatientAsync(AppDbContext db, string? gender)
    {
        var patient = new PatientProfile
        {
            Id = Guid.NewGuid(),
            FirstName = "Paciente",
            LastName = "Biometría",
            Gender = gender,
        };
        db.PatientProfiles.Add(patient);
        await db.SaveChangesAsync();
        return patient.Id;
    }

    private static void AddMeasurement(
        AppDbContext db,
        Guid patientId,
        Guid metricId,
        Guid unitId,
        decimal value,
        DateTime observedAt
    ) =>
        db.ClinicalMeasurements.Add(
            new ClinicalMeasurement
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                MetricId = metricId,
                UnitId = unitId,
                Value = value,
                ObservedAt = observedAt,
                Source = "mobile",
                CreatedAt = DateTime.UtcNow,
            }
        );

    private static async Task<IReadOnlyList<BiometriaDailyMetric>> ReadMetricsAsync(
        AppDbContext db,
        DateOnly date
    ) =>
        await db.BiometriaDailyMetrics.AsNoTracking()
            .Where(x => x.MetricDate == date)
            .OrderBy(x => x.MetricKey)
            .ThenBy(x => x.DimensionKey)
            .ToListAsync();

    private static BiometriaDailyMetric Row(
        IReadOnlyList<BiometriaDailyMetric> rows,
        string metricKey,
        string dimensionKey
    ) => rows.Single(x => x.MetricKey == metricKey && x.DimensionKey == dimensionKey);

    private static DateTime ObservedAt(DateOnly date) =>
        date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
}
