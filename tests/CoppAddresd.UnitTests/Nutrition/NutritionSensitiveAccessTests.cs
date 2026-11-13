using System.Security.Claims;
using System.Text.Json;
using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Api.Security;
using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.FoodAi;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Nutrition;

public sealed class NutritionSensitiveAccessTests
{
    [Theory]
    [InlineData("FOODAI/imagen.png")] [InlineData("media/../foodai/imagen.png")]
    [InlineData("media\\..\\foodai\\imagen.png")]
    public void PhotoAliases_StillRequireClinicalAuthorization(string key) => Assert.True(FoodPhotoAccess.IsPhotoKey(key));

    [Theory]
    [InlineData("asignado")] [InlineData("ajeno")] [InlineData("Admin")] [InlineData("sin_fotos")]
    [InlineData("app_propio")] [InlineData("app_ajeno")]
    public async Task PhotoSigning_ChecksScopeAndPermissionBeforeStorage_AndLimitsLifetime(string scenario)
    {
        var patient = Guid.NewGuid(); var owner = Guid.NewGuid(); var id = Guid.NewGuid(); var user = Guid.NewGuid();
        var analysis = new FoodAnalysis { AnalysisId = id, UserId = owner, ImageKey = $"foodai/{id:N}.png" };
        var analyses = Substitute.For<IFoodAnalysisRepository>();
        analyses.GetByAnalysisIdAsync(id, Arg.Any<CancellationToken>()).Returns(analysis);
        var context = Substitute.For<ICurrentContext>();
        context.UserId.Returns(scenario == "app_propio" ? owner : user);
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim("aud", scenario.StartsWith("app") ? "app" : "erp")], "test")) } };
        var actor = Substitute.For<IPatientScopeActor>(); actor.UserId.Returns(user); actor.IsAdmin.Returns(scenario == "Admin");
        actor.ProfessionalIdAsync(Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());
        actor.HasPermissionAsync("Nutrition.PhotosView", Arg.Any<CancellationToken>()).Returns(scenario != "sin_fotos");
        var store = Substitute.For<IPatientScopeStore>();
        var visible = (scenario == "ajeno" ? Array.Empty<Guid>() : new[] { patient }).AsQueryable();
        store.VisiblePatients(Arg.Any<Guid?>(), Arg.Any<bool>()).Returns(visible);
        store.ContainsAsync(visible, patient, Arg.Any<CancellationToken>()).Returns(visible.Contains(patient));
        store.OwnerAsync(PatientResource.FoodAnalysis, id, Arg.Any<CancellationToken>()).Returns(patient);
        var access = new FoodPhotoAccess(analyses, new PatientScopeGuard(actor, store), context, http);
        var objects = Substitute.For<IObjectStorageService>(); objects.IsCloudStorage.Returns(true);
        objects.GetFoodPhotoReadUrlAsync(analysis.ImageKey, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns("https://example.invalid/photo");
        var controller = new StorageController(objects, new StorageSignatureService(Guid.NewGuid().ToString()),
            Substitute.For<IDocumentRepository>(), Substitute.For<IPatientRepository>(), context, access)
        { ControllerContext = new() { HttpContext = http.HttpContext } };
        if (scenario is "ajeno" or "app_ajeno")
            await Assert.ThrowsAsync<NotFoundException>(() => controller.Sign(analysis.ImageKey, 900, CancellationToken.None));
        else if (scenario == "sin_fotos")
            await Assert.ThrowsAsync<ForbiddenException>(() => controller.Sign(analysis.ImageKey, 900, CancellationToken.None));
        else
        {
            var result = Assert.IsType<OkObjectResult>((await controller.Sign(analysis.ImageKey, 900, CancellationToken.None)).Result);
            Assert.Equal(300, JsonSerializer.SerializeToElement(result.Value).GetProperty("expiresInSeconds").GetInt32());
        }
        if (scenario is "ajeno" or "sin_fotos" or "app_ajeno")
            await objects.DidNotReceiveWithAnyArgs().GetFoodPhotoReadUrlAsync(default!, default, default);
    }

    [Fact]
    public async Task Coordinator_AnalysisRead_DoesNotExposePhotoOrMaskKeys()
    {
        var user = Guid.NewGuid(); var id = Guid.NewGuid();
        var repository = Substitute.For<IFoodAnalysisRepository>();
        repository.GetByAnalysisIdAsync(id, Arg.Any<CancellationToken>()).Returns(new FoodAnalysis
        { AnalysisId = id, UserId = Guid.NewGuid(), ImageKey = $"foodai/{id:N}.png", Items = [new() { Name = "Avena", MaskKey = "foodai/masks/private.png" }] });
        var scope = Substitute.For<IPatientScopeGuard>(); var context = Substitute.For<ICurrentContext>();
        var controller = new FoodAiController(Substitute.For<IFoodAiClient>(), repository, Substitute.For<IMediator>(),
            NullLogger<FoodAiController>.Instance, patientScope: scope, currentContext: context)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.ToString()), new Claim("aud", "erp")], "test")) } } };
        var result = Assert.IsType<OkObjectResult>(await controller.GetAnalysis(id, CancellationToken.None));
        var json = JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("imageKey").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("foods")[0].GetProperty("maskKey").ValueKind);
        await scope.Received(1).EnsureResourceAsync(PatientResource.FoodAnalysis, id, "Nutrition.RecordsView", CancellationToken.None);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task History_InternalCommentAndSnapshot_RequireNotesPermission(bool notes)
    {
        var patient = Guid.NewGuid(); var id = Guid.NewGuid();
        var repository = Substitute.For<INutritionPrescriptionRepository>();
        repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(new NutritionPrescription { Id = id, PatientId = patient });
        var store = Substitute.For<INutritionCaseStore>();
        store.CanAccessAsync(patient, Arg.Any<NutritionActor>(), Arg.Any<CancellationToken>()).Returns(true);
        store.GetContextAsync(patient, Arg.Any<CancellationToken>()).Returns(new NutritionCaseContext(null, null, "UTC", false, null));
        store.HistoryAsync(id, Arg.Any<CancellationToken>()).Returns(new[] { new NutritionPrescriptionEvent { Comment = "Nota interna", SnapshotJson = "{\"interno\":true}" } });
        var cases = new NutritionCases(repository, store, Substitute.For<INutritionDraftClient>(),
            Substitute.For<IClinicalContextService>(), Substitute.For<IProgramAssignmentRepository>(), TimeProvider.System);
        var actor = new NutritionActor(Guid.NewGuid(), Guid.NewGuid(), false, false, false, CanView: true, CanViewNotes: notes);
        var row = Assert.Single(await cases.HistoryAsync(new(patient, id, actor), CancellationToken.None));
        Assert.Equal(notes ? "Nota interna" : null, row.Comment);
        Assert.Equal(notes ? "{\"interno\":true}" : "{}", row.SnapshotJson);
        await Assert.ThrowsAsync<ForbiddenException>(() => cases.ExecuteAsync(new(NutritionOperation.Save, patient, id, 1, actor), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => cases.ExecuteAsync(new(NutritionOperation.Publish, patient, id, 1, actor), CancellationToken.None));
    }
}
