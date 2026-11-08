using System.Text.Json;
using CoppAddresd.Application.DTOs.LabExam;
using CoppAddresd.Application.Features.Agents;
using CoppAddresd.Application.Features.Measurements.Queries.GetMyMeasurements;
using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CoppAddresd.UnitTests.Features.Agents;

public sealed class GetPatientAiContextQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
    private readonly IPatientRepository patients = Substitute.For<IPatientRepository>();
    private readonly IClinicalMeasurementRepository measurements = Substitute.For<IClinicalMeasurementRepository>();
    private readonly IPatientMeasurementRepository devices = Substitute.For<IPatientMeasurementRepository>();
    private readonly IWellnessRepository wellness = Substitute.For<IWellnessRepository>();
    private readonly INutritionPrescriptionRepository prescriptions = Substitute.For<INutritionPrescriptionRepository>();
    private readonly TimeProvider clock = Substitute.For<TimeProvider>();
    private readonly PatientProfile patient = new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = "Activo", Timezone = "UTC" };

    public GetPatientAiContextQueryHandlerTests()
    {
        clock.GetUtcNow().Returns(new DateTimeOffset(Now));
        patients.GetByUserIdAsync(patient.UserId!.Value, Arg.Any<CancellationToken>()).Returns(patient);
        measurements.GetLastPerMetricAsync(patient.Id, Arg.Any<IEnumerable<string>>(), Guid.Empty, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, LabExamMetricSnapshot>());
        devices.GetDeviceWindowAsync(patient.Id, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MeasurementItemDto>());
        wellness.ListPlanAssignmentsByPatientAsync(patient.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<NutritionPlanAssignment>());
    }

    private GetPatientAiContextQueryHandler Handler() => new(patients, measurements, devices, wellness,
        prescriptions, clock, NullLogger<GetPatientAiContextQueryHandler>.Instance);
    private Task<PatientAiContextDto?> Read(CancellationToken ct = default) => Handler().Handle(new(patient.UserId!.Value), ct);

    private NutritionPlan AddPlan()
    {
        var plan = new NutritionPlan { Id = Guid.NewGuid(), PatientId = patient.Id, Name = "Plan vigente",
            Status = NutritionPlanStatus.Active, ReviewStatus = "approved", ApprovedAt = Now, ApprovedBy = Guid.NewGuid(),
            DailyCalorieTarget = 1800, DailyProteinTarget = 90, DailyCarbsTarget = 200, DailyFiberTarget = 30,
            Days = [new() { DayNumber = 1, MealType = MealType.Desayuno, Description = "Avena", Calories = 350 }] };
        wellness.ListPlanAssignmentsByPatientAsync(patient.Id, Arg.Any<CancellationToken>()).Returns(new[] {
            new NutritionPlanAssignment { PlanId = plan.Id, PatientId = patient.Id, StartDate = Now.Date, Status = AssignmentStatus.Active } });
        wellness.GetPlanByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        return plan;
    }

    [Fact]
    public async Task Handle_ConDatos_ComponeContextoCompactoYPropagaToken()
    {
        AddPlan();
        using var cts = new CancellationTokenSource();
        var metric = new LabExamMetricSnapshot("weight", 82, "kg", Now.AddDays(-2));
        measurements.GetLastPerMetricAsync(patient.Id, Arg.Any<IEnumerable<string>>(), Guid.Empty, cts.Token)
            .Returns(new Dictionary<string, LabExamMetricSnapshot> { ["weight"] = metric });
        patients.GetPrimaryDiagnosisAsync(patient.Id, cts.Token).Returns(new DiagnosisDto(Guid.NewGuid(), Guid.NewGuid(), "E11", "Diabetes", true));
        devices.GetDeviceWindowAsync(patient.Id, Now.AddDays(-7), Now, cts.Token).Returns(new[] {
            new MeasurementItemDto(Guid.NewGuid(), "step_count", "Pasos", 1000, "count", "u", new(Now.AddHours(-1)), "device"),
            new MeasurementItemDto(Guid.NewGuid(), "step_count", "Pasos", 2000, "count", "u", new(Now), "device") });

        var result = Assert.IsType<PatientAiContextDto>(await Read(cts.Token));
        Assert.Equal(patient.Id, result.PatientId);
        Assert.Equal(metric, Assert.Single(result.Measurements));
        Assert.Equal("E11", result.PrimaryDiagnosis!.Code);
        Assert.Equal(2000, Assert.Single(result.DeviceMetrics).Value);
        Assert.Equal(1800, result.NutritionPlan!.Kcal);
        Assert.Equal(90, result.NutritionPlan.ProteinG);
        Assert.Equal(200, result.NutritionPlan.CarbsG);
        Assert.Equal(30, result.NutritionPlan.FiberG);
        Assert.Equal("Avena", Assert.Single(result.NutritionPlan.Meals).Description);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Length < 8000);
        await patients.Received(1).GetByUserIdAsync(patient.UserId!.Value, cts.Token);
        await prescriptions.Received(1).GetCurrentPublishedAsync(patient.Id, DateOnly.FromDateTime(Now), cts.Token);
    }

    [Fact]
    public async Task Handle_SinPlan_DevuelveNull()
    {
        var result = Assert.IsType<PatientAiContextDto>(await Read());
        Assert.Null(result.NutritionPlan);
        Assert.Null(result.PrimaryDiagnosis);
    }

    [Fact]
    public async Task Handle_SinMedidas_ConservaPlanYListasVacias()
    {
        AddPlan();
        var result = Assert.IsType<PatientAiContextDto>(await Read());
        Assert.Empty(result.Measurements);
        Assert.Empty(result.DeviceMetrics);
        Assert.NotNull(result.NutritionPlan);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Inactivo")]
    [InlineData("Pendiente")]
    public async Task Handle_UsuarioSinPacienteActivo_DevuelveNullSinConsultarBloques(string? status)
    {
        patient.Status = status ?? "Activo";
        patients.GetByUserIdAsync(patient.UserId!.Value, Arg.Any<CancellationToken>()).Returns(status is null ? null : patient);
        Assert.Null(await Read());
        Assert.Empty(measurements.ReceivedCalls());
        Assert.Empty(devices.ReceivedCalls());
        Assert.Empty(wellness.ReceivedCalls());
    }

    [Fact]
    public async Task Handle_FalloDeUnBloque_ConservaLosDemas()
    {
        AddPlan();
        measurements.GetLastPerMetricAsync(patient.Id, Arg.Any<IEnumerable<string>>(), Guid.Empty, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException());
        var result = Assert.IsType<PatientAiContextDto>(await Read());
        Assert.Empty(result.Measurements);
        Assert.NotNull(result.NutritionPlan);
    }

    [Fact]
    public async Task Handle_Cancelado_RespetaCancelacion()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Read(cts.Token));
        Assert.Empty(patients.ReceivedCalls());
    }

    [Fact]
    public async Task Handle_TextosEscapadosYSeriesCompletas_RespetaPresupuestoUtf8()
    {
        var plan = AddPlan();
        plan.Name = new string('漢', 200);
        plan.Days = Enumerable.Range(0, 6).Select(i => new NutritionPlanDay {
            DayNumber = 1, MealType = MealType.Desayuno, SortOrder = i, Description = new string('漢', 1000)
        }).ToList();
        patients.GetPrimaryDiagnosisAsync(patient.Id, Arg.Any<CancellationToken>())
            .Returns(new DiagnosisDto(Guid.NewGuid(), Guid.NewGuid(), "E11", new string('漢', 1000), true));
        devices.GetDeviceWindowAsync(patient.Id, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(0, 7).SelectMany(day => new[] { "step_count", "distance_m", "activity_kcal", "sleep_minutes" }
                .Select(code => new MeasurementItemDto(Guid.NewGuid(), code, code, 12345, "count", "u", new(Now.AddDays(-day)), "device"))).ToArray());
        var result = Assert.IsType<PatientAiContextDto>(await Read());
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Length < 8000);
        Assert.Equal(1800, result.NutritionPlan!.Kcal);
        Assert.Equal("E11", result.PrimaryDiagnosis!.Code);
    }

    [Fact]
    public async Task Handle_BloquesOpcionalesFallan_DevuelveContextoVacio()
    {
        wellness.ListPlanAssignmentsByPatientAsync(patient.Id, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException());
        devices.GetDeviceWindowAsync(patient.Id, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException());
        patients.GetPrimaryDiagnosisAsync(patient.Id, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException());
        var result = Assert.IsType<PatientAiContextDto>(await Read());
        Assert.Null(result.NutritionPlan);
        Assert.Empty(result.DeviceMetrics);
        Assert.Null(result.PrimaryDiagnosis);
    }
}
