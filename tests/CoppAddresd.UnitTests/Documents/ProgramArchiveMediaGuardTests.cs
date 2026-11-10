using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetSnapshot;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums.ProgramProgress;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Documents;

public sealed class ProgramArchiveMediaGuardTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProgramMediaReference_CannotMintCapabilityForLabOriginalOrSidecar(bool cloud)
    {
        var enrollmentId = Guid.NewGuid(); var archive = $"documents/{Guid.NewGuid()}/{Guid.NewGuid()}";
        var actor = Substitute.For<IProgramActorContext>(); actor.ResolveActiveEnrollmentIdAsync(Arg.Any<CancellationToken>()).Returns(enrollmentId);
        var source = new TodayTaskContentDto(null, "Synthetic media alias", null, archive + "/extraction.json",
            MediaUrl: archive + "/original.pdf",
            Exercises: [new ExerciseItemDto("Synthetic", null, 1, 1, 180, 10, null, null, null, 0,
                Guid.NewGuid(), archive + "/original.pdf", archive + "/extraction.json", "Image")]);
        var snapshot = new ProgramSnapshotDto(enrollmentId,
            new ProgramSnapshotTemplateDto(Guid.NewGuid(), "synthetic", "Synthetic", 1, 1,
                ProgramWeekStatus.Active, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 15), 1, ["ejercicio"]),
            new DateOnly(2026, 11, 10),
            [new TodayTaskDto(TaskCode.ejercicio, "Synthetic", "Synthetic", 50, "pending", null, source)],
            0, false, 50, new XpInfoDto(0, "Synthetic", 100), new StreakInfoDto(0, 0, 0, 1m, null, 0), 7, []);
        var mediator = Substitute.For<IMediator>(); mediator.Send(Arg.Any<GetSnapshotQuery>(), Arg.Any<CancellationToken>()).Returns(snapshot);
        var storage = Substitute.For<IObjectStorageService>(); storage.IsCloudStorage.Returns(cloud);
        var controller = new ProgramController(mediator, NullLogger<ProgramController>.Instance, new ConfigurationBuilder().Build(), actor, storage);
        var result = await controller.GetMySnapshot(default);
        var payload = Assert.IsType<ProgramSnapshotDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var served = Assert.Single(payload.TodayTasks).Content!;
        Assert.Null(served.MediaUrl); Assert.Null(served.ThumbnailUrl);
        var exercise = Assert.Single(served.Exercises!); Assert.Null(exercise.MediaUrl); Assert.Null(exercise.ThumbnailUrl);
        Assert.Equal(archive + "/original.pdf", source.MediaUrl);
        await storage.DidNotReceiveWithAnyArgs().GetPreSignedUrlAsync(default!, default, default);
    }
}
