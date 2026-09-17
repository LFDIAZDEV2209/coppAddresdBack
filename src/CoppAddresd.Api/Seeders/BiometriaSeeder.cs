using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Metrics;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Api.Seeders;

/// <summary>
/// Seeder de Biometría: inserta mediciones clínicas (weight, height, waist, hip, wrist)
/// para las inscripciones activas del programa (12 semanas, 1 medición por semana)
/// y completa datos demográficos faltantes en patient_profiles.
///
/// Idempotente: verifica la existencia de mediciones antes de insertar (source = "seed:biometria").
/// Se ejecuta DESPUÉS de ClinicalMeasurementsSeeder (necesita las métricas waist/hip/wrist).
/// </summary>
public sealed class BiometriaSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<BiometriaSeeder> logger) : IHostedService
{
    private const string BiometriaSource = "seed:biometria";
    private const string DefaultTimezone = "America/Bogota";

    // Demographics seed data for patients without gender/dob/city
    private static readonly (string FirstName, string LastName, string Gender, DateOnly Dob, string CityName)[] DemographicsSeeds =
    [
        ("Ana", "García", "Femenino", new DateOnly(1985, 3, 15), "Bogotá"),
        ("Carlos", "Rodríguez", "Masculino", new DateOnly(1978, 7, 22), "Medellín"),
        ("María", "López", "Femenino", new DateOnly(1990, 11, 5), "Cali"),
        ("Pedro", "Martínez", "Masculino", new DateOnly(1982, 1, 30), "Barranquilla"),
    ];

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SeedAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Seed de Biometría cancelado");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo el seed de Biometría");
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        await SeedDemographicsAsync(ct);
        await SeedClinicalMeasurementsAsync(ct);
    }

    private async Task SeedDemographicsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Get patients without gender, date_of_birth, or city_id
        var patientsNeedingDemographics = await db.PatientProfiles
            .Where(p => p.Status == "Activo" && p.DeletedAt == null
                && (p.Gender == null || p.DateOfBirth == null || p.CityId == null))
            .OrderBy(p => p.CreatedAt)
            .Take(DemographicsSeeds.Length)
            .Select(p => new { p.Id, p.FirstName, p.LastName, p.Gender, p.DateOfBirth, p.CityId })
            .ToListAsync(ct);

        if (patientsNeedingDemographics.Count == 0)
        {
            logger.LogInformation("Biometría: todos los pacientes activos ya tienen datos demográficos.");
            return;
        }

        var cityNames = DemographicsSeeds.Select(d => d.CityName).Distinct().ToList();
        var cities = await db.Cities
            .Where(c => cityNames.Contains(c.Name))
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(ct);

        var cityMap = cities.ToDictionary(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase);

        var updated = 0;
        for (var i = 0; i < patientsNeedingDemographics.Count; i++)
        {
            var patient = patientsNeedingDemographics[i];
            var seed = DemographicsSeeds[i % DemographicsSeeds.Length];

            var patientIdx = i % DemographicsSeeds.Length;
            var (firstName, lastName, gender, dob, cityName) = DemographicsSeeds[patientIdx];

            await db.PatientProfiles
                .Where(p => p.Id == patient.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Gender, patient.Gender ?? gender)
                    .SetProperty(p => p.DateOfBirth, patient.DateOfBirth ?? dob.ToDateTime(TimeOnly.MinValue))
                    .SetProperty(p => p.CityId, patient.CityId ?? (cityMap.TryGetValue(cityName, out var cid) ? cid : null))
                    .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);

            updated++;
        }

        if (updated > 0)
        {
            logger.LogInformation("Biometría: {Count} pacientes con datos demográficos actualizados.", updated);
        }
    }

    private async Task SeedClinicalMeasurementsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Get active enrollments
        var activeEnrollments = await db.ProgramEnrollments
            .Where(e => e.Status == ProgramEnrollmentStatus.Active)
            .Select(e => new { e.Id, e.PatientId, e.StartLocalDate, e.CurrentWeekNumber })
            .ToListAsync(ct);

        if (activeEnrollments.Count == 0)
        {
            logger.LogInformation("Biometría: no hay inscripciones activas para sembrar mediciones.");
            return;
        }

        // Get metric IDs for biometria metrics
        var metricCodes = new[] { "weight", "height", "waist", "hip", "wrist", "bmi", "body_fat", "glucose_fasting" };
        var metricIds = await db.MeasurementMetrics
            .Where(m => metricCodes.Contains(m.Code) && m.IsActive)
            .Select(m => new { m.Id, m.Code })
            .ToListAsync(ct);

        var metricMap = metricIds.ToDictionary(m => m.Code, m => m.Id);
        if (metricMap.Count < metricCodes.Length)
        {
            logger.LogWarning("Biometría: faltan métricas en el catálogo ({Found}/{Expected}). Se omite el seed.",
                metricMap.Count, metricCodes.Length);
            return;
        }

        // Get unit IDs
        var unitCodes = new[] { "kg", "cm", "kg_m2", "pct", "mg_dl" };
        var unitIds = await db.UnitOfMeasures
            .Where(u => unitCodes.Contains(u.Code) && u.IsActive)
            .Select(u => new { u.Id, u.Code })
            .ToListAsync(ct);

        var unitMap = unitIds.ToDictionary(u => u.Code, u => u.Id);

        // Check existing biometria measurements count
        var existingCount = await db.ClinicalMeasurements
            .Where(m => m.Source == BiometriaSource)
            .CountAsync(ct);

        if (existingCount > 0)
        {
            logger.LogInformation("Biometría: ya existen {Count} mediciones sembradas. Seed omitido.", existingCount);
            return;
        }

        var rng = new Random(42); // deterministic seed
        var inserted = 0;

        foreach (var enrollment in activeEnrollments)
        {
            // Base height for this patient (160-180 cm range, deterministic per patient)
            var baseHeight = 160m + (decimal)(rng.Next(0, 21));
            // Base weight derived from a plausible BMI (22-30 range)
            var baseBmi = 22m + (decimal)(rng.Next(0, 90)) / 10m;
            var baseWeight = baseBmi * (baseHeight / 100m) * (baseHeight / 100m);

            var maxWeeks = Math.Min(enrollment.CurrentWeekNumber, 12);

            for (var week = 1; week <= maxWeeks; week++)
            {
                var weekStart = enrollment.StartLocalDate.AddDays((week - 1) * 7);
                var observedAt = weekStart.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc);

                // Weekly variation: small random changes
                var weightVariation = (decimal)(rng.Next(-20, 21)) / 10m; // ±2 kg
                var weekWeight = Math.Round(baseWeight + weightVariation - (week * 0.3m), 1); // slight downward trend
                var weekHeight = Math.Round(baseHeight + (decimal)(rng.Next(-1, 2)) / 10m, 1);
                var weekBmi = Math.Round(weekWeight / ((weekHeight / 100m) * (weekHeight / 100m)), 1);
                var weekBodyFat = Math.Round(25m + (decimal)(rng.Next(-30, 31)) / 10m - (week * 0.2m), 1);
                var weekGlucose = Math.Round(95m + (decimal)(rng.Next(-15, 26)), 1);
                var weekWaist = Math.Round(85m + (decimal)(rng.Next(-10, 11)) - (week * 0.1m), 1);
                var weekHip = Math.Round(100m + (decimal)(rng.Next(-5, 6)), 1);
                var weekWrist = Math.Round(16m + (decimal)(rng.Next(-1, 3)) / 10m, 1);

                // Weight
                db.ClinicalMeasurements.Add(new ClinicalMeasurement
                {
                    Id = Guid.NewGuid(),
                    PatientId = enrollment.PatientId,
                    MetricId = metricMap["weight"],
                    Value = weekWeight,
                    UnitId = unitMap["kg"],
                    ObservedAt = observedAt,
                    Source = BiometriaSource,
                    CreatedAt = DateTime.UtcNow,
                });
                inserted++;

                // Height
                db.ClinicalMeasurements.Add(new ClinicalMeasurement
                {
                    Id = Guid.NewGuid(),
                    PatientId = enrollment.PatientId,
                    MetricId = metricMap["height"],
                    Value = weekHeight,
                    UnitId = unitMap["cm"],
                    ObservedAt = observedAt,
                    Source = BiometriaSource,
                    CreatedAt = DateTime.UtcNow,
                });
                inserted++;

                // Waist
                db.ClinicalMeasurements.Add(new ClinicalMeasurement
                {
                    Id = Guid.NewGuid(),
                    PatientId = enrollment.PatientId,
                    MetricId = metricMap["waist"],
                    Value = weekWaist,
                    UnitId = unitMap["cm"],
                    ObservedAt = observedAt,
                    Source = BiometriaSource,
                    CreatedAt = DateTime.UtcNow,
                });
                inserted++;

                // Hip
                db.ClinicalMeasurements.Add(new ClinicalMeasurement
                {
                    Id = Guid.NewGuid(),
                    PatientId = enrollment.PatientId,
                    MetricId = metricMap["hip"],
                    Value = weekHip,
                    UnitId = unitMap["cm"],
                    ObservedAt = observedAt,
                    Source = BiometriaSource,
                    CreatedAt = DateTime.UtcNow,
                });
                inserted++;

                // BMI
                db.ClinicalMeasurements.Add(new ClinicalMeasurement
                {
                    Id = Guid.NewGuid(),
                    PatientId = enrollment.PatientId,
                    MetricId = metricMap["bmi"],
                    Value = weekBmi,
                    UnitId = unitMap["kg_m2"],
                    ObservedAt = observedAt,
                    Source = BiometriaSource,
                    CreatedAt = DateTime.UtcNow,
                });
                inserted++;

                // Body Fat
                db.ClinicalMeasurements.Add(new ClinicalMeasurement
                {
                    Id = Guid.NewGuid(),
                    PatientId = enrollment.PatientId,
                    MetricId = metricMap["body_fat"],
                    Value = weekBodyFat,
                    UnitId = unitMap["pct"],
                    ObservedAt = observedAt,
                    Source = BiometriaSource,
                    CreatedAt = DateTime.UtcNow,
                });
                inserted++;

                // Glucose Fasting
                db.ClinicalMeasurements.Add(new ClinicalMeasurement
                {
                    Id = Guid.NewGuid(),
                    PatientId = enrollment.PatientId,
                    MetricId = metricMap["glucose_fasting"],
                    Value = weekGlucose,
                    UnitId = unitMap["mg_dl"],
                    ObservedAt = observedAt,
                    Source = BiometriaSource,
                    CreatedAt = DateTime.UtcNow,
                });
                inserted++;
            }
        }

        if (inserted > 0)
        {
            await db.SaveChangesAsync(ct);
            await BackfillBiometriaRollupAsync(db, ct);
        }

        logger.LogInformation(
            "Biometría seed completado: {Enrollments} inscripciones, {Inserted} mediciones insertadas y rollup actualizado.",
            activeEnrollments.Count, inserted);
    }

    private static Task BackfillBiometriaRollupAsync(AppDbContext db, CancellationToken ct)
        // Recomputo set-based e idempotente compartido con el backfill de arranque
        // (MetricsBackfillSeeder) y el endpoint reconcile-metrics.
        => BiometriaRollupSql.RecomputeAllAsync(db, ct);
}
