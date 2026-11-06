using System.Text.Json.Nodes;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.Localization;
using CoppAddresd.Domain.Exceptions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Localization;

public sealed class PatientContentLocalizationTests
{
    [Fact]
    public async Task Nutrition_UsesApprovedEnglishNotes_DeduplicatesSourceText_AndNeverMutatesSourceOrPatientData()
    {
        var original = JsonNode.Parse("""{"id":"plan-id","name":"Plan clínico","dailyCalorieTarget":1800,"mealTiming":"Desayuno a las 7:00","patient":{"id":"patient-id","name":"María García"},"userNotes":"Mi comentario privado","days":[{"dayNumber":1,"meals":[{"id":"meal-id","mealType":"des","foods":"Arroz 50 g","calories":180,"notes":"Revisar 2 porciones","notesEn":"Review 2 portions","answers":{"text":"Mi respuesta"}},{"foods":"Arroz 50 g","notes":"Sin azúcar"}]}]}""")!;
        var before = original.ToJsonString();
        var translator = Substitute.For<IContentTranslationService>();
        translator.TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", Arg.Any<CancellationToken>()).Returns(new Dictionary<string, string>
        { ["Plan clínico"] = "Clinical plan", ["Desayuno a las 7:00"] = "Breakfast at 7:00", ["Arroz 50 g"] = "Rice 50 g", ["Sin azúcar"] = "No added sugar", ["plan-id"] = "do not apply this extra key" });
        var result = await new PatientContentLocalization(translator).TranslateAsync(original, PatientContentProfile.NutritionPlan, "en", CancellationToken.None);
        Assert.Equal("Clinical plan", result["name"]!.GetValue<string>());
        Assert.Equal("Rice 50 g", result["days"]![0]!["meals"]![0]!["foods"]!.GetValue<string>());
        Assert.Equal("Rice 50 g", result["days"]![0]!["meals"]![1]!["foods"]!.GetValue<string>());
        Assert.Equal("Review 2 portions", result["days"]![0]!["meals"]![0]!["notes"]!.GetValue<string>());
        Assert.Equal(1800, result["dailyCalorieTarget"]!.GetValue<int>());
        Assert.Equal("Breakfast at 7:00", result["mealTiming"]!.GetValue<string>());
        Assert.Equal(180, result["days"]![0]!["meals"]![0]!["calories"]!.GetValue<int>());
        Assert.Equal("des", result["days"]![0]!["meals"]![0]!["mealType"]!.GetValue<string>());
        Assert.Equal("Mi respuesta", result["days"]![0]!["meals"]![0]!["answers"]!["text"]!.GetValue<string>());
        Assert.Equal("María García", result["patient"]!["name"]!.GetValue<string>());
        Assert.Equal("plan-id", result["id"]!.GetValue<string>());
        Assert.Equal("Mi comentario privado", result["userNotes"]!.GetValue<string>());
        Assert.Equal(before, original.ToJsonString()); Assert.NotSame(original, result);
        await translator.Received(1).TranslateAsync(Arg.Is<IReadOnlyCollection<string>>(sources => sources.Count == 4 && !sources.Contains("Revisar 2 porciones") && sources.Contains("Arroz 50 g")), "en", CancellationToken.None);
    }

    public static TheoryData<PatientContentProfile, string, string, string> ProfileCases => new()
    {
        { PatientContentProfile.ProgramSnapshot, """{"enrollmentId":"enrollment-id","template":{"name":"Programa"},"todayTasks":[{"taskCode":"emocional","content":{"title":"Lección","takeaways":["Respira","Respira"],"exercises":[{"name":"Marcha","repetitions":12,"durationSecs":60}]},"emotionalAssessment":{"answers":{"notes":"Mi respuesta libre"}}}]}""", "Programa", "Program" },
        { PatientContentProfile.Notifications, """{"items":[{"id":"notification-id","type":"hydration_reminder","title":"Agua","message":"Bebe 250 ml","sentAt":"2026-11-05"}],"data":[{"title":"Aviso","message":"Revisa tu plan"}]}""", "Agua", "Water" },
        { PatientContentProfile.AssessmentAssignments, """[{"id":"assignment-id","testCode":"sueno","testName":"Sueño","status":"pending","answers":{"text":"Mi respuesta"}}]""", "Sueño", "Sleep" },
        { PatientContentProfile.AssessmentBatteries, """[{"id":"battery-id","batteryName":"Batería inicial","code":"initial"}]""", "Batería inicial", "Initial assessment" },
        { PatientContentProfile.AssessmentDetail, """{"id":"test-id","code":"sueno","name":"Sueño","questions":[{"id":"question-id","code":"q1","text":"¿Cómo dormiste?","options":[{"id":"option-id","value":"fair","score":2,"text":"Regular"}],"answer":"Mi respuesta"}]}""", "Sueño", "Sleep" },
        { PatientContentProfile.AssessmentResults, """[{"id":"result-id","code":"sueno","label":"Sueño","qualifier":"Regular","value":15.5,"unit":"points","answers":{"body":"Mi respuesta"}}]""", "Sueño", "Sleep" },
        { PatientContentProfile.AssessmentHistory, """[{"id":"evaluation-id","testName":"Sueño","results":[{"code":"sueno","label":"Sueño","value":15.5}],"notes":"Mi respuesta"}]""", "Sueño", "Sleep" },
        { PatientContentProfile.Measurements, """{"items":[{"id":"measurement-id","metricCode":"body_fat","metricName":"Grasa corporal","value":23.5,"unit":"%","recordedAt":"2026-11-05","notes":"Mi observación"}]}""", "Grasa corporal", "Body fat" },
        { PatientContentProfile.Specialties, """[{"id":"specialty-id","code":"FAMILY_MEDICINE","name":"Medicina familiar","category":"Medicina","description":"Atención primaria","isActive":true}]""", "Medicina familiar", "Family medicine" },
        { PatientContentProfile.ProfessionalsCatalog, """{"data":[{"id":"professional-id","fullName":"Dra. María García","professionalTypeName":"Médico","professionalTypeDisplayName":"Médico","specialties":[{"id":"specialty-id","code":"FAMILY_MEDICINE","name":"Medicina familiar"}],"locations":[{"id":"location-id","name":"Clínica Los Andes"}]}],"total":1}""", "Médico", "Physician" },
    };

    [Theory, MemberData(nameof(ProfileCases))]
    public async Task ExplicitProfiles_TranslateDisplayFields_AndPreserveAllOtherValues(PatientContentProfile profile, string source, string known, string english)
    {
        var original = JsonNode.Parse(source)!; var before = original.ToJsonString();
        var translator = Substitute.For<IContentTranslationService>();
        IReadOnlyCollection<string>? requested = null;
        translator.TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", Arg.Any<CancellationToken>()).Returns(call =>
        {
            requested = call.ArgAt<IReadOnlyCollection<string>>(0);
            return requested.ToDictionary(text => text, text => text == known ? english : "English " + text, StringComparer.Ordinal);
        });
        var localized = await new PatientContentLocalization(translator).TranslateAsync(original, profile, "en", CancellationToken.None);
        Assert.Contains(english, localized.ToJsonString()); Assert.NotNull(requested); Assert.Contains(known, requested!);
        Assert.Equal(before, original.ToJsonString());
        var protectedStrings = new[] { "enrollment-id", "emocional", "Mi respuesta libre", "notification-id", "hydration_reminder", "2026-11-05", "assignment-id", "sueno", "pending", "Mi respuesta", "battery-id", "initial", "test-id", "question-id", "q1", "option-id", "fair", "result-id", "points", "evaluation-id", "measurement-id", "body_fat", "%", "Mi observación", "specialty-id", "FAMILY_MEDICINE", "professional-id", "Dra. María García", "location-id", "Clínica Los Andes" };
        foreach (var value in protectedStrings)
        {
            if (before.Contains(System.Text.Json.JsonSerializer.Serialize(value), StringComparison.Ordinal))
            {
                Assert.DoesNotContain(value, requested!);
                Assert.Contains(System.Text.Json.JsonSerializer.Serialize(value), localized.ToJsonString());
            }
        }
        var nestedSources = profile switch
        {
            PatientContentProfile.ProgramSnapshot => new[] { "Lección", "Respira", "Marcha" },
            PatientContentProfile.Notifications => ["Bebe 250 ml", "Aviso", "Revisa tu plan"],
            PatientContentProfile.AssessmentDetail => ["¿Cómo dormiste?", "Regular"],
            PatientContentProfile.Specialties => ["Medicina", "Atención primaria"],
            PatientContentProfile.ProfessionalsCatalog => ["Medicina familiar"],
            _ => [],
        };
        Assert.All(nestedSources, value => Assert.Contains(value, requested!));
        if (profile == PatientContentProfile.ProgramSnapshot)
        {
            Assert.Equal(1, requested!.Count(value => value == "Respira"));
            Assert.All(localized["todayTasks"]![0]!["content"]!["takeaways"]!.AsArray(), value => Assert.Equal("English Respira", value!.GetValue<string>()));
        }
        if (profile == PatientContentProfile.ProfessionalsCatalog)
        {
            Assert.Equal("Médico", localized["data"]![0]!["professionalTypeName"]!.GetValue<string>());
            Assert.Equal("Physician", localized["data"]![0]!["professionalTypeDisplayName"]!.GetValue<string>());
        }
        // Las cantidades se copian sin pasar al traductor como campos independientes.
        var beforeNumbers = Numbers(original); var afterNumbers = Numbers(localized);
        Assert.Equal(beforeNumbers, afterNumbers);
    }

    [Fact]
    public async Task SpanishAndNoDisplayFields_ReturnIndependentClonesWithoutCallingTranslator()
    {
        var translator = Substitute.For<IContentTranslationService>();
        var service = new PatientContentLocalization(translator);
        var original = JsonNode.Parse("""{"id":"id","name":"Plan","answers":{"text":"Libre"}}""")!;
        var es = await service.TranslateAsync(original, PatientContentProfile.NutritionPlan, "es", CancellationToken.None);
        Assert.Equal(original.ToJsonString(), es.ToJsonString()); Assert.NotSame(original, es);
        var raw = JsonNode.Parse("""{"id":"id","answers":{"text":"Libre"}}""")!;
        Assert.Equal(raw.ToJsonString(), (await service.TranslateAsync(raw, PatientContentProfile.NutritionPlan, "en", CancellationToken.None)).ToJsonString());
        await translator.DidNotReceiveWithAnyArgs().TranslateAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task NullIncompleteOrBlankTranslation_FailsWithoutMutatingOriginal(bool nullMap, bool blank)
    {
        var original = JsonNode.Parse("""{"name":"Plan"}""")!;
        var translator = Substitute.For<IContentTranslationService>();
        translator.TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<string, string>>(nullMap ? null! : blank ? new Dictionary<string, string> { ["Plan"] = " " } : new Dictionary<string, string>()));
        await Assert.ThrowsAsync<ContentTranslationUnavailableException>(() => new PatientContentLocalization(translator)
            .TranslateAsync(original, PatientContentProfile.NutritionPlan, "en", CancellationToken.None));
        Assert.Equal("Plan", original["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task CancellationDuringTranslation_IsPreservedEvenWhenTranslatorIgnoresIt()
    {
        var original = JsonNode.Parse("""{"name":"Plan"}""")!;
        using var cancellation = new CancellationTokenSource();
        var translator = Substitute.For<IContentTranslationService>();
        translator.TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", cancellation.Token).Returns(_ =>
        { cancellation.Cancel(); return (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["Plan"] = "Meal plan" }; });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PatientContentLocalization(translator)
            .TranslateAsync(original, PatientContentProfile.NutritionPlan, "en", cancellation.Token));
        Assert.Equal("Plan", original["name"]!.GetValue<string>());
    }

    private static string[] Numbers(JsonNode node)
    {
        var numbers = new List<string>();
        void Visit(JsonNode? current)
        {
            if (current is JsonValue value && value.GetValueKind() == System.Text.Json.JsonValueKind.Number) numbers.Add(value.ToJsonString());
            else if (current is JsonObject obj) foreach (var item in obj) Visit(item.Value);
            else if (current is JsonArray array) foreach (var item in array) Visit(item);
        }
        Visit(node); return numbers.ToArray();
    }
}
