using System.Security.Claims;
using System.Text.Json;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.DTOs.FoodAi;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.FoodAi;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Localization;

public sealed class FoodHistoryConsentTests
{
    private static FoodAiController Controller(Guid user, IFoodAnalysisRepository repository, IContentTranslationService translations)
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
            [new(ClaimTypes.NameIdentifier, user.ToString()), new("aud", "app")], "test")) };
        http.Request.Headers["X-App-Language"] = "en";
        return new(Substitute.For<IFoodAiClient>(), repository, Substitute.For<IMediator>(),
            NullLogger<FoodAiController>.Instance, translations)
            { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    [Theory]
    [InlineData("CONSENT_REQUIRED", 403)]
    [InlineData("CONSENT_DOCUMENT_UNAVAILABLE", 503)]
    public async Task Own_history_remains_readable_without_new_translation_consent(string code, int status)
    {
        var user = Guid.NewGuid(); var id = Guid.NewGuid();
        var repository = Substitute.For<IFoodAnalysisRepository>();
        repository.GetByAnalysisIdAsync(id, Arg.Any<CancellationToken>()).Returns(new FoodAnalysis
        {
            AnalysisId = id, UserId = user, Items =
            [new() { Name = "Arroz", NameEn = "Rice", ItemIndex = 0 }, new() { Name = "Pollo", ItemIndex = 1 }]
        });
        var translations = Substitute.For<IContentTranslationService>();
        translations.TranslateAsync(Arg.Any<IReadOnlyCollection<string>>(), "en", Arg.Any<CancellationToken>(),
                new ContentTranslationSubject("food", user))
            .Returns(Task.FromException<IReadOnlyDictionary<string, string>>(new AiConsentException(code, "food", status)));
        var response = Assert.IsType<OkObjectResult>(await Controller(user, repository, translations).GetAnalysis(id, default));
        var payload = JsonSerializer.SerializeToElement(response.Value);
        var foods = payload.GetProperty("foods");
        Assert.Equal("Rice", foods[0].GetProperty("nameEn").GetString());
        Assert.Equal("Pollo", foods[1].GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, foods[1].GetProperty("nameEn").ValueKind);
        await translations.Received(1).TranslateAsync(Arg.Is<IReadOnlyCollection<string>>(texts => texts.SequenceEqual(new[] { "Pollo" })),
            "en", Arg.Any<CancellationToken>(), new ContentTranslationSubject("food", user));
        await repository.DidNotReceive().AddAsync(Arg.Any<FoodAnalysis>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Foreign_or_legacy_anonymous_analysis_cannot_be_read_or_changed(bool anonymous)
    {
        var user = Guid.NewGuid(); var id = Guid.NewGuid();
        var repository = Substitute.For<IFoodAnalysisRepository>();
        repository.GetByAnalysisIdAsync(id, Arg.Any<CancellationToken>()).Returns(new FoodAnalysis
        { AnalysisId = id, UserId = anonymous ? null : Guid.NewGuid(), Items = [new() { Name = "Arroz" }] });
        var translations = Substitute.For<IContentTranslationService>();
        var controller = Controller(user, repository, translations);
        Assert.IsType<NotFoundObjectResult>(await controller.GetAnalysis(id, default));
        Assert.IsType<NotFoundObjectResult>(await controller.PostFeedback(id, new FoodFeedbackRequest(null, "FOOD_WRONG"), default));
        Assert.Empty(translations.ReceivedCalls());
        await repository.DidNotReceive().AddFeedbackAsync(Arg.Any<FoodAnalysisFeedback>(), Arg.Any<CancellationToken>());
    }
}
