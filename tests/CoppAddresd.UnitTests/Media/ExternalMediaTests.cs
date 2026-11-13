using System.Text.Json;
using CoppAddresd.Application.Features.Media;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Media;

public sealed class ExternalMediaTests
{
    [Theory]
    [InlineData("youtube", "https://www.youtube.com/watch?v=dQw4w9WgXcQ", true)]
    [InlineData("youtube", "https://youtu.be/dQw4w9WgXcQ", true)]
    [InlineData("youtube", "https://www.youtube.com/embed/dQw4w9WgXcQ", true)]
    [InlineData("youtube", "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", true)]
    [InlineData("youtube", "https://www.youtube.com/shorts/dQw4w9WgXcQ", true)]
    [InlineData("vimeo", "https://vimeo.com/123456789", true)]
    [InlineData("vimeo", "https://player.vimeo.com/video/123456789", true)]
    [InlineData("vimeo", "https://www.youtube.com/watch?v=dQw4w9WgXcQ", false)]
    [InlineData("youtube", "https://vimeo.com/123456789", false)]
    [InlineData("other", "https://vimeo.com/123456789", false)]
    [InlineData("youtube", "http://youtu.be/dQw4w9WgXcQ", false)]
    [InlineData("youtube", "https://youtube.com.ejemplo.test/watch?v=dQw4w9WgXcQ", false)]
    [InlineData("youtube", "https://youtube.com@ejemplo.test/watch?v=dQw4w9WgXcQ", false)]
    [InlineData("youtube", "https://usuario@youtube.com/watch?v=dQw4w9WgXcQ", false)]
    [InlineData("youtube", "https://www.youtube.com:444/watch?v=dQw4w9WgXcQ", false)]
    [InlineData("youtube", "https://www.youtube.com/watch?v=dQw4w9WgXcQ&v=otro", false)]
    [InlineData("youtube", "https://www.youtube.com/playlist?list=123", false)]
    [InlineData("youtube", "https://youtu.be/", false)]
    [InlineData("vimeo", "https://vimeo.com/channels/123456789", false)]
    [InlineData("vimeo", "javascript:alert(1)", false)]
    [InlineData("youtube", null, false)]
    public void URLPorProveedor_ValidaCreateYUpdate(string provider, string? url, bool valid)
    {
        var command = Create(provider, url);
        Assert.Equal(valid, new CreateMediaItemCommandValidator().Validate(command).IsValid);
        var update = new UpdateMediaItemCommand(Guid.NewGuid(), "Video", null, "Equipo", MediaType.Video,
            MediaCategory.Biologia, null, null, null, null, null, MediaStatus.Draft, 0, 0, 0,
            SourceKind: "external", Provider: provider, ExternalUrl: url);
        Assert.Equal(valid, new UpdateMediaItemCommandValidator().Validate(update).IsValid);
    }

    private static CreateMediaItemCommand Create(string provider, string? url) =>
        new("Video", null, "Equipo", MediaType.Video, MediaCategory.Biologia, null, null, null,
            null, null, MediaStatus.Draft, 0, 0, 0, SourceKind: "external", Provider: provider, ExternalUrl: url);

    [Fact]
    public async Task Externo_ConservaContratoPublicaYBorraSinLlamarAlStorage()
    {
        var repository = Substitute.For<IMediaItemRepository>();
        var storage = Substitute.For<IObjectStorageService>();
        MediaItem? saved = null;
        repository.AddAsync(Arg.Any<MediaItem>(), Arg.Any<CancellationToken>()).Returns(call => { saved = call.Arg<MediaItem>(); return saved; });
        var created = await new CreateMediaItemCommandHandler(repository, NullLogger<CreateMediaItemCommandHandler>.Instance)
            .Handle(Create("youtube", "https://youtu.be/dQw4w9WgXcQ") with { Tags = [" Movilidad ", "movilidad"], ThumbnailKey = "https://images.example.test/cover.jpg" }, CancellationToken.None);
        Assert.NotNull(saved); Assert.Equal("external/youtube/dQw4w9WgXcQ", saved.StorageKey);
        Assert.Equal(["movilidad"], saved.Tags);
        var wire = JsonSerializer.SerializeToElement(created, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("external", wire.GetProperty("source_kind").GetString());
        Assert.Equal("youtube", wire.GetProperty("provider").GetString());
        Assert.Equal("https://youtu.be/dQw4w9WgXcQ", wire.GetProperty("external_url").GetString());
        Assert.Equal(saved.ThumbnailKey, wire.GetProperty("thumbnailUrl").GetString());
        repository.GetByIdAsync(saved.Id, Arg.Any<CancellationToken>()).Returns(saved);
        var publisher = new PublishMediaItemCommandHandler(repository, storage, NullLogger<PublishMediaItemCommandHandler>.Instance);
        var publication = await publisher.Handle(new(saved.Id), CancellationToken.None);
        Assert.True(publication.Validation.ExternalUrlVerified); Assert.False(publication.Validation.StorageKeyVerified);
        Assert.Equal(publication.PublishedAt, (await publisher.Handle(new(saved.Id), CancellationToken.None)).PublishedAt);
        await new DeleteMediaItemCommandHandler(repository, storage, NullLogger<DeleteMediaItemCommandHandler>.Instance).Handle(new(saved.Id), CancellationToken.None);
        Assert.Empty(storage.ReceivedCalls());
    }

    [Fact]
    public void ContratoDeEntrada_AceptaCamposExternosSinStorage()
    {
        var request = JsonSerializer.Deserialize<CreateMediaItemRequest>("""{"title":"Video","author":"Equipo","mediaType":1,"source_kind":"external","provider":"vimeo","external_url":"https://vimeo.com/123456789","thumbnailUrl":"https://images.example.test/a.jpg","durationSeconds":45}""", new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(request); Assert.Equal("external", request.SourceKind); Assert.Null(request.StorageKey);
        Assert.Equal(45, request.DurationSeconds); Assert.Equal("vimeo", request.Provider);
        Assert.NotNull(ExternalMediaRules.ResolveThumbnail(null, request.ThumbnailUrl));
    }

    [Fact]
    public void OrigenEtiquetasYTipos_InvalidosSeRechazan()
    {
        var validator = new CreateMediaItemCommandValidator();
        var valid = Create("vimeo", "https://vimeo.com/123456789");
        Assert.False(validator.Validate(valid with { SourceKind = "otro" }).IsValid);
        Assert.False(validator.Validate(valid with { MediaType = MediaType.Audio }).IsValid);
        Assert.False(validator.Validate(valid with { Tags = [""] }).IsValid);
        Assert.False(validator.Validate(valid with { Tags = [new string('a', 65)] }).IsValid);
        Assert.False(validator.Validate(valid with { Tags = Enumerable.Repeat("a", 33).ToArray() }).IsValid);
        Assert.False(validator.Validate(valid with { SourceKind = "file", StorageKey = "media/a.mp4" }).IsValid);
    }
}
