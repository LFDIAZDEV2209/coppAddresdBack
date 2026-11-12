using System.Text.Json;
using System.Text.Json.Nodes;
using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Nutrition;
using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Features.Nutrition.Engine;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.Localization;
using CoppAddresd.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class NutritionInstructionTranslationTests
{
    private const string Source = "Sin azúcar. Máximo 2 g de sal.";
    private const string English = "No sugar. Maximum 2 g of salt.";

    [Theory]
    [InlineData("Sin azúcar. Máximo 1 g de sal.", null, null)]
    [InlineData(Source, null, English)]
    [InlineData(null, null, English)]
    [InlineData("Sin azúcar. Máximo 1 g de sal.", "No sugar. Maximum 1 g of salt.", "No sugar. Maximum 1 g of salt.")]
    [InlineData(Source, "Avoid sugar. Maximum 2 g of salt.", "Avoid sugar. Maximum 2 g of salt.")]
    [InlineData(Source + " ", null, null)]
    public async Task Save_BindsEnglishInstructionsToExactSpanishSource(
        string? sourceEdit, string? englishEdit, string? expectedEnglish)
    {
        var foods = NutritionMealSolverTests.Foods();
        var engine = new NutritionEngine(foods);
        var patient = new NutritionPatient(68.5, 161, 38, "F", 26.4, 5.9, "MARCHA");
        var targets = NutritionEngine.ComputeTargets(patient);
        var plan = new NutritionPrescription
        {
            PatientId = Guid.NewGuid(), Revision = 4, Instructions = Source, InstructionsEn = English,
            InstructionsEnSourceHash = NutritionInstructionTranslation.SourceHash(Source),
            ClinicalProfileJson = JsonSerializer.Serialize(patient), TargetsJson = JsonSerializer.Serialize(targets),
            CalorieTarget = targets.K, CarbsTarget = targets.C, ProteinTarget = targets.P,
            FatTarget = targets.F, FiberTarget = targets.Fi, WaterMl = (int)(targets.Vasos * 250),
            Meals = engine.BuildDays(patient).SelectMany(day => day.Meals.Select(meal =>
                new NutritionPrescriptionMeal
                {
                    DayNumber = day.D, MealType = meal.Key, ScheduledTime = new TimeOnly(7, 0),
                    Items = meal.Value.Select(item => new NutritionPrescriptionItem
                    { FoodItemId = item.Id, Grams = item.G }).ToList(),
                })).ToList(),
        };
        var originalValues = NumericAndApprovalState(plan);
        var repository = Substitute.For<INutritionPrescriptionRepository>();
        repository.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        repository.ListFoodsAsync(Arg.Any<CancellationToken>()).Returns(foods.Values.ToArray());
        var store = Substitute.For<INutritionCaseStore>();
        store.CanAccessAsync(plan.PatientId, Arg.Any<NutritionActor>(), Arg.Any<CancellationToken>()).Returns(true);
        store.GetContextAsync(plan.PatientId, Arg.Any<CancellationToken>()).Returns(
            new NutritionCaseContext(new DateOnly(1988, 1, 1), "F", "America/Bogota", true, null));
        store.ExecuteAsync(plan.PatientId, Arg.Any<Func<CancellationToken, Task<NutritionView>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<NutritionView>>>()(call.Arg<CancellationToken>()));
        NutritionPrescriptionEvent? audit = null;
        store.AddEventAsync(Arg.Do<NutritionPrescriptionEvent>(entry => audit = entry), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var cases = new NutritionCases(repository, store, Substitute.For<INutritionDraftClient>(),
            Substitute.For<IClinicalContextService>(), Substitute.For<IProgramAssignmentRepository>(), TimeProvider.System);
        var actor = new NutritionActor(Guid.NewGuid(), null, true, true, true);

        var result = await cases.ExecuteAsync(new NutritionCaseCommand(NutritionOperation.Save,
            plan.PatientId, plan.Id, plan.Revision, actor,
            Edit: new NutritionEdit(Instructions: sourceEdit, InstructionsEn: englishEdit)), CancellationToken.None);

        Assert.Equal(sourceEdit ?? Source, result.Instructions);
        Assert.Equal(expectedEnglish, result.InstructionsEn);
        Assert.Equal(expectedEnglish, plan.InstructionsEn);
        var expectedHash = englishEdit is not null
            ? NutritionInstructionTranslation.PairedSourceHash(sourceEdit, englishEdit)
            : sourceEdit is not null && sourceEdit != Source ? null : NutritionInstructionTranslation.SourceHash(Source);
        Assert.Equal(expectedHash, plan.InstructionsEnSourceHash);
        Assert.Equal(expectedHash is not null ? expectedEnglish : null, NutritionInstructionTranslation.VerifiedEnglish(plan));
        Assert.Equal(originalValues, NumericAndApprovalState(plan));
        await repository.Received(1).UpdateAsync(plan, Arg.Any<CancellationToken>());
        using var snapshot = JsonDocument.Parse(Assert.IsType<NutritionPrescriptionEvent>(audit).SnapshotJson);
        Assert.Equal(expectedEnglish, snapshot.RootElement.GetProperty("instructionsEn").GetString());
        Assert.Equal(sourceEdit ?? Source, snapshot.RootElement.GetProperty("instructions").GetString());
        Assert.Equal(expectedHash, snapshot.RootElement.GetProperty("instructionsEnSourceHash").GetString());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(Source, null)]
    [InlineData(null, "Corrected English without an explicit source")]
    public async Task Save_DoesNotBlessHistoricalEnglish_WhenNoExplicitPairIsProvided(string? sourceEdit, string? englishEdit)
    {
        var fixture = Fixture();
        await fixture.Cases.ExecuteAsync(fixture.Command(NutritionOperation.Save) with
        { Edit = new NutritionEdit(Instructions: sourceEdit, InstructionsEn: englishEdit) }, CancellationToken.None);
        Assert.Null(fixture.Plan.InstructionsEnSourceHash);
        Assert.Null(NutritionInstructionTranslation.VerifiedEnglish(fixture.Plan));
        Assert.Equal(englishEdit ?? English, fixture.Plan.InstructionsEn);
        Assert.Equal(Source, fixture.Plan.Instructions);
    }

    [Fact]
    public async Task EnglishOnlyCorrection_RemainsForStaff_ButRequiresExplicitSourceBeforePatientReuse()
    {
        var fixture = Fixture();
        fixture.Plan.InstructionsEnSourceHash = NutritionInstructionTranslation.SourceHash(Source);
        await fixture.Cases.ExecuteAsync(fixture.Command(NutritionOperation.Save) with
        { Edit = new NutritionEdit(InstructionsEn: "Review the current source before reuse") }, CancellationToken.None);
        Assert.Null(fixture.Plan.InstructionsEnSourceHash);
        Assert.Null(NutritionInstructionTranslation.VerifiedEnglish(fixture.Plan));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Generation_StampsBothValidatedDraftAndDeterministicFallbackPairs(bool validDraft)
    {
        var fixture = Fixture(validDraft);
        NutritionPrescription? generated = null;
        fixture.Repository.When(repository => repository.AddAsync(Arg.Any<NutritionPrescription>(), Arg.Any<CancellationToken>()))
            .Do(call => generated = call.Arg<NutritionPrescription>());
        await fixture.Cases.ExecuteAsync(fixture.Command(NutritionOperation.Generate) with
        { Generate = new NutritionGenerateInput(Manual: new(68.5, 161, 38, "F", 26.4), BirthDate: new(1988, 1, 1)) }, CancellationToken.None);
        var plan = Assert.IsType<NutritionPrescription>(generated);
        Assert.Equal(NutritionInstructionTranslation.SourceHash(plan.Instructions), plan.InstructionsEnSourceHash);
        Assert.Equal(plan.InstructionsEn, NutritionInstructionTranslation.VerifiedEnglish(plan));
        Assert.Equal(validDraft ? "Narrativa nueva\nIndicaciones nuevas" : "Plan generado automáticamente con reglas del programa.", plan.Instructions);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CorrelatedDraftWorker_KeepsMeasuredProfileAndReservationBehavior_AndPersistsSourceProvenance(bool validDraft)
    {
        var fixture = Fixture(validDraft);
        var observed = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        fixture.Clinical.ConsolidateAsync(fixture.Plan.PatientId, Arg.Any<CancellationToken>()).Returns(
            new ClinicalContextDto(new(38, "F", null),
                [new("weight", 68.5m, "kg", observed), new("height", 161m, "cm", observed)], [], [], [], new(null, null, null)));
        var reservation = new NutritionDraftReservation { PatientId = fixture.Plan.PatientId, BatteryAssignmentId = Guid.NewGuid(), State = "generating" };
        var reservations = Substitute.For<INutritionDraftReservationStore>();
        reservations.ClaimAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(reservation);
        NutritionPrescription? generated = null;
        fixture.Repository.When(repository => repository.AddAsync(Arg.Any<NutritionPrescription>(), Arg.Any<CancellationToken>()))
            .Do(call => { generated = call.Arg<NutritionPrescription>();
                fixture.Repository.GetByIdAsync(generated.Id, Arg.Any<CancellationToken>()).Returns(generated); });
        var workflow = new NutritionDraftWorkflow(reservations, fixture.Assessments, fixture.Cases,
            NullLogger<NutritionDraftWorkflow>.Instance, TimeProvider.System);
        using var cancellation = new CancellationTokenSource();

        Assert.True(await workflow.ProcessNextAsync(cancellation.Token));

        var plan = Assert.IsType<NutritionPrescription>(generated);
        Assert.Equal(reservation.Id, plan.Id);
        Assert.Equal(NutritionSystemActor.Id, plan.CreatedBy);
        Assert.Equal("aprobado", plan.State);
        Assert.True(plan.IsPublished);
        Assert.Equal(NutritionSystemActor.Id, plan.ApprovedBy);
        Assert.Equal(NutritionInstructionTranslation.SourceHash(plan.Instructions), plan.InstructionsEnSourceHash);
        Assert.Equal(plan.InstructionsEn, NutritionInstructionTranslation.VerifiedEnglish(plan));
        using var snapshot = JsonDocument.Parse(plan.ClinicalProfileJson);
        Assert.Equal(68.5m, snapshot.RootElement.GetProperty("measuredFields").GetProperty("weightKg").GetProperty("value").GetDecimal());
        Assert.Equal("clinical_context", snapshot.RootElement.GetProperty("measuredFields").GetProperty("weightKg").GetProperty("source").GetString());
        Assert.True(snapshot.RootElement.TryGetProperty("assessmentResults", out _));
        await fixture.Assessments.Received(1).ReadProfileAsync(fixture.Plan.PatientId, reservation.BatteryAssignmentId, cancellation.Token);
        await fixture.Drafts.Received(1).GenerateAsync(Arg.Is<NutritionDraftRequest>(request => request.ClinicalProfile != null
            && request.ClinicalProfile.EngineVersion == "nutrition-engine-v1"
            && request.ClinicalProfile.Fields["weightKg"].Source == "clinical_context"
            && !request.ClinicalProfile.Fields.ContainsKey("birthDate")), cancellation.Token);
        await reservations.Received(1).CompleteAsync(reservation, reservation.Id, !validDraft, cancellation.Token);
        await reservations.DidNotReceiveWithAnyArgs().FailAsync(default!, default!, default, default);
        Assert.Equal(Source, fixture.Plan.Instructions);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Regenerate_UsesGeneratedProvenance_OnlyWhenReplacingTheInstructionPair(bool preserveConfirmed)
    {
        var fixture = Fixture();
        fixture.Plan.ManualEditsConfirmed = preserveConfirmed;
        await fixture.Cases.ExecuteAsync(fixture.Command(NutritionOperation.Regenerate), CancellationToken.None);
        Assert.Equal(preserveConfirmed ? Source : "Narrativa nueva\nIndicaciones nuevas", fixture.Plan.Instructions);
        Assert.Equal(preserveConfirmed ? null : NutritionInstructionTranslation.SourceHash(fixture.Plan.Instructions), fixture.Plan.InstructionsEnSourceHash);
        Assert.Equal(preserveConfirmed ? null : fixture.Plan.InstructionsEn, NutritionInstructionTranslation.VerifiedEnglish(fixture.Plan));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateVersion_CopiesExistingProvenance_WithoutBlessingAnUnverifiedPair(bool verified)
    {
        var fixture = Fixture();
        fixture.Plan.State = "aprobado";
        fixture.Plan.InstructionsEnSourceHash = verified ? NutritionInstructionTranslation.SourceHash(Source) : null;
        NutritionPrescription? copy = null;
        fixture.Repository.When(repository => repository.AddAsync(Arg.Any<NutritionPrescription>(), Arg.Any<CancellationToken>()))
            .Do(call => copy = call.Arg<NutritionPrescription>());
        await fixture.Cases.ExecuteAsync(fixture.Command(NutritionOperation.CreateVersion), CancellationToken.None);
        var plan = Assert.IsType<NutritionPrescription>(copy);
        Assert.Equal(fixture.Plan.InstructionsEnSourceHash, plan.InstructionsEnSourceHash);
        Assert.Equal(verified ? English : null, NutritionInstructionTranslation.VerifiedEnglish(plan));
        Assert.Equal(Source, plan.Instructions);
        Assert.Equal(English, plan.InstructionsEn);
    }

    [Fact]
    public async Task UnverifiedRead_TranslatesTheCurrentSource_WithoutUsingOrModifyingStaleEnglish()
    {
        var fixture = Fixture();
        fixture.Plan.Instructions = "Sin azúcar. Máximo 1 g de sal.";
        fixture.Plan.InstructionsEnSourceHash = NutritionInstructionTranslation.SourceHash(Source);
        var translator = Substitute.For<IContentTranslationService>();
        translator.TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyDictionary<string, string>>(
                call.Arg<IReadOnlyCollection<string>>().ToDictionary(source => source, _ => "No sugar. Maximum 1 g of salt.")));
        var source = new JsonObject { ["days"] = new JsonArray(new JsonObject
        { ["meals"] = new JsonArray(new JsonObject
            { ["notes"] = fixture.Plan.Instructions, ["notesEn"] = NutritionInstructionTranslation.VerifiedEnglish(fixture.Plan), ["calories"] = 320 }) }) };
        var localized = await new PatientContentLocalization(translator).TranslateAsync(source, PatientContentProfile.NutritionPlan, "en", CancellationToken.None);
        Assert.Equal("No sugar. Maximum 1 g of salt.", localized["days"]![0]!["meals"]![0]!["notes"]!.GetValue<string>());
        Assert.Equal(320, localized["days"]![0]!["meals"]![0]!["calories"]!.GetValue<int>());
        Assert.Equal("Sin azúcar. Máximo 1 g de sal.", fixture.Plan.Instructions);
        Assert.Equal(English, fixture.Plan.InstructionsEn);
        Assert.Equal(fixture.Plan.Instructions, source["days"]![0]!["meals"]![0]!["notes"]!.GetValue<string>());
        await translator.Received(1).TranslateAsync(Arg.Is<IReadOnlyCollection<string>>(texts => texts.SequenceEqual(new[] { fixture.Plan.Instructions })), "en", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SourceHash_UsesExactUtf8_AndDeclinesMissingOrMismatchedProvenance()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", NutritionInstructionTranslation.SourceHash("abc"));
        Assert.NotEqual(NutritionInstructionTranslation.SourceHash(Source), NutritionInstructionTranslation.SourceHash(Source + " "));
        Assert.NotEqual(NutritionInstructionTranslation.SourceHash("azúcar"), NutritionInstructionTranslation.SourceHash("azu\u0301car"));
        Assert.Null(NutritionInstructionTranslation.PairedSourceHash("", English));
        Assert.Null(NutritionInstructionTranslation.PairedSourceHash(Source, " "));
    }

    private sealed record CaseFixture(NutritionPrescription Plan, NutritionCases Cases, INutritionPrescriptionRepository Repository,
        IClinicalContextService Clinical, INutritionDraftClient Drafts, IProgramAssignmentRepository Assessments)
    {
        public NutritionCaseCommand Command(NutritionOperation operation) => new(operation, Plan.PatientId, Plan.Id,
            Plan.Revision, new NutritionActor(Guid.NewGuid(), null, true, true, true));
    }

    private static CaseFixture Fixture(bool validDraft = true)
    {
        var foods = NutritionMealSolverTests.Foods();
        var engine = new NutritionEngine(foods);
        var patient = new NutritionPatient(68.5, 161, 38, "F", 26.4, 5.9, "MARCHA") { BirthDate = new(1988, 1, 1) };
        var plan = new NutritionPrescription
        {
            PatientId = Guid.NewGuid(), Instructions = Source, InstructionsEn = English,
            ClinicalProfileJson = JsonSerializer.Serialize(patient), TargetsJson = JsonSerializer.Serialize(NutritionEngine.ComputeTargets(patient)),
            Meals = engine.BuildDays(patient).SelectMany(day => day.Meals.Select(meal => new NutritionPrescriptionMeal
            {
                DayNumber = day.D, MealType = meal.Key, ScheduledTime = new TimeOnly(7, 0),
                Items = meal.Value.Select(item => new NutritionPrescriptionItem { FoodItemId = item.Id, Grams = item.G }).ToList(),
            })).ToList(),
        };
        var repository = Substitute.For<INutritionPrescriptionRepository>();
        repository.ListFoodsAsync(Arg.Any<CancellationToken>()).Returns(foods.Values.ToArray());
        repository.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        var store = Substitute.For<INutritionCaseStore>();
        store.CanAccessAsync(plan.PatientId, Arg.Any<NutritionActor>(), Arg.Any<CancellationToken>()).Returns(true);
        store.GetContextAsync(plan.PatientId, Arg.Any<CancellationToken>()).Returns(new NutritionCaseContext(new(1988, 1, 1), "F", "America/Bogota", true, null));
        store.ExecuteAsync(plan.PatientId, Arg.Any<Func<CancellationToken, Task<NutritionView>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<NutritionView>>>()(call.Arg<CancellationToken>()));
        store.NextVersionAsync(plan.CycleId, Arg.Any<CancellationToken>()).Returns(2);
        var clinical = Substitute.For<IClinicalContextService>();
        clinical.ConsolidateAsync(plan.PatientId, Arg.Any<CancellationToken>()).Returns(new ClinicalContextDto(new(38, "F", null), [], [], [], [], new(null, null, null)));
        var drafts = Substitute.For<INutritionDraftClient>();
        drafts.GenerateAsync(Arg.Any<NutritionDraftRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            new NutritionDraftResponse(new("Narrativa nueva", "New narrative"), new("Indicaciones nuevas", validDraft ? "New instructions" : ""), call.Arg<NutritionDraftRequest>().Menus, "llm"));
        var assessments = Substitute.For<IProgramAssignmentRepository>();
        return new(plan, new NutritionCases(repository, store, drafts, clinical, assessments, TimeProvider.System), repository, clinical, drafts, assessments);
    }

    private static string NumericAndApprovalState(NutritionPrescription plan) => JsonSerializer.Serialize(new
    {
        plan.CalorieTarget, plan.CarbsTarget, plan.ProteinTarget, plan.FatTarget, plan.FiberTarget,
        plan.WaterMl, plan.State, plan.IsPublished, plan.ApprovedBy, plan.ApprovedAt,
        Meals = plan.Meals.Select(meal => new
        {
            meal.DayNumber, meal.MealType, meal.ScheduledTime, meal.WaterMl,
            Items = meal.Items.Select(item => new { item.FoodItemId, item.Grams }),
        }),
    });
}
