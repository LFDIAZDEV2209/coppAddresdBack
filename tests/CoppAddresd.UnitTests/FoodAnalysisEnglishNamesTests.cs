using System.Security.Claims;
using System.Text.Json;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Features.FoodAi;
using CoppAddresd.Application.DTOs.FoodAi;
using CoppAddresd.Domain.Entities.FoodAi;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests;

public sealed class FoodAnalysisEnglishNamesTests
{
    [Fact]
    public async Task EnglishHistory_UsesStoredThenExactCatalogThenTranslation_WithoutChangingSources()
    {
        var owner = Guid.NewGuid();
        var analysis = new FoodAnalysis { AnalysisId = Guid.NewGuid(), UserId = owner,
            Items = [
                new() { ItemIndex = 0, Name = "Arroz con pollo", NameEn = "Chicken and rice", PortionSize = "medium", NutritionStatus = "available", EstimatedGrams = 180, Calories = 270 },
                new() { ItemIndex = 1, Name = "apple", EstimatedGrams = 100, Calories = 52 },
                new() { ItemIndex = 2, Name = "Receta familiar de la abuela", EstimatedGrams = 90, Calories = 120 },
            ] };
        var catalog = Substitute.For<IFoodNameCatalog>();
        catalog.GetEnglishNamesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { ["apple"] = "apple" });
        var translator = Substitute.For<IContentTranslationService>();
        translator.TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", Arg.Any<CancellationToken>(), Arg.Any<ContentTranslationSubject>())
            .Returns(new Dictionary<string, string> { ["Receta familiar de la abuela"] = "Grandmother's family recipe" });
        var controller = Controller(analysis, owner, catalog, translator);
        using var cancellation = new CancellationTokenSource();
        var response = Assert.IsType<OkObjectResult>(await controller.GetAnalysis(analysis.AnalysisId, cancellation.Token, "en"));
        var foods = JsonSerializer.SerializeToElement(response.Value).GetProperty("foods");
        Assert.Equal("Chicken and rice", foods[0].GetProperty("nameEn").GetString());
        Assert.Equal("apple", foods[1].GetProperty("nameEn").GetString());
        Assert.Equal("Grandmother's family recipe", foods[2].GetProperty("nameEn").GetString());
        Assert.Equal("Arroz con pollo", foods[0].GetProperty("name").GetString());
        Assert.Equal(180, foods[0].GetProperty("portion").GetProperty("estimatedGrams").GetInt32());
        Assert.Equal(270, foods[0].GetProperty("nutrition").GetProperty("calories").GetDecimal());
        Assert.Null(analysis.Items.ElementAt(1).NameEn);
        Assert.Null(analysis.Items.ElementAt(2).NameEn);
        Assert.Equal(270, analysis.Items.First().Calories);
        await translator.Received(1).TranslateAsync(Arg.Is<IReadOnlyCollection<string>>(names => names.SequenceEqual(new[] { "Receta familiar de la abuela" })), "en", cancellation.Token, new ContentTranslationSubject("food", owner));
    }

    [Fact]
    public async Task SpanishHistory_DoesNotCallTranslation_AndForeignAnalysisIsNotExposed()
    {
        var owner = Guid.NewGuid();
        var analysis = new FoodAnalysis { AnalysisId = Guid.NewGuid(), UserId = owner, Items = [new() { Name = "Receta familiar" }] };
        var translator = Substitute.For<IContentTranslationService>();
        var controller = Controller(analysis, owner, Substitute.For<IFoodNameCatalog>(), translator);
        var result = Assert.IsType<OkObjectResult>(await controller.GetAnalysis(analysis.AnalysisId, CancellationToken.None));
        Assert.Equal("Receta familiar", JsonSerializer.SerializeToElement(result.Value).GetProperty("foods")[0].GetProperty("name").GetString());
        await translator.DidNotReceiveWithAnyArgs().TranslateAsync(default!, default!, default);
        var foreign = Controller(analysis, Guid.NewGuid(), Substitute.For<IFoodNameCatalog>(), translator);
        Assert.IsType<NotFoundObjectResult>(await foreign.GetAnalysis(analysis.AnalysisId, CancellationToken.None, "en"));
        await translator.DidNotReceiveWithAnyArgs().TranslateAsync(default!, default!, default);
    }

    [Fact]
    public async Task MissingTranslation_FailsExplicitly_AndCancellationPropagates()
    {
        var owner = Guid.NewGuid();
        var analysis = new FoodAnalysis { AnalysisId = Guid.NewGuid(), UserId = owner, Items = [new() { Name = "Receta familiar" }] };
        var translator = Substitute.For<IContentTranslationService>();
        translator.TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", Arg.Any<CancellationToken>(), Arg.Any<ContentTranslationSubject>())
            .Returns(new Dictionary<string, string>());
        var catalog = Substitute.For<IFoodNameCatalog>();
        catalog.GetEnglishNamesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>());
        await Assert.ThrowsAsync<ContentTranslationUnavailableException>(() => Controller(analysis, owner, catalog, translator)
            .GetAnalysis(analysis.AnalysisId, CancellationToken.None, "en"));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        catalog.GetEnglishNamesAsync(Arg.Any<IReadOnlyCollection<string>>(), cancellation.Token)
            .Returns(Task.FromCanceled<IReadOnlyDictionary<string, string>>(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Controller(analysis, owner, catalog, translator)
            .GetAnalysis(analysis.AnalysisId, cancellation.Token, "en"));
    }

    [Fact]
    public async Task FreshAnalysis_EnglishRequest_LocalizesOnlyTheMissingDisplayName()
    {
        var owner = Guid.NewGuid();
        var food = new DetectedFoodDto("Receta familiar", .9, new(0, 0, 10, 10));
        var original = new AnalyzeFoodImageResult(Guid.NewGuid().ToString(), "completed", "qa", "qa", "qa", 1, [food]);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<AnalyzeFoodImageCommand>(), Arg.Any<CancellationToken>()).Returns(original);
        var translator = Substitute.For<IContentTranslationService>();
        translator.TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", Arg.Any<CancellationToken>(), Arg.Any<ContentTranslationSubject>())
            .Returns(new Dictionary<string, string> { ["Receta familiar"] = "Family recipe" });
        var catalog = Substitute.For<IFoodNameCatalog>();
        catalog.GetEnglishNamesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>());
        var controller = new FoodAiController(Substitute.For<IFoodAiClient>(), Substitute.For<IFoodAnalysisRepository>(), mediator,
            NullLogger<FoodAiController>.Instance, translator, catalog)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("aud", "app"), new Claim(ClaimTypes.NameIdentifier, owner.ToString())], "test")) } } };
        var image = new FormFile(new MemoryStream([1]), 0, 1, "image", "example.jpg") { Headers = new HeaderDictionary(), ContentType = "image/jpeg" };
        var response = await controller.Analyze(image, CancellationToken.None, "en");
        var payload = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal("Family recipe", payload.GetProperty("foods")[0].GetProperty("nameEn").GetString());
        Assert.Equal("Receta familiar", payload.GetProperty("foods")[0].GetProperty("name").GetString());
        Assert.Null(food.NameEn); Assert.Null(food.NutritionResult);
    }

    [Fact]
    public async Task CuratedCatalog_UsesExactIdentity_RejectsAmbiguityAndNutritionEquivalence()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, """{"foods":[{"canonical":"grilled_chicken","display_name":"Pollo a la plancha","fdc_name":"Chicken, meat and skin"},{"canonical":"egg","display_name":"Ambiguo"},{"canonical":"eggs","display_name":"Ambiguo"}]}""");
            var catalog = new CuratedFoodNameCatalog(path, NullLogger<CuratedFoodNameCatalog>.Instance);
            var names = await catalog.GetEnglishNamesAsync(["grilled_chicken", "Pollo a la plancha", "Ambiguo", "Chicken, meat and skin", "Receta familiar"], CancellationToken.None);
            Assert.Equal(2, names.Count);
            Assert.Equal("grilled chicken", names["grilled_chicken"]);
            Assert.Equal("grilled chicken", names["Pollo a la plancha"]);
            Assert.DoesNotContain("Ambiguo", names.Keys);
            Assert.DoesNotContain("Chicken, meat and skin", names.Keys);
            Assert.DoesNotContain("Receta familiar", names.Keys);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("app", null, null, false)]
    [InlineData("erp", "en", "en", false)]
    [InlineData("app", "es", null, false)]
    [InlineData("app", "en", null, true)]
    [InlineData("app", null, "en", true)]
    public async Task HistoryProjection_RequiresExplicitAppChoice_AndDoesNotTranslateLegacyAutomaticHeaders(
        string audience, string? selectedHeader, string? query, bool translates)
    {
        var owner = Guid.NewGuid();
        var analysis = new FoodAnalysis { AnalysisId = Guid.NewGuid(), UserId = owner, Items = [new() { Name = "Receta familiar", PortionSize = "medium", EstimatedGrams = 90, Calories = 120 }] };
        var translator = Substitute.For<IContentTranslationService>();
        translator.TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", Arg.Any<CancellationToken>(), Arg.Any<ContentTranslationSubject>())
            .Returns(new Dictionary<string, string> { ["Receta familiar"] = "Family recipe" });
        var catalog = Substitute.For<IFoodNameCatalog>();
        catalog.GetEnglishNamesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<string, string>());
        var controller = Controller(analysis, owner, catalog, translator);
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, owner.ToString()), new Claim("aud", audience)], "test"));
        controller.Request.Headers.AcceptLanguage = "en-US,en;q=0.9";
        if (selectedHeader is not null) controller.Request.Headers["X-App-Language"] = selectedHeader;
        var result = Assert.IsType<OkObjectResult>(await controller.GetAnalysis(analysis.AnalysisId, CancellationToken.None, query));
        var food = JsonSerializer.SerializeToElement(result.Value).GetProperty("foods")[0];
        Assert.Equal(translates ? "Family recipe" : null, food.GetProperty("nameEn").GetString());
        Assert.Equal("Receta familiar", food.GetProperty("name").GetString());
        Assert.Equal(90, food.GetProperty("portion").GetProperty("estimatedGrams").GetInt32());
        Assert.Null(Assert.Single(analysis.Items).NameEn);
        if (translates) await translator.Received(1).TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", Arg.Any<CancellationToken>(), new ContentTranslationSubject("food", owner));
        else await translator.DidNotReceiveWithAnyArgs().TranslateAsync(default!, default!, default);
    }

    private static FoodAiController Controller(FoodAnalysis analysis, Guid owner, IFoodNameCatalog catalog, IContentTranslationService translator)
    {
        var repository = Substitute.For<IFoodAnalysisRepository>();
        repository.GetByAnalysisIdAsync(analysis.AnalysisId, Arg.Any<CancellationToken>()).Returns(analysis);
        return new(Substitute.For<IFoodAiClient>(), repository, Substitute.For<IMediator>(), NullLogger<FoodAiController>.Instance,
            translator, catalog, Substitute.For<IPatientScopeGuard>(), Substitute.For<CoppAddresd.Api.Context.ICurrentContext>())
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner.ToString()), new Claim("aud", "app")], "test")) } },
        };
    }
}
