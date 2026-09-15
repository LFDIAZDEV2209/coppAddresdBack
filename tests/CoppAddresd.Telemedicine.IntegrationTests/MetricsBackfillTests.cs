using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Infrastructure.Metrics;
using CoppAddresd.Telemedicine.Infrastructure.Repositories;
using CoppAddresd.Telemedicine.UnitTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.IntegrationTests;

/// <summary>
/// Backfill de métricas pre-agregadas contra PostgreSQL real: agregados por
/// día/profesional/estado/hora, espejo global, stats con pacientes únicos,
/// idempotencia y dry-run. Cada test usa clínica y profesionales únicos (la BD
/// de la colección es compartida) y filtra por esa clínica.
/// </summary>
[Collection(TelemedicineTestCollection.Name)]
public class MetricsBackfillTests
{
    private readonly TelemedicineTestContext _ctx;

    public MetricsBackfillTests(TelemedicineTestDatabase database)
    {
        _ctx = new TelemedicineTestContext(database.ConnectionString);
    }

    private static DateTimeOffset UtcNoon(DateOnly day) =>
        new(day.ToDateTime(TimeOnly.MinValue).AddHours(12), TimeSpan.Zero);

    /// <summary>Hora UTC exacta del día (las citas del mismo profesional no pueden solaparse).</summary>
    private static DateTimeOffset UtcAt(DateOnly day, int hour) =>
        new(day.ToDateTime(TimeOnly.MinValue).AddHours(hour), TimeSpan.Zero);

    private static Appointment Appointment(
        Guid professionalId,
        Guid patientId,
        Guid clinicId,
        DateTimeOffset start,
        AppointmentStatus status
    ) =>
        new()
        {
            ProfessionalId = professionalId,
            PatientId = patientId,
            SpecialtyId = TestData.SpecialtyId,
            OrganizationId = TestData.Org,
            ClinicId = clinicId,
            LocationId = TestData.LocationId,
            ScheduledStart = start,
            ScheduledEnd = start.AddMinutes(30),
            DurationMinutes = 30,
            Status = status,
            CreatedBy = TestData.UserId,
        };

    private MetricsBackfillService Service() =>
        new(_ctx.Create(), NullLogger<MetricsBackfillService>.Instance);

    [Fact]
    public async Task Backfill_AgregaPorDiaProfesionalEstadoYHora()
    {
        var clinic = Guid.NewGuid();
        var profA = Guid.NewGuid();
        var profB = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();
        var d1 = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-10));
        var d2 = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-5));

        await using (var db = _ctx.Create())
        {
            var repo = new AppointmentRepository(db);
            await repo.AddAsync(
                Appointment(profA, p1, clinic, UtcAt(d1, 9), AppointmentStatus.Confirmed)
            );
            await repo.AddAsync(
                Appointment(profA, p2, clinic, UtcAt(d1, 12), AppointmentStatus.Confirmed)
            );
            await repo.AddAsync(
                Appointment(profA, p1, clinic, UtcAt(d1, 15), AppointmentStatus.Completed)
            );
            await repo.AddAsync(
                Appointment(profB, p3, clinic, UtcAt(d1, 12), AppointmentStatus.Cancelled)
            );
            await repo.AddAsync(
                Appointment(profA, p1, clinic, UtcAt(d2, 10), AppointmentStatus.NoShow)
            );
            // Otra clínica en el mismo rango: debe quedar excluida.
            await repo.AddAsync(
                Appointment(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    UtcNoon(d1),
                    AppointmentStatus.Confirmed
                )
            );
        }

        var result = await Service()
            .BackfillAsync(d1, d2, clinic, dryRun: false, CancellationToken.None);

        Assert.False(result.DryRun);
        Assert.Equal(5, result.AppointmentsConsidered);
        // daily_total: (D1,A)=3,(D1,B)=1,(D2,A)=1 + globales D1=4,D2=1 → 5
        // status_count: D1/A ×2, D1/B ×1, D2/A ×1 + globales ×4 → 8
        // hourly_count: A/D1 ×3, B/D1 ×1, A/D2 ×1 + globales D1 ×3, D2 ×1 → 9.
        // Total 22. Stats: 3 filas.
        Assert.Equal(22, result.MetricsRows);
        Assert.Equal(3, result.StatsRows);

        await using var check = _ctx.Create();
        var metrics = await check
            .AppointmentDailyMetrics.AsNoTracking()
            .Where(m => m.ClinicId == clinic)
            .ToListAsync();
        Assert.Equal(22, metrics.Count);

        long Total(DateOnly day, Guid prof, string key, string dim) =>
            metrics
                .Where(m =>
                    m.MetricDate == day
                    && m.ProfessionalId == prof
                    && m.MetricKey == key
                    && m.DimensionKey == dim
                )
                .Sum(m => m.TotalCount);

        Assert.Equal(3, Total(d1, profA, "daily_total", "general"));
        Assert.Equal(4, Total(d1, Guid.Empty, "daily_total", "general"));
        Assert.Equal(1, Total(d2, Guid.Empty, "daily_total", "general"));
        Assert.Equal(2, Total(d1, profA, "status_count", nameof(AppointmentStatus.Confirmed)));
        Assert.Equal(1, Total(d1, profB, "status_count", nameof(AppointmentStatus.Cancelled)));
        Assert.Equal(1, Total(d2, profA, "status_count", nameof(AppointmentStatus.NoShow)));
        Assert.Equal(1, Total(d1, profA, "hourly_count", "Hour_09"));
        Assert.Equal(2, Total(d1, Guid.Empty, "hourly_count", "Hour_12"));

        var stats = await check
            .ProfessionalDailyStats.AsNoTracking()
            .Where(s => s.ClinicId == clinic)
            .ToListAsync();
        Assert.Equal(3, stats.Count);
        var a1 = Assert.Single(stats, s => s.ProfessionalId == profA && s.MetricDate == d1);
        Assert.Equal(3, a1.TotalAppointments);
        Assert.Equal(1, a1.CompletedAppointments);
        Assert.Equal(0, a1.CancelledAppointments);
        Assert.Equal(0, a1.NoShowAppointments);
        Assert.Equal(2, a1.UniquePatients);
    }

    [Fact]
    public async Task Backfill_Reejecucion_EsIdempotente()
    {
        var clinic = Guid.NewGuid();
        var prof = Guid.NewGuid();
        var d = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-7));

        await using (var db = _ctx.Create())
        {
            var repo = new AppointmentRepository(db);
            await repo.AddAsync(
                Appointment(prof, Guid.NewGuid(), clinic, UtcNoon(d), AppointmentStatus.Completed)
            );
        }

        var first = await Service()
            .BackfillAsync(d, d, clinic, dryRun: false, CancellationToken.None);
        var second = await Service()
            .BackfillAsync(d, d, clinic, dryRun: false, CancellationToken.None);

        Assert.Equal(first.MetricsRows, second.MetricsRows);
        Assert.Equal(first.StatsRows, second.StatsRows);

        await using var check = _ctx.Create();
        var total = await check
            .AppointmentDailyMetrics.AsNoTracking()
            .Where(m =>
                m.ClinicId == clinic && m.MetricKey == "daily_total" && m.DimensionKey == "general"
            )
            .SumAsync(m => m.TotalCount);
        // 1 profesional + 1 global, sin duplicados por la re-ejecución.
        Assert.Equal(2, total);
    }

    [Fact]
    public async Task Backfill_MismoProfesionalDosClinicas_NoFalla()
    {
        // Regresión: la PK de las tablas pre-agregadas no incluye la clínica;
        // agrupar por clínica generaba dos filas con la misma PK y el
        // ON CONFLICT abortaba con 21000.
        var clinicA = Guid.NewGuid();
        var clinicB = Guid.NewGuid();
        var prof = Guid.NewGuid();
        var d = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-6));

        await using (var db = _ctx.Create())
        {
            var repo = new AppointmentRepository(db);
            await repo.AddAsync(
                Appointment(prof, Guid.NewGuid(), clinicA, UtcAt(d, 9), AppointmentStatus.Completed)
            );
            await repo.AddAsync(
                Appointment(
                    prof,
                    Guid.NewGuid(),
                    clinicA,
                    UtcAt(d, 11),
                    AppointmentStatus.Completed
                )
            );
            await repo.AddAsync(
                Appointment(
                    prof,
                    Guid.NewGuid(),
                    clinicB,
                    UtcAt(d, 14),
                    AppointmentStatus.Completed
                )
            );
        }

        var result = await Service()
            .BackfillAsync(d, d, clinicId: null, dryRun: false, CancellationToken.None);

        Assert.Equal(3, result.AppointmentsConsidered);
        // daily_total: profesional + global; status: Completed prof + global;
        // horas: 3 del profesional + 3 globales.
        Assert.Equal(2 + 2 + 6, result.MetricsRows);
        Assert.Equal(1, result.StatsRows);

        await using var check = _ctx.Create();
        var daily = await check
            .AppointmentDailyMetrics.AsNoTracking()
            .Where(m =>
                m.MetricDate == d
                && m.MetricKey == "daily_total"
                && m.DimensionKey == "general"
                && (m.ProfessionalId == prof || m.ProfessionalId == Guid.Empty)
            )
            .ToListAsync();
        Assert.Equal(3, daily.Single(m => m.ProfessionalId == prof).TotalCount);
        Assert.Equal(3, daily.Single(m => m.ProfessionalId == Guid.Empty).TotalCount);

        var stat = await check
            .ProfessionalDailyStats.AsNoTracking()
            .SingleAsync(s => s.ProfessionalId == prof && s.MetricDate == d);
        Assert.Equal(3, stat.TotalAppointments);
        Assert.Equal(3, stat.CompletedAppointments);
        Assert.Equal(3, stat.UniquePatients);
    }

    [Fact]
    public async Task Backfill_CubreCitasFuturas()
    {
        // Las citas programadas a futuro también llevan filas pre-agregadas;
        // sin ellas, los rangos que tocan futuro (p. ej. próximos 7 días)
        // quedarían subcontados porque los lectores prefieren el pre-agregado.
        var clinic = Guid.NewGuid();
        var prof = Guid.NewGuid();
        var future = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(5));

        await using (var db = _ctx.Create())
        {
            var repo = new AppointmentRepository(db);
            await repo.AddAsync(
                Appointment(
                    prof,
                    Guid.NewGuid(),
                    clinic,
                    UtcAt(future, 10),
                    AppointmentStatus.Confirmed
                )
            );
            await repo.AddAsync(
                Appointment(
                    prof,
                    Guid.NewGuid(),
                    clinic,
                    UtcAt(future, 12),
                    AppointmentStatus.Confirmed
                )
            );
        }

        var result = await Service()
            .BackfillAsync(future, future, clinic, dryRun: false, CancellationToken.None);

        Assert.Equal(2, result.AppointmentsConsidered);

        await using var check = _ctx.Create();
        var checkRepo = new AppointmentRepository(check);
        var from = future.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        Assert.Equal(
            2,
            await checkRepo.CountInRangeAsync(
                null,
                new DateTimeOffset(from),
                new DateTimeOffset(from).AddDays(1),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task Backfill_DryRun_NoEscribeNada()
    {
        var clinic = Guid.NewGuid();
        var prof = Guid.NewGuid();
        var d = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-8));

        await using (var db = _ctx.Create())
        {
            var repo = new AppointmentRepository(db);
            await repo.AddAsync(
                Appointment(prof, Guid.NewGuid(), clinic, UtcNoon(d), AppointmentStatus.Confirmed)
            );
        }

        var result = await Service()
            .BackfillAsync(d, d, clinic, dryRun: true, CancellationToken.None);

        Assert.True(result.DryRun);
        Assert.Equal(1, result.AppointmentsConsidered);
        Assert.True(result.MetricsRows > 0);

        await using var check = _ctx.Create();
        Assert.False(
            await check.AppointmentDailyMetrics.AsNoTracking().AnyAsync(m => m.ClinicId == clinic)
        );
        Assert.False(
            await check.ProfessionalDailyStats.AsNoTracking().AnyAsync(s => s.ClinicId == clinic)
        );
    }
}
