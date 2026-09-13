using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.RecordWeight;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using Moq;

namespace CoppAddresd.UnitTests.ProgramProgress;

public class RecordWeightTests
{
    private readonly Guid patient = Guid.NewGuid(), enrollment = Guid.NewGuid(), actor = Guid.NewGuid();
    private readonly Mock<IProgramRepository> program = new();
    private readonly Mock<IClinicalMeasurementRepository> measurements = new();
    private readonly Mock<ICacheService> cache = new();

    private RecordWeightCommand Request(decimal weight = 80) => new(patient, enrollment, actor, weight, DateOnly.FromDateTime(DateTime.UtcNow));
    private RecordWeightCommandHandler Handler(Guid? owner = null)
    {
        program.Setup(p => p.GetEnrollmentAsync(enrollment, It.IsAny<CancellationToken>())).ReturnsAsync(
            new ProgramEnrollmentDto(enrollment, owner ?? patient, Guid.NewGuid(), "UTC", ProgramEnrollmentStatus.Active,
                DateTime.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), 1, 83, 0, 0, 0, 0, null, null, null, DateTime.UtcNow));
        measurements.Setup(m => m.GetActiveMetricsWithUnitsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
            new MeasurementMetric { Id = Guid.NewGuid(), Code = "weight", DefaultUnitId = Guid.NewGuid(),
                DefaultUnit = new UnitOfMeasure { Code = "kg", IsActive = true } }
        ]);
        return new(program.Object, measurements.Object, cache.Object);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(501)] [InlineData(80.123)]
    public void RejectsInvalidWeight(decimal weight) => Assert.False(new RecordWeightCommandValidator().Validate(Request(weight)).IsValid);

    [Theory]
    [InlineData(1)] [InlineData(80.25)] [InlineData(500)]
    public void AcceptsExistingWeightRange(decimal weight) => Assert.True(new RecordWeightCommandValidator().Validate(Request(weight)).IsValid);

    [Fact]
    public void RejectsMissingDate() => Assert.False(new RecordWeightCommandValidator().Validate(Request() with { Date = default }).IsValid);

    [Fact]
    public async Task AppendsIndependentMeasurementsAndInvalidatesOnlyPatientHistory()
    {
        var rows = new List<ClinicalMeasurement>();
        measurements.Setup(m => m.AddBatchAsync(It.IsAny<IReadOnlyList<ClinicalMeasurement>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<ClinicalMeasurement>, CancellationToken>((batch, _) => rows.AddRange(batch)).Returns(Task.CompletedTask);
        var handler = Handler();
        await handler.Handle(Request(86), default);
        await handler.Handle(Request(80), default);
        Assert.Equal(2, rows.Count);
        Assert.NotEqual(rows[0].Id, rows[1].Id);
        Assert.Equal(86, rows[0].Value);
        Assert.Equal(80, rows[1].Value);
        Assert.All(rows, row => { Assert.Equal(patient, row.PatientId); Assert.Equal(actor, row.CreatedBy); Assert.Equal("patient", row.Source); });
        cache.Verify(c => c.RemoveAsync(CacheKeys.MetricsHistory(patient), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RejectsAnotherPatientsEnrollmentWithoutWriting()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handler(Guid.NewGuid()).Handle(Request(), default));
        measurements.Verify(m => m.AddBatchAsync(It.IsAny<IReadOnlyList<ClinicalMeasurement>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RejectsFutureDateWithoutWriting()
    {
        await Assert.ThrowsAsync<UnprocessableEntityException>(() => Handler().Handle(Request() with { Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2) }, default));
        measurements.Verify(m => m.AddBatchAsync(It.IsAny<IReadOnlyList<ClinicalMeasurement>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FailedPersistenceDoesNotInvalidateOrReturnSuccess()
    {
        measurements.Setup(m => m.AddBatchAsync(It.IsAny<IReadOnlyList<ClinicalMeasurement>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("test storage unavailable"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(Request(), default));
        cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EndpointUsesAuthenticatedIdentityEvenWithInjectedJsonIds()
    {
        var context = new Mock<CoppAddresd.Api.Context.IProgramActorContext>();
        context.SetupGet(c => c.UserId).Returns(actor);
        context.Setup(c => c.ResolvePatientProfileIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        context.Setup(c => c.ResolveActiveEnrollmentIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(enrollment);
        var mediator = new Mock<MediatR.IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<RecordWeightCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecordedWeightDto(Guid.NewGuid(), 80, Request().Date, DateTime.UtcNow));
        var controller = new CoppAddresd.Api.Controllers.ProgramController(mediator.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CoppAddresd.Api.Controllers.ProgramController>.Instance,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), context.Object,
            Mock.Of<IObjectStorageService>());
        var payload = System.Text.Json.JsonSerializer.Deserialize<RecordWeightRequest>(
            "{\"weightKg\":80,\"date\":\"2026-09-11\",\"patientId\":\"" + Guid.NewGuid() + "\",\"userId\":\"" + Guid.NewGuid() + "\"}",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        await controller.RecordWeight(payload, default);
        mediator.Verify(m => m.Send(It.Is<RecordWeightCommand>(r => r.PatientId == patient && r.ActorId == actor && r.EnrollmentId == enrollment), It.IsAny<CancellationToken>()), Times.Once);
    }
}
