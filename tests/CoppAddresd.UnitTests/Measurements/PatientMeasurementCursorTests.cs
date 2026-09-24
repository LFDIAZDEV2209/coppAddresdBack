using CoppAddresd.Application.Features.Measurements.Queries.GetMyMeasurements;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.UnitTests.Measurements;

/// <summary>
/// Pruebas del cursor keyset de <see cref="PatientMeasurementRepository"/>
/// (Fase 7, móvil): primera página sin cursor, segunda página con cursor
/// válido, cursor malformado → 400, peek de <c>HasNextPage</c> y desempate por
/// <c>id DESC</c> con igual <c>observed_at</c>. Se usa el proveedor InMemory
/// (lógica de paginación, no traducción SQL): una sola query con
/// <c>AsNoTracking</c>, sin N+1.
/// </summary>
public sealed class PatientMeasurementCursorTests
{
    private static AppDbContext CreateDb() =>
        new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options
        );

    private static UnitOfMeasure BuildUnit(string code, string symbol) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Symbol = symbol,
            Name = symbol,
            IsActive = true,
        };

    private static MeasurementMetric BuildMetric(string code, string name, UnitOfMeasure unit) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Category = "vital",
            IsActive = true,
            DefaultUnitId = unit.Id,
            DefaultUnit = unit,
        };

    private static ClinicalMeasurement BuildMeasurement(
        Guid patientId,
        MeasurementMetric metric,
        UnitOfMeasure unit,
        DateTime observedAtUtc,
        decimal value = 70m,
        string source = "device"
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            MetricId = metric.Id,
            Metric = metric,
            UnitId = unit.Id,
            Unit = unit,
            Value = value,
            ObservedAt = DateTime.SpecifyKind(observedAtUtc, DateTimeKind.Utc),
            RecordedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Source = source,
        };

    private static async Task SeedAsync(
        AppDbContext db,
        Guid patientId,
        IReadOnlyList<ClinicalMeasurement> rows
    )
    {
        var units = rows.Select(r => r.Unit!).DistinctBy(u => u.Id).ToList();
        var metrics = rows.Select(r => r.Metric!).DistinctBy(m => m.Id).ToList();
        await db.UnitOfMeasures.AddRangeAsync(units);
        await db.MeasurementMetrics.AddRangeAsync(metrics);
        await db.ClinicalMeasurements.AddRangeAsync(rows);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetPaged_CursorNull_PrimeraPaginaOrdenDesc()
    {
        // Arrange: 3 filas del paciente, fechas distintas.
        using var db = CreateDb();
        var patientId = Guid.NewGuid();
        var unit = BuildUnit("kg", "kg");
        var metric = BuildMetric("weight", "Peso", unit);
        var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var rows = new[]
        {
            BuildMeasurement(patientId, metric, unit, t0.AddDays(-2), value: 70m),
            BuildMeasurement(patientId, metric, unit, t0.AddDays(-1), value: 71m),
            BuildMeasurement(patientId, metric, unit, t0, value: 72m),
        };
        await SeedAsync(db, patientId, rows);
        var repo = new PatientMeasurementRepository(db);

        // Act: primera página sin cursor.
        var page = await repo.GetPagedAsync(patientId, null, 2, null, CancellationToken.None);

        // Assert: las 2 más recientes DESC, hay siguiente.
        Assert.Equal(2, page.Items.Count);
        Assert.True(page.HasNextPage);
        Assert.NotNull(page.NextCursor);
        Assert.Equal(72m, page.Items[0].Value);
        Assert.Equal(71m, page.Items[1].Value);
    }

    [Fact]
    public async Task GetPaged_CursorValido_SegundaPaginaSinSolape()
    {
        // Arrange: 3 filas, primera página de 2.
        using var db = CreateDb();
        var patientId = Guid.NewGuid();
        var unit = BuildUnit("kg", "kg");
        var metric = BuildMetric("weight", "Peso", unit);
        var t0 = new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc);
        var rows = new[]
        {
            BuildMeasurement(patientId, metric, unit, t0.AddDays(-2), value: 70m),
            BuildMeasurement(patientId, metric, unit, t0.AddDays(-1), value: 71m),
            BuildMeasurement(patientId, metric, unit, t0, value: 72m),
        };
        await SeedAsync(db, patientId, rows);
        var repo = new PatientMeasurementRepository(db);
        var first = await repo.GetPagedAsync(patientId, null, 2, null, CancellationToken.None);

        // Act: segunda página con el NextCursor opaco de la primera.
        var second = await repo.GetPagedAsync(
            patientId,
            null,
            2,
            first.NextCursor,
            CancellationToken.None
        );

        // Assert: solo la restante, sin solape, última página.
        Assert.Single(second.Items);
        Assert.Equal(70m, second.Items[0].Value);
        Assert.False(second.HasNextPage);
        Assert.Null(second.NextCursor);
        Assert.DoesNotContain(
            second.Items,
            i => i.Id == first.Items[0].Id || i.Id == first.Items[1].Id
        );
    }

    [Fact]
    public async Task GetPaged_CursorInvalido_LanzaValidation()
    {
        // Arrange: repositorio vacío, cursor que no es Base64.
        using var db = CreateDb();
        var repo = new PatientMeasurementRepository(db);

        // Act + Assert: excepción controlada → 400 por el middleware.
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            repo.GetPagedAsync(Guid.NewGuid(), null, 20, "no-es-base64!!!", CancellationToken.None)
        );
        Assert.Equal("cursor", ex.Errors.First().PropertyName);
        Assert.Contains("CURSOR_INVALID", ex.Errors.First().ErrorMessage);
    }

    [Fact]
    public async Task GetPaged_MasFilasQuePageSize_HasNextPageTrue()
    {
        // Arrange: 3 filas, pageSize 2.
        using var db = CreateDb();
        var patientId = Guid.NewGuid();
        var unit = BuildUnit("kg", "kg");
        var metric = BuildMetric("weight", "Peso", unit);
        var t0 = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        await SeedAsync(
            db,
            patientId,
            [
                BuildMeasurement(patientId, metric, unit, t0.AddDays(-2)),
                BuildMeasurement(patientId, metric, unit, t0.AddDays(-1)),
                BuildMeasurement(patientId, metric, unit, t0),
            ]
        );
        var repo = new PatientMeasurementRepository(db);

        // Act.
        var page = await repo.GetPagedAsync(patientId, null, 2, null, CancellationToken.None);

        // Assert: peek pageSize+1 → hay siguiente, la extra no se devuelve.
        Assert.Equal(2, page.Items.Count);
        Assert.True(page.HasNextPage);
        Assert.NotNull(page.NextCursor);
    }

    [Fact]
    public async Task GetPaged_ExactamentePageSizeFilas_HasNextPageFalse()
    {
        // Arrange: exactamente 2 filas, pageSize 2.
        using var db = CreateDb();
        var patientId = Guid.NewGuid();
        var unit = BuildUnit("kg", "kg");
        var metric = BuildMetric("weight", "Peso", unit);
        var t0 = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        await SeedAsync(
            db,
            patientId,
            [
                BuildMeasurement(patientId, metric, unit, t0.AddDays(-1)),
                BuildMeasurement(patientId, metric, unit, t0),
            ]
        );
        var repo = new PatientMeasurementRepository(db);

        // Act.
        var page = await repo.GetPagedAsync(patientId, null, 2, null, CancellationToken.None);

        // Assert: sin fila extra → última página.
        Assert.Equal(2, page.Items.Count);
        Assert.False(page.HasNextPage);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task GetPaged_MismoObservedAt_DesempataPorIdDesc()
    {
        // Arrange: 3 filas con la MISMA fecha, ids conocidos.
        using var db = CreateDb();
        var patientId = Guid.NewGuid();
        var unit = BuildUnit("kg", "kg");
        var metric = BuildMetric("weight", "Peso", unit);
        var sameDate = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var id3 = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var rows = new[] { id1, id2, id3 }
            .Select(id =>
            {
                var m = BuildMeasurement(patientId, metric, unit, sameDate);
                m.Id = id;
                return m;
            })
            .ToList();
        await SeedAsync(db, patientId, rows);
        var repo = new PatientMeasurementRepository(db);

        // Act.
        var page = await repo.GetPagedAsync(patientId, null, 10, null, CancellationToken.None);

        // Assert: orden estable id DESC (el mayor primero).
        Assert.Equal(3, page.Items.Count);
        Assert.Equal(id3, page.Items[0].Id);
        Assert.Equal(id2, page.Items[1].Id);
        Assert.Equal(id1, page.Items[2].Id);
        Assert.False(page.HasNextPage);
    }
}
