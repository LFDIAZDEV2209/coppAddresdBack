using CoppAddresd.Application.Features.Nutrition.Queries.GetMyNutritionPlan;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CoppAddresd.UnitTests.Nutrition;

/// <summary>
/// Pruebas del query self-service <c>GetMyNutritionPlan</c> (Fase 8, móvil):
/// resolución del paciente por JWT, selección de la asignación activa del ERP
/// (overlap → gana el StartDate más reciente) y proyección del plan con días
/// y comidas ordenados. Los repositorios se mockean (sin EF en unit).
/// </summary>
public sealed class GetMyNutritionPlanQueryHandlerTests
{
    private readonly IPatientRepository _patients = Substitute.For<IPatientRepository>();
    private readonly IWellnessRepository _wellness = Substitute.For<IWellnessRepository>();

    private GetMyNutritionPlanQueryHandler BuildHandler() =>
        new(_patients, _wellness, Substitute.For<ILogger<GetMyNutritionPlanQueryHandler>>());

    private static PatientProfile BuildPatient(Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            FirstName = "Ana",
            LastName = "Ruiz",
            Status = "Activo",
        };

    private static NutritionPlan BuildPlan() =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Plan control glucémico",
            TargetCondition = "Diabetes",
            DurationDays = 7,
            DailyCalorieTarget = 1800,
            DailyProteinTarget = 90m,
            DailyCarbsTarget = 200m,
            DailyFatTarget = 60m,
            DailyFiberTarget = 25m,
            Allergens = "Frutos secos",
            MealTiming = "7:00, 12:00, 19:00",
            Status = NutritionPlanStatus.Active,
            Days =
            [
                new NutritionPlanDay
                {
                    Id = Guid.NewGuid(),
                    DayNumber = 2,
                    DailyWaterMl = 2000,
                    MealType = MealType.Cena,
                    Description = "Cena ligera",
                    Calories = 400,
                    SortOrder = 1,
                },
                new NutritionPlanDay
                {
                    Id = Guid.NewGuid(),
                    DayNumber = 1,
                    DailyWaterMl = 2500,
                    MealType = MealType.Almuerzo,
                    Description = "Almuerzo",
                    Calories = 600,
                    SortOrder = 2,
                },
                new NutritionPlanDay
                {
                    Id = Guid.NewGuid(),
                    DayNumber = 1,
                    DailyWaterMl = 2500,
                    MealType = MealType.Desayuno,
                    Description = "Desayuno",
                    Calories = 350,
                    SortOrder = 1,
                },
            ],
        };

    private static NutritionPlanAssignment BuildAssignment(
        Guid patientId,
        Guid planId,
        AssignmentStatus status,
        DateTime? start = null,
        DateTime? end = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            PlanId = planId,
            StartDate = start ?? DateTime.UtcNow.AddDays(-1),
            EndDate = end,
            Status = status,
        };

    [Fact]
    public async Task Handle_PlanActivo_RetornaDtoConDiasYComidasOrdenados()
    {
        // Arrange: paciente con asignación activa vigente.
        var patient = BuildPatient();
        var plan = BuildPlan();
        _patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);
        _wellness
            .ListPlanAssignmentsByPatientAsync(patient.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { BuildAssignment(patient.Id, plan.Id, AssignmentStatus.Active) });
        _wellness.GetPlanByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);

        // Act.
        var result = await BuildHandler()
            .Handle(new GetMyNutritionPlanQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert: contrato reducido completo y orden DayNumber/SortOrder.
        Assert.NotNull(result);
        Assert.Equal(plan.Id, result.Id);
        Assert.Equal("Plan control glucémico", result.Name);
        Assert.Equal("Active", result.Status);
        Assert.Equal(2500, result.DailyWaterMl);
        Assert.Equal(2, result.Days.Count);
        Assert.Equal(1, result.Days[0].DayNumber);
        Assert.Equal(2, result.Days[1].DayNumber);
        Assert.Equal(
            ["Desayuno", "Almuerzo"],
            result.Days[0].Meals.Select(m => m.MealType).ToList()
        );
        Assert.Equal("Cena", result.Days[1].Meals[0].MealType);
        Assert.Equal(1800, result.DailyCalorieTarget);
    }

    [Fact]
    public async Task Handle_Overlap_GanaStartDateMasReciente()
    {
        // Arrange: dos asignaciones activas solapadas.
        var patient = BuildPatient();
        var oldPlan = BuildPlan();
        var newPlan = BuildPlan();
        _patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);
        _wellness
            .ListPlanAssignmentsByPatientAsync(patient.Id, Arg.Any<CancellationToken>())
            .Returns(
                new[]
                {
                    BuildAssignment(
                        patient.Id,
                        oldPlan.Id,
                        AssignmentStatus.Active,
                        start: DateTime.UtcNow.AddDays(-30)
                    ),
                    BuildAssignment(
                        patient.Id,
                        newPlan.Id,
                        AssignmentStatus.Active,
                        start: DateTime.UtcNow.AddDays(-1)
                    ),
                }
            );
        _wellness
            .GetPlanByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
                Task.FromResult<NutritionPlan?>(
                    callInfo.Arg<Guid>() == newPlan.Id ? newPlan : oldPlan
                )
            );

        // Act.
        var result = await BuildHandler()
            .Handle(new GetMyNutritionPlanQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert: se sirve el plan de la asignación más reciente.
        Assert.NotNull(result);
        Assert.Equal(newPlan.Id, result.Id);
        await _wellness.Received(1).GetPlanByIdAsync(newPlan.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SinAsignacionActiva_RetornaNull()
    {
        // Arrange: solo asignación pausada (el controlador la vuelve 404).
        var patient = BuildPatient();
        _patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);
        _wellness
            .ListPlanAssignmentsByPatientAsync(patient.Id, Arg.Any<CancellationToken>())
            .Returns(
                new[] { BuildAssignment(patient.Id, Guid.NewGuid(), AssignmentStatus.Paused) }
            );

        // Act.
        var result = await BuildHandler()
            .Handle(new GetMyNutritionPlanQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert: null sin tocar el plan.
        Assert.Null(result);
        await _wellness
            .DidNotReceiveWithAnyArgs()
            .GetPlanByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AsignacionVencida_RetornaNull()
    {
        // Arrange: asignación activa pero con ventana ya cerrada.
        var patient = BuildPatient();
        _patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);
        _wellness
            .ListPlanAssignmentsByPatientAsync(patient.Id, Arg.Any<CancellationToken>())
            .Returns(
                new[]
                {
                    BuildAssignment(
                        patient.Id,
                        Guid.NewGuid(),
                        AssignmentStatus.Active,
                        start: DateTime.UtcNow.AddDays(-30),
                        end: DateTime.UtcNow.AddDays(-1)
                    ),
                }
            );

        // Act.
        var result = await BuildHandler()
            .Handle(new GetMyNutritionPlanQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_UsuarioSinPerfil_LanzaNotFound()
    {
        // Arrange: el JWT no tiene perfil vinculado en patient_profiles.
        _patients
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PatientProfile?)null);

        // Act + Assert: 404 sin revelar otros perfiles.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            BuildHandler()
                .Handle(new GetMyNutritionPlanQuery(Guid.NewGuid()), CancellationToken.None)
        );

        await _wellness
            .DidNotReceiveWithAnyArgs()
            .ListPlanAssignmentsByPatientAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PlanHuerfano_RetornaNull()
    {
        // Arrange: asignación activa cuyo plan ya no existe.
        var patient = BuildPatient();
        _patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);
        _wellness
            .ListPlanAssignmentsByPatientAsync(patient.Id, Arg.Any<CancellationToken>())
            .Returns(
                new[] { BuildAssignment(patient.Id, Guid.NewGuid(), AssignmentStatus.Active) }
            );
        _wellness
            .GetPlanByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((NutritionPlan?)null);

        // Act.
        var result = await BuildHandler()
            .Handle(new GetMyNutritionPlanQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}
