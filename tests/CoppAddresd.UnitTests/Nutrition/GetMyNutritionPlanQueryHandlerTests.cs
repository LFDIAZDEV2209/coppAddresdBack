using CoppAddresd.Application.Features.Nutrition.Queries.GetMyNutritionPlan;
using CoppAddresd.Application.Features.Nutrition;
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
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 6, 2, 0, 0, TimeSpan.Zero);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Composition_LocalDate_Coexistence_AndFrozenJsonContract(bool published)
    {
        var patient = BuildPatient(); patient.Timezone = "America/Bogota";
        _patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);
        var repository = Substitute.For<INutritionPrescriptionRepository>();
        var today = new DateOnly(2026, 11, 5);
        var legacy = BuildPlan(patient.Id);
        _wellness.ListPlanAssignmentsByPatientAsync(patient.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { BuildAssignment(patient.Id, legacy.Id, AssignmentStatus.Active, new(2026, 11, 5)) });
        _wellness.GetPlanByIdAsync(legacy.Id, Arg.Any<CancellationToken>()).Returns(legacy);
        var prescription = new NutritionPrescription { PatientId = patient.Id, StartDate = today.AddDays(-29), EndDate = today,
            CalorieTarget = 1500, WaterMl = 2250, Revision = 7, Instructions = "Indicaciones",
            Meals = [new() { DayNumber = 30, MealType = "mer", ScheduledTime = new(16, 0),
                Items = [new() { FoodItemId = "avena", Grams = 50, FoodItem = new() { Id = "avena", Name = "Avena",
                    Carbs100 = 60, Protein100 = 10, Fat100 = 8, Fiber100 = 6, Calories100 = 999 } }] }] };
        repository.GetCurrentPublishedAsync(patient.Id, today, Arg.Any<CancellationToken>())
            .Returns(published ? prescription : null);
        using var source = new CancellationTokenSource();
        var query = new GetMyNutritionPlanQueryHandler(_patients, _wellness,
            Substitute.For<ILogger<GetMyNutritionPlanQueryHandler>>(), repository, new FixedClock());
        var result = (await query.Handle(new(Guid.NewGuid()), source.Token))!;
        Assert.Equal(published ? prescription.Id : legacy.Id, result.Id);
        Assert.Equal("Active", result.Status);
        await repository.Received(1).GetCurrentPublishedAsync(patient.Id, today, source.Token);
        if (published)
        {
            Assert.Equal(30, result.CurrentDay); Assert.Equal(7, result.Revision);
            var meal = Assert.Single(Assert.Single(result.Days).Meals);
            Assert.Equal("Snack", meal.MealType); Assert.Equal("Avena: 50 g", meal.Foods);
            Assert.Equal("16:00", meal.SuggestedTime);
            Assert.Equal(176, meal.Calories); Assert.Equal(30m, meal.CarbsG); Assert.Equal(5m, meal.ProteinG);
            Assert.Equal(4m, meal.FatG); Assert.Equal(3m, meal.FiberG); Assert.Equal("Indicaciones", meal.Notes);
            await _wellness.DidNotReceive().ListPlanAssignmentsByPatientAsync(patient.Id, Arg.Any<CancellationToken>());
        }
        var json = System.Text.Json.JsonSerializer.SerializeToElement(result,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal("id,name,targetCondition,durationDays,dailyCalorieTarget,dailyProteinTarget,dailyCarbsTarget,dailyFatTarget,dailyFiberTarget,dailyWaterMl,allergens,mealTiming,status,days,startDate,endDate,currentDay,revision,disclaimer",
            string.Join(',', json.EnumerateObject().Select(x => x.Name)));
        Assert.Equal("dayNumber,dailyWaterMl,meals", string.Join(',', json.GetProperty("days")[0].EnumerateObject().Select(x => x.Name)));
        Assert.Equal("mealType,description,foods,calories,proteinG,carbsG,fatG,fiberG,waterMl,notes,sortOrder,suggestedTime,notesEn",
            string.Join(',', json.GetProperty("days")[0].GetProperty("meals")[0].EnumerateObject().Select(x => x.Name)));
        Assert.Equal(published ? "16:00" : null,
            json.GetProperty("days")[0].GetProperty("meals")[0].GetProperty("suggestedTime").GetString());
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query.Handle(new(Guid.NewGuid()), source.Token));
    }

    [Theory]
    [InlineData(null, "Review portions", "Revisar porciones", "Review portions")]
    [InlineData("Indicación específica del almuerzo", "Review portions", "Revisar porciones", null)]
    [InlineData("", "Review portions", "Revisar porciones", null)]
    [InlineData(null, null, "Revisar porciones", null)]
    [InlineData(null, "Review older portions", null, null)]
    [InlineData(null, "Review older portions", "Indicaciones anteriores", null)]
    public async Task PublishedMenu_ProjectsMatchingBilingualInstructions_WithoutOverridingMealNotes(
        string? mealNote, string? instructionsEn, string? verifiedSource, string? expectedNotesEn)
    {
        var patient = BuildPatient(); patient.Timezone = "America/Bogota";
        _patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);
        var repository = Substitute.For<INutritionPrescriptionRepository>();
        var today = new DateOnly(2026, 11, 5);
        var prescription = new NutritionPrescription
        {
            PatientId = patient.Id, StartDate = today, EndDate = today.AddDays(29), IsPublished = true,
            Instructions = "Revisar porciones", InstructionsEn = instructionsEn,
            InstructionsEnSourceHash = NutritionInstructionTranslation.SourceHash(verifiedSource),
            Meals = [new() { DayNumber = 1, MealType = "alm", ScheduledTime = new(12, 0), Notes = mealNote }],
        };
        repository.GetCurrentPublishedAsync(patient.Id, today, Arg.Any<CancellationToken>()).Returns(prescription);
        var handler = new GetMyNutritionPlanQueryHandler(_patients, _wellness,
            Substitute.For<ILogger<GetMyNutritionPlanQueryHandler>>(), repository, new FixedClock());

        var result = (await handler.Handle(new(Guid.NewGuid()), CancellationToken.None))!;
        var meal = Assert.Single(Assert.Single(result.Days).Meals);
        Assert.Equal(mealNote ?? "Revisar porciones", meal.Notes);
        Assert.Equal(expectedNotesEn, meal.NotesEn);
        Assert.Equal(mealNote, Assert.Single(prescription.Meals).Notes);
        Assert.Equal("Revisar porciones", prescription.Instructions);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(result,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal(expectedNotesEn, json.GetProperty("days")[0].GetProperty("meals")[0].GetProperty("notesEn").GetString());
    }

    [Theory]
    [InlineData(false, "Tomar 250 ml de agua.", "Drink 250 ml of water.")]
    [InlineData(true, "Tomar 250 ml de agua.", "Drink 250 ml of water.")]
    [InlineData(false, null, null)]
    public async Task PublishedMenu_DoesNotPresentInternalPendingReviewCopy_AsPatientInstructions(
        bool specificNote, string? clinicalEs, string? clinicalEn)
    {
        var patient = BuildPatient(); patient.Timezone = "America/Bogota";
        _patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);
        var repository = Substitute.For<INutritionPrescriptionRepository>();
        const string internalEs = "Propuesta determinista pendiente de revisión.\nRevisar porciones y alertas antes de aprobar.";
        const string internalEn = "Deterministic proposal pending review.\nReview portions and alerts before approval.";
        var sourceEs = internalEs + (clinicalEs is null ? "" : "\n" + clinicalEs);
        var sourceEn = internalEn + (clinicalEn is null ? "" : "\n" + clinicalEn);
        var plan = new NutritionPrescription { PatientId = patient.Id, IsPublished = true, State = "aprobado",
            StartDate = new(2026, 11, 5), EndDate = new(2026, 12, 4),
            Instructions = sourceEs, InstructionsEn = sourceEn,
            InstructionsEnSourceHash = NutritionInstructionTranslation.SourceHash(sourceEs),
            Meals = [new() { DayNumber = 1, MealType = "alm", ScheduledTime = new(12, 0),
                MenuName = "Pollo con ensalada", WaterMl = 250, Notes = specificNote ? "Paciente comenta: Propuesta determinista pendiente de revisión. Tomar 1 taza." : null }] };
        repository.GetCurrentPublishedAsync(patient.Id, new DateOnly(2026, 11, 5), Arg.Any<CancellationToken>()).Returns(plan);
        var handler = new GetMyNutritionPlanQueryHandler(_patients, _wellness,
            Substitute.For<ILogger<GetMyNutritionPlanQueryHandler>>(), repository, new FixedClock());
        var result = (await handler.Handle(new(Guid.NewGuid()), CancellationToken.None))!;
        var meal = Assert.Single(Assert.Single(result.Days).Meals);
        Assert.Equal(specificNote ? plan.Meals.Single().Notes : clinicalEs, meal.Notes);
        Assert.Equal(specificNote ? null : clinicalEn, meal.NotesEn);
        Assert.Equal("Pollo con ensalada", meal.Description);
        Assert.Equal(250, meal.WaterMl);
        Assert.Equal(sourceEs, plan.Instructions); Assert.Equal(sourceEn, plan.InstructionsEn);
        Assert.Equal(NutritionInstructionTranslation.SourceHash(sourceEs), plan.InstructionsEnSourceHash);
        Assert.True(plan.IsPublished); Assert.Equal("aprobado", plan.State);
    }

    [Fact]
    public async Task LegacyPublishedMenu_RemovesOnlyInternalReviewLines_AndPreservesClinicalTextAndSource()
    {
        var patient = BuildPatient();
        var plan = BuildPlan(patient.Id);
        const string source = "Propuesta determinista: revisa porciones y alertas antes de aprobar.\nMantener 50 g de arroz; no añadir azúcar.";
        plan.Days.First().Notes = source;
        _patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);
        _wellness.ListPlanAssignmentsByPatientAsync(patient.Id, Arg.Any<CancellationToken>())
            .Returns([BuildAssignment(patient.Id, plan.Id, AssignmentStatus.Active)]);
        _wellness.GetPlanByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        var result = (await BuildHandler().Handle(new(Guid.NewGuid()), CancellationToken.None))!;
        var dinner = result.Days.Single(day => day.DayNumber == 2).Meals.Single();
        Assert.Equal("Mantener 50 g de arroz; no añadir azúcar.", dinner.Notes);
        Assert.Equal(400, dinner.Calories); Assert.Equal("Cena ligera", dinner.Description);
        Assert.Equal(source, plan.Days.First().Notes);
    }

    [Fact]
    public async Task PublishedMenu_PreservesEachMealTimeIncludingSnack_InScheduledOrder()
    {
        var patient = BuildPatient(); patient.Timezone = "America/Bogota";
        _patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);
        var repository = Substitute.For<INutritionPrescriptionRepository>();
        var today = new DateOnly(2026, 11, 5);
        var prescription = new NutritionPrescription
        {
            PatientId = patient.Id, StartDate = today, EndDate = today.AddDays(29),
            Meals = [
                new() { DayNumber = 1, MealType = "cen", ScheduledTime = new(19, 0) },
                new() { DayNumber = 1, MealType = "mer", ScheduledTime = new(15, 30), MenuName = "Merienda prescrita" },
                new() { DayNumber = 1, MealType = "des", ScheduledTime = new(7, 0) },
                new() { DayNumber = 1, MealType = "alm", ScheduledTime = new(12, 0) },
            ],
        };
        repository.GetCurrentPublishedAsync(patient.Id, today, Arg.Any<CancellationToken>()).Returns(prescription);
        var handler = new GetMyNutritionPlanQueryHandler(_patients, _wellness,
            Substitute.For<ILogger<GetMyNutritionPlanQueryHandler>>(), repository, new FixedClock());
        var result = (await handler.Handle(new(Guid.NewGuid()), CancellationToken.None))!;
        var meals = Assert.Single(result.Days).Meals;
        Assert.Equal(["Desayuno", "Almuerzo", "Snack", "Cena"], meals.Select(m => m.MealType));
        Assert.Equal(["07:00", "12:00", "15:30", "19:00"], meals.Select(m => m.SuggestedTime));
        Assert.Equal("Merienda prescrita", meals[2].Description);
    }

    private readonly IPatientRepository _patients = Substitute.For<IPatientRepository>();
    private readonly IWellnessRepository _wellness = Substitute.For<IWellnessRepository>();

    private GetMyNutritionPlanQueryHandler BuildHandler() =>
        new(_patients, _wellness, Substitute.For<ILogger<GetMyNutritionPlanQueryHandler>>(),
            Substitute.For<INutritionPrescriptionRepository>(), TimeProvider.System);

    private static PatientProfile BuildPatient(Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            FirstName = "Ana",
            LastName = "Ruiz",
            Status = "Activo",
        };

    private static NutritionPlan BuildPlan(Guid patientId) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Plan control glucémico",
            PatientId = patientId, ApprovedAt = DateTime.UtcNow, ApprovedBy = Guid.NewGuid(), ReviewStatus = "approved",
            TargetCondition = "Diabetes",
            DurationDays = 30,
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
    public async Task Handle_SameLocalStartDate_GreatestAssignmentIdWinsRegardlessOfInputOrder()
    {
        var patient = BuildPatient();
        var first = BuildPlan(patient.Id); var second = BuildPlan(patient.Id);
        _patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);
        _wellness.GetPatientTimezoneAsync(patient.Id, Arg.Any<CancellationToken>()).Returns("America/Bogota");
        var start = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("America/Bogota")).Date;
        var low = BuildAssignment(patient.Id, first.Id, AssignmentStatus.Active, start);
        low.Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var high = BuildAssignment(patient.Id, second.Id, AssignmentStatus.Active, start);
        high.Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        _wellness.GetPlanByIdAsync(first.Id, Arg.Any<CancellationToken>()).Returns(first);
        _wellness.GetPlanByIdAsync(second.Id, Arg.Any<CancellationToken>()).Returns(second);
        foreach (var assignments in new[] { new[] { low, high }, new[] { high, low } })
        {
            _wellness.ListPlanAssignmentsByPatientAsync(patient.Id, Arg.Any<CancellationToken>()).Returns(assignments);
            var result = await BuildHandler().Handle(new(Guid.NewGuid()), CancellationToken.None);
            Assert.Equal(second.Id, result!.Id);
        }
    }

    [Fact]
    public async Task Handle_PlanActivo_RetornaDtoConDiasYComidasOrdenados()
    {
        // Arrange: paciente con asignación activa vigente.
        var patient = BuildPatient();
        var plan = BuildPlan(patient.Id);
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
        Assert.Equal(plan.Revision, result.Revision);
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
        var oldPlan = BuildPlan(patient.Id);
        var newPlan = BuildPlan(patient.Id);
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
