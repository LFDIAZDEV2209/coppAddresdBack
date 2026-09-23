using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.RecordDeviceMetrics;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using Moq;

namespace CoppAddresd.UnitTests.ProgramProgress;

public class RecordDeviceMetricsTests
{
    private readonly Guid patient = Guid.NewGuid(),
        enrollment = Guid.NewGuid(),
        actor = Guid.NewGuid();
    private readonly Mock<IProgramRepository> program = new();
    private readonly Mock<IClinicalMeasurementRepository> measurements = new();
    private readonly Mock<ICacheService> cache = new();

    private RecordDeviceMetricsCommand Request(
        decimal? steps = 6240,
        decimal? distance = 4120,
        decimal? kcal = 210,
        decimal? sleep = null
    ) => new(patient, enrollment, actor, steps, distance, kcal, sleep, DateTime.UtcNow);

    private RecordDeviceMetricsCommandHandler Handler(Guid? owner = null)
    {
        program
            .Setup(p => p.GetEnrollmentAsync(enrollment, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ProgramEnrollmentDto(
                    enrollment,
                    owner ?? patient,
                    Guid.NewGuid(),
                    "UTC",
                    ProgramEnrollmentStatus.Active,
                    DateTime.UtcNow,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    1,
                    83,
                    0,
                    0,
                    0,
                    0,
                    null,
                    null,
                    null,
                    DateTime.UtcNow
                )
            );
        measurements
            .Setup(m => m.GetActiveMetricsWithUnitsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MeasurementMetric
                {
                    Id = Guid.NewGuid(),
                    Code = "step_count",
                    DefaultUnitId = Guid.NewGuid(),
                    DefaultUnit = new UnitOfMeasure { Code = "count", IsActive = true },
                },
                new MeasurementMetric
                {
                    Id = Guid.NewGuid(),
                    Code = "distance_m",
                    DefaultUnitId = Guid.NewGuid(),
                    DefaultUnit = new UnitOfMeasure { Code = "meters", IsActive = true },
                },
                new MeasurementMetric
                {
                    Id = Guid.NewGuid(),
                    Code = "activity_kcal",
                    DefaultUnitId = Guid.NewGuid(),
                    DefaultUnit = new UnitOfMeasure { Code = "kcal", IsActive = true },
                },
                new MeasurementMetric
                {
                    Id = Guid.NewGuid(),
                    Code = "sleep_minutes",
                    DefaultUnitId = Guid.NewGuid(),
                    DefaultUnit = new UnitOfMeasure { Code = "minutes", IsActive = true },
                },
            ]);
        return new(program.Object, measurements.Object, cache.Object);
    }

    [Fact]
    public void RejectsPayloadWithoutAnyMetric() =>
        Assert.False(
            new RecordDeviceMetricsCommandValidator()
                .Validate(Request(steps: null, distance: null, kcal: null, sleep: null))
                .IsValid
        );

    [Theory]
    [InlineData(0)]
    [InlineData(200001)]
    public void RejectsStepsOutOfRange(decimal steps) =>
        Assert.False(
            new RecordDeviceMetricsCommandValidator().Validate(Request(steps: steps)).IsValid
        );

    [Theory]
    [InlineData(1)]
    [InlineData(6240)]
    [InlineData(200000)]
    public void AcceptsStepsInRange(decimal steps) =>
        Assert.True(
            new RecordDeviceMetricsCommandValidator().Validate(Request(steps: steps)).IsValid
        );

    [Fact]
    public void RejectsSleepLongerThanADay() =>
        Assert.False(
            new RecordDeviceMetricsCommandValidator()
                .Validate(Request(steps: null, distance: null, kcal: null, sleep: 1441))
                .IsValid
        );

    [Fact]
    public async Task UpsertsOneRowPerProvidedMetricAndInvalidatesHistory()
    {
        List<DailyDeviceMetric>? rows = null;
        measurements
            .Setup(m =>
                m.UpsertDailyDeviceMetricsAsync(
                    patient,
                    It.IsAny<IReadOnlyList<DailyDeviceMetric>>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    actor,
                    "device",
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                Guid,
                IReadOnlyList<DailyDeviceMetric>,
                DateTime,
                DateTime,
                DateTime,
                Guid,
                string,
                CancellationToken
            >((_, batch, _, _, _, _, _, _) => rows = batch.ToList())
            .Returns(Task.CompletedTask);

        var result = await Handler().Handle(Request(sleep: 435), default);

        Assert.NotNull(rows);
        Assert.Equal(
            ["step_count", "distance_m", "activity_kcal", "sleep_minutes"],
            rows!.Select(r => r.Code)
        );
        Assert.Equal(435, rows.Single(r => r.Code == "sleep_minutes").Value);
        Assert.Equal(4, result.Codes.Count);
        cache.Verify(
            c => c.RemoveAsync(CacheKeys.MetricsHistory(patient), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task OnlyUpsertsProvidedMetrics()
    {
        List<DailyDeviceMetric>? rows = null;
        measurements
            .Setup(m =>
                m.UpsertDailyDeviceMetricsAsync(
                    patient,
                    It.IsAny<IReadOnlyList<DailyDeviceMetric>>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    actor,
                    "device",
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                Guid,
                IReadOnlyList<DailyDeviceMetric>,
                DateTime,
                DateTime,
                DateTime,
                Guid,
                string,
                CancellationToken
            >((_, batch, _, _, _, _, _, _) => rows = batch.ToList())
            .Returns(Task.CompletedTask);

        await Handler().Handle(Request(distance: null, kcal: null), default);

        Assert.Equal(["step_count"], rows!.Select(r => r.Code));
    }

    [Fact]
    public async Task RejectsAnotherPatientsEnrollmentWithoutWriting()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(Guid.NewGuid()).Handle(Request(), default)
        );
        measurements.Verify(
            m =>
                m.UpsertDailyDeviceMetricsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyList<DailyDeviceMetric>>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        cache.Verify(
            c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task FailsClosedWhenCatalogCodeIsMissing()
    {
        var handler = Handler();
        measurements
            .Setup(m => m.GetActiveMetricsWithUnitsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MeasurementMetric
                {
                    Id = Guid.NewGuid(),
                    Code = "step_count",
                    DefaultUnitId = Guid.NewGuid(),
                    DefaultUnit = new UnitOfMeasure { Code = "count", IsActive = true },
                },
            ]);

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            handler.Handle(Request(steps: null, distance: null, kcal: null, sleep: 435), default)
        );
    }
}
