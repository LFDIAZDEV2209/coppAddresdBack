using CoppAddresd.Api.Context;
using CoppAddresd.Api.Controllers;
using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.DTOs.Storage;
using CoppAddresd.Application.Features.Media;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetSnapshot;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Media;

public sealed class ExerciseMediaTests
{
    [Theory]
    [InlineData("image/png", true)]
    [InlineData("image/svg+xml", false)]
    [InlineData("text/html", false)]
    [InlineData(null, false)]
    public async Task ImageCrud_ValidaMimeAntesDeGuardar(string? contentType, bool expected)
    {
        var create = new CreateMediaItemCommand("Técnica", null, "Equipo", MediaType.Image, MediaCategory.SaludFisica,
            "media/thumbnails/exercise.png", null, contentType, 1024, null, MediaStatus.Draft, 0, 0, 0);
        var update = new UpdateMediaItemCommand(Guid.NewGuid(), create.Title, null, create.Author, create.MediaType,
            create.Category, create.StorageKey, null, contentType, 1024, null, MediaStatus.Draft, 0, 0, 0);
        Assert.Equal(expected, (await new CreateMediaItemCommandValidator().ValidateAsync(create)).IsValid);
        Assert.Equal(expected, (await new UpdateMediaItemCommandValidator().ValidateAsync(update)).IsValid);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("image/gif")]
    public async Task Publish_ImagenRealNoExigeDuracion(string contentType)
    {
        var repository = Substitute.For<IMediaItemRepository>();
        var storage = Substitute.For<IObjectStorageService>();
        var medium = new MediaItem { Id = Guid.NewGuid(), Title = "Técnica prescrita", Author = "Equipo clínico",
            MediaType = MediaType.Image, StorageKey = "media/thumbnails/exercise.png", ContentType = contentType,
            FileSizeBytes = 1024, DurationSecs = null, Status = MediaStatus.Draft };
        repository.GetByIdAsync(medium.Id).Returns(medium);
        storage.HeadObjectAsync(medium.StorageKey).Returns(new ObjectMetadata(medium.StorageKey, 1024, null, contentType, DateTimeOffset.UtcNow));
        var handler = new PublishMediaItemCommandHandler(repository, storage, NullLogger<PublishMediaItemCommandHandler>.Instance);
        var result = await handler.Handle(new PublishMediaItemCommand(medium.Id), default);
        Assert.Equal(MediaStatus.Published, result.Status);
        Assert.Null(result.Validation.DurationSecs);
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("text/html")]
    [InlineData("video/mp4")]
    public void ImageRules_RechazanFormatosActivosOTipoIncorrecto(string contentType)
        => Assert.False(MediaPublishRules.IsAllowedContentType(MediaType.Image, contentType));

    [Fact]
    public async Task Publish_ImagenDemasiadoGrandeNoSePublica()
    {
        var repository = Substitute.For<IMediaItemRepository>();
        var storage = Substitute.For<IObjectStorageService>();
        var medium = new MediaItem { Id = Guid.NewGuid(), Title = "Técnica", MediaType = MediaType.Image,
            StorageKey = "media/thumbnails/exercise.png", ContentType = "image/png", FileSizeBytes = MediaPublishRules.MaxImageBytes + 1 };
        repository.GetByIdAsync(medium.Id).Returns(medium);
        storage.HeadObjectAsync(medium.StorageKey).Returns(new ObjectMetadata(medium.StorageKey, medium.FileSizeBytes.Value, null, "image/png", DateTimeOffset.UtcNow));
        var handler = new PublishMediaItemCommandHandler(repository, storage, NullLogger<PublishMediaItemCommandHandler>.Instance);
        await Assert.ThrowsAsync<UnprocessableEntityException>(() => handler.Handle(new PublishMediaItemCommand(medium.Id), default));
        await repository.DidNotReceive().UpdateAsync(Arg.Any<MediaItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MySnapshot_FirmaDemostracionesDelPacienteResueltoPorJwt()
    {
        var enrollmentId = Guid.NewGuid();
        var actor = Substitute.For<IProgramActorContext>();
        actor.ResolveActiveEnrollmentIdAsync(Arg.Any<CancellationToken>()).Returns(enrollmentId);
        var medium = new ExerciseItemDto("Técnica", null, 3, 10, 60, 30, "Piernas", null, null, 0,
            Guid.NewGuid(), "media/videos/exercise.mp4", "media/thumbnails/exercise.jpg", "Video", 2, "2-1-2", 4);
        var snapshot = new ProgramSnapshotDto(enrollmentId,
            new ProgramSnapshotTemplateDto(Guid.NewGuid(), "program", "Programa", 30, 1,
                ProgramWeekStatus.Active, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7), 1, ["ejercicio"]),
            new DateOnly(2026, 11, 3),
            [new TodayTaskDto(TaskCode.ejercicio, "Rutina", "Movimiento", 50, "pending", null,
                new TodayTaskContentDto(null, "Rutina", null, null, Exercises: [medium]))],
            0, false, 50, new XpInfoDto(0, "Inicial", 100), new StreakInfoDto(0, 0, 0, 1m, null, 0), 7, []);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Is<GetSnapshotQuery>(query => query.EnrollmentId == enrollmentId), Arg.Any<CancellationToken>()).Returns(snapshot);
        var storage = Substitute.For<IObjectStorageService>();
        storage.IsCloudStorage.Returns(true);
        storage.GetPreSignedUrlAsync(medium.MediaUrl!, TimeSpan.FromHours(1), Arg.Any<CancellationToken>()).Returns("https://signed.example/exercise.mp4?sig=temporary");
        storage.GetPreSignedUrlAsync(medium.ThumbnailUrl!, TimeSpan.FromHours(1), Arg.Any<CancellationToken>()).Returns("https://signed.example/exercise.jpg?sig=temporary");
        var controller = new ProgramController(mediator, NullLogger<ProgramController>.Instance, new ConfigurationBuilder().Build(), actor, storage);
        var result = await controller.GetMySnapshot(default);
        var payload = Assert.IsType<ProgramSnapshotDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var served = Assert.Single(Assert.Single(payload.TodayTasks).Content!.Exercises!);
        Assert.Equal("https://signed.example/exercise.mp4?sig=temporary", served.MediaUrl);
        Assert.Equal("https://signed.example/exercise.jpg?sig=temporary", served.ThumbnailUrl);
        Assert.Equal(medium.WeightKg, served.WeightKg);
        Assert.Equal(medium.MediaId, served.MediaId);
        // La resolución no altera el DTO ni reemplaza un vídeo con contenido demo.
        Assert.Equal("media/videos/exercise.mp4", medium.MediaUrl);
    }

    [Fact]
    public async Task MySnapshot_SinInscripcionNoLeeNingunaDemostracion()
    {
        var mediator = Substitute.For<IMediator>();
        var controller = new ProgramController(mediator, NullLogger<ProgramController>.Instance, new ConfigurationBuilder().Build(),
            Substitute.For<IProgramActorContext>(), Substitute.For<IObjectStorageService>());
        var result = await controller.GetMySnapshot(default);
        Assert.IsType<NotFoundObjectResult>(result.Result);
        await mediator.DidNotReceive().Send(Arg.Any<GetSnapshotQuery>(), Arg.Any<CancellationToken>());
    }
}
