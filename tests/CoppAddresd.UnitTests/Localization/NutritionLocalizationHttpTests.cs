using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Api.Localization;
using CoppAddresd.Api.Middleware;
using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.Nutrition.Queries.GetMyNutritionPlan;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetSnapshot;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.Localization;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CoppAddresd.UnitTests.Localization;

/// <summary>Regresión del texto reportado en el dispositivo: rutas MVC reales y DTO tipados, sin BD ni proveedor IA.</summary>
public sealed class NutritionLocalizationHttpTests
{
    private const string ReportedPhrase = "Propuesta determinista pendiente de revisión.";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ReportedPhrase] = "Deterministic proposal pending review.",
        ["Plan nutricional"] = "Nutrition plan",
        ["Control metabólico"] = "Metabolic management",
        ["Sin maní"] = "No peanuts",
        ["Comidas a las 07:00, 12:00, 15:30 y 19:00"] = "Meals at 07:00, 12:00, 15:30 and 19:00",
        ["Avena con fruta"] = "Oatmeal with fruit",
        ["Avena: 45.5 g; Banano: 100 g"] = "Oats: 45.5 g; Banana: 100 g",
        ["Arroz con pollo"] = "Chicken and rice",
        ["Arroz: 150 g; Pollo: 120 g"] = "Rice: 150 g; Chicken: 120 g",
        ["Evitar azúcar; máximo 2 g de sal."] = "Avoid sugar; maximum 2 g of salt.",
        ["Yogur sin azúcar"] = "Unsweetened yogurt",
        ["Yogur natural: 125 g; Nueces: 15 g"] = "Plain yogurt: 125 g; Walnuts: 15 g",
        ["Mantener la porción de 125 g."] = "Keep the 125 g portion.",
        ["Sopa de verduras"] = "Vegetable soup",
        ["Zanahoria: 80 g; Agua: 250 ml"] = "Carrot: 80 g; Water: 250 ml",
        ["Tomar con 250 ml de agua."] = "Take with 250 ml of water.",
        ["Programa clínico"] = "Clinical program",
        ["Menú asignado"] = "Assigned menu",
        ["Indicaciones del plan publicado"] = "Published plan instructions",
    };

    private sealed class Translator : IContentTranslationService
    {
        public bool Unavailable { get; set; }
        public List<string[]> Calls { get; } = [];
        public Task<IReadOnlyDictionary<string, string>> TranslateAsync(IReadOnlyCollection<string> sources, string language, CancellationToken ct, ContentTranslationSubject? subject = null)
        {
            ct.ThrowIfCancellationRequested();
            Assert.Equal("en", language);
            Calls.Add(sources.ToArray());
            if (Unavailable) throw new ContentTranslationUnavailableException();
            // Un campo nuevo sin traducción debe romper el test, no recibir un prefijo inglés artificial.
            return Task.FromResult<IReadOnlyDictionary<string, string>>(sources.ToDictionary(source => source, source => English[source], StringComparer.Ordinal));
        }
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("X-Test-Authenticated")) return Task.FromResult(AuthenticateResult.NoResult());
            var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "11111111-1111-4111-8111-111111111111"), new Claim("aud", "app")], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user, Scheme.Name)));
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        public Guid UserId { get; } = Guid.Parse("11111111-1111-4111-8111-111111111111");
        public Guid EnrollmentId { get; } = Guid.Parse("22222222-2222-4222-8222-222222222222");
        public Translator Translations { get; } = new();
        public IMediator Mediator { get; } = Substitute.For<IMediator>();
        public MyNutritionPlanDto Plan { get; }
        public ProgramSnapshotDto Snapshot { get; }
        public HttpClient Client { get; private set; } = null!;
        private WebApplication application = null!;

        private Harness()
        {
            Plan = new(Guid.Parse("33333333-3333-4333-8333-333333333333"), "Plan nutricional", "Control metabólico", 30,
                1800, 100.5m, 220.25m, 55.75m, 28.5m, 2250, "Sin maní", "Comidas a las 07:00, 12:00, 15:30 y 19:00", "Active",
                [new(1, 2250,
                [
                    new("Desayuno", "Avena con fruta", "Avena: 45.5 g; Banano: 100 g", 320, 12.5m, 55.25m, 6.75m, 5.5m, 250, ReportedPhrase, 1, "07:00"),
                    new("Almuerzo", "Arroz con pollo", "Arroz: 150 g; Pollo: 120 g", 540, 40.25m, 65.5m, 12.75m, 4.25m, 300, "Evitar azúcar; máximo 2 g de sal.", 2, "12:00"),
                    new("Snack", "Yogur sin azúcar", "Yogur natural: 125 g; Nueces: 15 g", 190, 9.5m, 15.25m, 11.75m, 2.5m, 200, "Mantener la porción de 125 g.", 3, "15:30"),
                    new("Cena", "Sopa de verduras", "Zanahoria: 80 g; Agua: 250 ml", 280, 15.75m, 35.5m, 8.25m, 6.5m, 250, "Tomar con 250 ml de agua.", 4, "19:00"),
                ])], new(2026, 11, 6), new(2026, 12, 5), 1, 7);
            Snapshot = new(EnrollmentId,
                new(Guid.Parse("44444444-4444-4444-8444-444444444444"), "clinical-program-83", "Programa clínico", 12, 3,
                    ProgramWeekStatus.Active, new(2026, 11, 2), new(2026, 11, 8), 4, ["nut", "vitals"]), new(2026, 11, 6),
                [new(TaskCode.nut, "Nutrición del día", "Menú del día", 100, "Pending", null,
                    new(null, "Menú asignado", null, null, NutritionPlanId: Plan.Id, NutritionPlanName: Plan.Name,
                        NutritionPlanDayNumber: 1, DailyCalorieTarget: Plan.DailyCalorieTarget, DailyProteinTarget: Plan.DailyProteinTarget,
                        DailyCarbsTarget: Plan.DailyCarbsTarget, DailyFatTarget: Plan.DailyFatTarget, DailyFiberTarget: Plan.DailyFiberTarget,
                        NutritionMeals: Plan.Days[0].Meals.Select(meal => new NutritionMealDto(meal.MealType, meal.Description,
                            meal.Foods, meal.Calories, meal.ProteinG, meal.CarbsG, meal.FatG, meal.FiberG, meal.WaterMl, meal.Notes, meal.SortOrder)).ToArray(),
                        Description: "Indicaciones del plan publicado"),
                    EmotionalAssessment: new(4, "trabajo", new(Notes: "Mi nota personal sin traducir", SleepMinutes: 420, SleepSource: "manual")))],
                100, true, 650, new(450, "level-2", 900), new(3, 8, 1, 1, null, 0), 7, [], DailyBonusAmount: 50);
            Mediator.Send(Arg.Any<GetMyNutritionPlanQuery>(), Arg.Any<CancellationToken>()).Returns(Plan);
            Mediator.Send(Arg.Any<GetSnapshotQuery>(), Arg.Any<CancellationToken>()).Returns(Snapshot);
        }

        public static async Task<Harness> CreateAsync()
        {
            var test = new Harness();
            var current = Substitute.For<ICurrentContext>(); current.UserId.Returns(test.UserId);
            var actor = Substitute.For<IProgramActorContext>(); actor.ResolveActiveEnrollmentIdAsync(Arg.Any<CancellationToken>()).Returns(test.EnrollmentId);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
            var services = builder.Services;
            services.AddSingleton(test.Mediator); services.AddSingleton(current); services.AddSingleton(actor);
            services.AddSingleton(Substitute.For<IObjectStorageService>());
            services.AddSingleton<IContentTranslationService>(test.Translations);
            services.AddScoped<PatientContentLocalization>(); services.AddScoped<PatientContentLocalizationFilter>();
            services.AddAuthentication("NutritionLocalizationTest")
                .AddScheme<AuthenticationSchemeOptions, TestAuthentication>("NutritionLocalizationTest", _ => { });
            services.AddAuthorization();
            services.AddControllers(options => options.Filters.Add<PatientContentLocalizationFilter>())
                .AddApplicationPart(typeof(MyPatientProfileController).Assembly)
                .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            test.application = builder.Build();
            test.application.UseMiddleware<ExceptionHandlingMiddleware>(); test.application.UseRouting();
            test.application.UseAuthentication(); test.application.UseAuthorization(); test.application.MapControllers();
            await test.application.StartAsync(); test.Client = test.application.GetTestClient();
            return test;
        }

        public async ValueTask DisposeAsync() { Client.Dispose(); await application.DisposeAsync(); }
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task RealNutritionPlanRoute_TranslatesReportedPhraseAndAllFourMeals_WithoutChangingCanonicalData(string language)
    {
        await using var test = await Harness.CreateAsync();
        var before = JsonSerializer.Serialize(test.Plan, Json);
        using var request = Request("/api/v1/me/nutrition-plan", language);
        using var response = await test.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(language, Assert.Single(response.Content.Headers.ContentLanguage));
        Assert.Contains("Accept-Language", response.Headers.Vary);
        var actual = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var expected = language == "en" ? EnglishPlan(test.Plan) : test.Plan;
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected, Json), actual), actual.ToJsonString());
        AssertMeals(actual["days"]![0]!["meals"]!.AsArray(), test.Plan.Days[0].Meals, language);
        Assert.Equal(before, JsonSerializer.Serialize(test.Plan, Json));
        AssertTranslationInputs(test.Translations, language, test.Plan.Days[0].Meals);
        await test.Mediator.Received(1).Send(Arg.Is<GetMyNutritionPlanQuery>(query => query.UserId == test.UserId), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public async Task RealSnapshotRoute_TranslatesNutritionMealsAndReportedPhrase_WithoutChangingIdentityNutrientsOrPatientNotes(string language)
    {
        await using var test = await Harness.CreateAsync();
        var before = JsonSerializer.Serialize(test.Snapshot, Json);
        using var request = Request("/api/v1/program/me/snapshot", language);
        using var response = await test.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(language, Assert.Single(response.Content.Headers.ContentLanguage));
        Assert.Contains("Accept-Language", response.Headers.Vary);
        var actual = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var sourceTask = test.Snapshot.TodayTasks[0];
        var expected = language == "en" ? test.Snapshot with
        {
            Template = test.Snapshot.Template with { Name = English[test.Snapshot.Template.Name] },
            TodayTasks = [sourceTask with { Content = sourceTask.Content! with
            {
                Title = English[sourceTask.Content!.Title!], Description = English[sourceTask.Content.Description!],
                NutritionPlanName = English[sourceTask.Content.NutritionPlanName!],
                NutritionMeals = sourceTask.Content.NutritionMeals!.Select(meal => meal with
                { Description = English[meal.Description!], Foods = English[meal.Foods!], Notes = English[meal.Notes!] }).ToArray(),
            } }],
        } : test.Snapshot;
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected, Json), actual), actual.ToJsonString());
        AssertMeals(actual["todayTasks"]![0]!["content"]!["nutritionMeals"]!.AsArray(), test.Plan.Days[0].Meals, language);
        Assert.Equal("Mi nota personal sin traducir", actual["todayTasks"]![0]!["emotionalAssessment"]!["answers"]!["notes"]!.GetValue<string>());
        Assert.Equal(before, JsonSerializer.Serialize(test.Snapshot, Json));
        AssertTranslationInputs(test.Translations, language, test.Plan.Days[0].Meals);
        await test.Mediator.Received(1).Send(Arg.Is<GetSnapshotQuery>(query => query.EnrollmentId == test.EnrollmentId), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("/api/v1/me/nutrition-plan")]
    [InlineData("/api/v1/program/me/snapshot")]
    public async Task TranslationUnavailable_DoesNotReturnReportedSpanishMealTextAsSuccessfulEnglish(string path)
    {
        await using var test = await Harness.CreateAsync(); test.Translations.Unavailable = true;
        using var request = Request(path, "en");
        using var response = await test.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5), response.Headers.RetryAfter!.Delta);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("CONTENT_TRANSLATION_UNAVAILABLE", JsonNode.Parse(body)!["title"]!.GetValue<string>());
        Assert.DoesNotContain(ReportedPhrase, body);
        Assert.DoesNotContain("Avena con fruta", body);
        Assert.Single(test.Translations.Calls);
        Assert.Equal(ReportedPhrase, test.Plan.Days[0].Meals[0].Notes);
    }

    private static HttpRequestMessage Request(string path, string language)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-Authenticated", "true");
        request.Headers.TryAddWithoutValidation("Accept-Language", language);
        request.Headers.TryAddWithoutValidation("X-App-Language", language);
        return request;
    }

    private static MyNutritionPlanDto EnglishPlan(MyNutritionPlanDto plan) => plan with
    {
        Name = English[plan.Name], TargetCondition = English[plan.TargetCondition!],
        Allergens = English[plan.Allergens!], MealTiming = English[plan.MealTiming!],
        Days = plan.Days.Select(day => day with { Meals = day.Meals.Select(meal => meal with
        { Description = English[meal.Description!], Foods = English[meal.Foods!], Notes = English[meal.Notes!] }).ToList() }).ToList(),
    };

    private static void AssertMeals(JsonArray meals, IReadOnlyList<MyNutritionPlanMealDto> sources, string language)
    {
        Assert.Equal(4, meals.Count);
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index]; var meal = meals[index]!;
            Assert.Equal(language == "en" ? English[source.Description!] : source.Description, meal["description"]!.GetValue<string>());
            Assert.Equal(language == "en" ? English[source.Foods!] : source.Foods, meal["foods"]!.GetValue<string>());
            Assert.Equal(language == "en" ? English[source.Notes!] : source.Notes, meal["notes"]!.GetValue<string>());
            Assert.Equal(source.MealType, meal["mealType"]!.GetValue<string>());
            Assert.Equal(source.Calories, meal["calories"]!.GetValue<int>());
            Assert.Equal(source.ProteinG, meal["proteinG"]!.GetValue<decimal>());
            Assert.Equal(source.CarbsG, meal["carbsG"]!.GetValue<decimal>());
            Assert.Equal(source.FatG, meal["fatG"]!.GetValue<decimal>());
            Assert.Equal(source.FiberG, meal["fiberG"]!.GetValue<decimal>());
            Assert.Equal(source.WaterMl, meal["waterMl"]!.GetValue<int>());
            Assert.Equal(source.SortOrder, meal["sortOrder"]!.GetValue<int>());
        }
    }

    private static void AssertTranslationInputs(Translator translator, string language, IReadOnlyList<MyNutritionPlanMealDto> meals)
    {
        if (language == "es") { Assert.Empty(translator.Calls); return; }
        var requested = Assert.Single(translator.Calls);
        Assert.Contains(ReportedPhrase, requested);
        Assert.All(meals, meal =>
        {
            Assert.Contains(meal.Description!, requested);
            Assert.Contains(meal.Foods!, requested);
            Assert.Contains(meal.Notes!, requested);
            Assert.DoesNotContain(meal.MealType, requested);
        });
        Assert.DoesNotContain("Mi nota personal sin traducir", requested);
    }
}
