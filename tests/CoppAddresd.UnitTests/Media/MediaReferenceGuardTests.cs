using CoppAddresd.Application.DTOs.Storage;
using CoppAddresd.Application.Features.Media;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Media;

/// <summary>
/// Integridad referencial y ciclo de vida de blobs (change
/// erp-program-content-admin, Fase 3): guardia 409 de eliminación (REQ-PCA-07),
/// borrado físico de blobs y recolector de huérfanos con ventana de retención
/// (REQ-PCA-08). Handlers con dobles del repositorio y del storage.
/// </summary>
public sealed class MediaReferenceGuardTests
{
    private readonly IMediaItemRepository _repository = Substitute.For<IMediaItemRepository>();
    private readonly IObjectStorageService _storage = Substitute.For<IObjectStorageService>();

    private static MediaItem Medium(
        string storageKey = "media/podcasts/xa.mp3",
        string? thumbnailKey = "media/thumbnails/xa.jpg",
        string title = "Lección A"
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Author = "Autor",
            MediaType = MediaType.Podcast,
            Category = MediaCategory.Biologia,
            StorageKey = storageKey,
            ThumbnailKey = thumbnailKey,
            ContentType = "audio/mpeg",
            FileSizeBytes = 1024,
            DurationSecs = 120,
            Status = MediaStatus.Draft,
            SortOrder = 1,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private DeleteMediaItemCommandHandler CreateDeleteHandler() =>
        new(_repository, _storage, NullLogger<DeleteMediaItemCommandHandler>.Instance);

    // ===================== Guardia de integridad (409) =====================

    [Fact]
    public async Task Delete_MedioConReferencias_BloqueaYNoTocaStorageNiFila()
    {
        var medium = Medium();
        _repository.GetByIdAsync(medium.Id).Returns(medium);
        _repository
            .GetReferencesAsync(medium.Id)
            .Returns(
                new MediaReferencesDto(
                    medium.Id,
                    TotalReferences: 2,
                    TemplateReferences:
                    [
                        new MediaTemplateReferenceDto(Guid.NewGuid(), "Programa 83 Días", 1, 80),
                    ],
                    EnrollmentReferences:
                    [
                        new MediaEnrollmentReferenceDto(
                            Guid.NewGuid(),
                            "Carlos Mendoza",
                            4,
                            1,
                            IsFrozen: false
                        ),
                    ]
                )
            );

        var result = await CreateDeleteHandler()
            .Handle(new DeleteMediaItemCommand(medium.Id), default);

        Assert.True(result.Blocked);
        Assert.False(result.NotFound);
        Assert.NotNull(result.BlockingReferences);
        Assert.Equal(2, result.BlockingReferences!.TotalReferences);
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<MediaItem>());
        await _storage.DidNotReceive().DeleteObjectsAsync(Arg.Any<IEnumerable<string>>());
    }

    [Fact]
    public async Task Delete_MedioSinReferencias_EliminaFilaYBlobs()
    {
        var medium = Medium();
        _repository.GetByIdAsync(medium.Id).Returns(medium);
        _repository
            .GetReferencesAsync(medium.Id)
            .Returns(new MediaReferencesDto(medium.Id, 0, [], []));

        var result = await CreateDeleteHandler()
            .Handle(new DeleteMediaItemCommand(medium.Id), default);

        Assert.False(result.Blocked);
        Assert.False(result.NotFound);
        await _repository.Received(1).DeleteAsync(medium);
        await _storage
            .Received(1)
            .DeleteObjectsAsync(
                Arg.Is<IEnumerable<string>>(keys =>
                    keys.Contains("media/podcasts/xa.mp3")
                    && keys.Contains("media/thumbnails/xa.jpg")
                )
            );
    }

    [Fact]
    public async Task Delete_MedioSinPortada_SoloBorraElStorageKey()
    {
        var medium = Medium(thumbnailKey: null);
        _repository.GetByIdAsync(medium.Id).Returns(medium);
        _repository
            .GetReferencesAsync(medium.Id)
            .Returns(new MediaReferencesDto(medium.Id, 0, [], []));

        await CreateDeleteHandler().Handle(new DeleteMediaItemCommand(medium.Id), default);

        await _storage
            .Received(1)
            .DeleteObjectsAsync(
                Arg.Is<IEnumerable<string>>(keys =>
                    keys.Contains("media/podcasts/xa.mp3")
                    && !keys.Any(k => k.Contains("thumbnails"))
                )
            );
    }

    [Fact]
    public async Task Delete_MedioInexistente_ReturnsNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns((MediaItem?)null);

        var result = await CreateDeleteHandler()
            .Handle(new DeleteMediaItemCommand(Guid.NewGuid()), default);

        Assert.True(result.NotFound);
        await _storage.DidNotReceive().DeleteObjectsAsync(Arg.Any<IEnumerable<string>>());
    }

    // ===================== GetMediaReferencesQuery =====================

    [Fact]
    public async Task GetReferences_DelegaEnElRepositorio()
    {
        var mediaId = Guid.NewGuid();
        var dto = new MediaReferencesDto(
            mediaId,
            1,
            [new MediaTemplateReferenceDto(Guid.NewGuid(), "Plantilla X", 3, 50)],
            []
        );
        _repository.GetReferencesAsync(mediaId).Returns(dto);

        var handler = new GetMediaReferencesQueryHandler(_repository);
        var result = await handler.Handle(new GetMediaReferencesQuery(mediaId), default);

        Assert.Same(dto, result);
    }

    [Fact]
    public async Task GetReferences_MedioInexistente_DevuelveNull()
    {
        _repository.GetReferencesAsync(Arg.Any<Guid>()).Returns((MediaReferencesDto?)null);

        var handler = new GetMediaReferencesQueryHandler(_repository);
        var result = await handler.Handle(new GetMediaReferencesQuery(Guid.NewGuid()), default);

        Assert.Null(result);
    }

    // ===================== Recolector de huérfanos (GC) =====================

    private static ObjectMetadata Obj(string key, DateTimeOffset lastModified, long size = 1024) =>
        new(
            Key: key,
            Size: size,
            ETag: null,
            ContentType: "audio/mpeg",
            LastModified: lastModified
        );

    private void SetupStorage(params ObjectMetadata[] objects) =>
        _storage
            .ListObjectsAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ListObjectsResult(objects, null));

    [Fact]
    public async Task Cleanup_DryRun_ReportaSinEliminar()
    {
        var old = Obj("media/podcasts/huerfano-viejo.mp3", DateTimeOffset.UtcNow.AddDays(-10));
        SetupStorage(old);

        var handler = new CleanupOrphanedBlobsCommandHandler(
            _repository,
            _storage,
            NullLogger<CleanupOrphanedBlobsCommandHandler>.Instance
        );

        var result = await handler.Handle(new CleanupOrphanedBlobsCommand(DryRun: true), default);

        Assert.Equal(1, result.OrphanedObjects);
        Assert.Equal(0, result.PurgedObjects);
        Assert.Equal(0, result.BytesFreed);
        Assert.Contains("media/podcasts/huerfano-viejo.mp3", result.OrphanedKeys);
        await _storage.DidNotReceive().DeleteObjectsAsync(Arg.Any<IEnumerable<string>>());
    }

    [Fact]
    public async Task Cleanup_Purga_SoloHuerosViejos_PreservaReferenciadosYRecientes()
    {
        var mediaId = Guid.NewGuid();
        var medium = Medium(
            storageKey: "media/podcasts/referenciado.mp3",
            thumbnailKey: "media/thumbnails/referenciado.jpg"
        );
        medium.Id = mediaId;
        _repository
            .GetAllStorageKeysAsync()
            .Returns(
                new HashSet<string>(StringComparer.Ordinal)
                {
                    medium.StorageKey,
                    medium.ThumbnailKey!,
                }
            );

        var now = DateTimeOffset.UtcNow;
        var orfanViejo = Obj("media/podcasts/huerfano-viejo.mp3", now.AddDays(-30), size: 2048);
        var orfanOtroViejo = Obj("media/podcasts/huerfano-viejo-2.mp3", now.AddDays(-8), size: 512);
        var uploadReciente = Obj("media/podcasts/subido-hace-2-horas.mp3", now.AddHours(-2));
        var referenciadoViejo = Obj("media/podcasts/referenciado.mp3", now.AddDays(-100));
        SetupStorage(orfanViejo, orfanOtroViejo, uploadReciente, referenciadoViejo);

        var handler = new CleanupOrphanedBlobsCommandHandler(
            _repository,
            _storage,
            NullLogger<CleanupOrphanedBlobsCommandHandler>.Instance
        );

        var result = await handler.Handle(
            new CleanupOrphanedBlobsCommand(DryRun: false, RetentionDays: 7),
            default
        );

        // Huérfanos: los 2 viejos; el upload de 2 horas está dentro de la
        // ventana de retención (7 días) y el referenciado nunca es huérfano.
        Assert.Equal(2, result.OrphanedObjects);
        Assert.Equal(2, result.PurgedObjects);
        Assert.Equal(2048 + 512, result.BytesFreed);
        await _storage
            .Received(1)
            .DeleteObjectsAsync(
                Arg.Is<IEnumerable<string>>(keys =>
                    keys.Contains("media/podcasts/huerfano-viejo.mp3")
                    && keys.Contains("media/podcasts/huerfano-viejo-2.mp3")
                    && !keys.Contains("media/podcasts/subido-hace-2-horas.mp3")
                    && !keys.Contains("media/podcasts/referenciado.mp3")
                )
            );
    }

    [Fact]
    public async Task Cleanup_SinHueros_ResultaVacioYNoElimina()
    {
        _repository.GetAllStorageKeysAsync().Returns(new HashSet<string>(StringComparer.Ordinal));
        SetupStorage(Array.Empty<ObjectMetadata>());

        var handler = new CleanupOrphanedBlobsCommandHandler(
            _repository,
            _storage,
            NullLogger<CleanupOrphanedBlobsCommandHandler>.Instance
        );

        var result = await handler.Handle(new CleanupOrphanedBlobsCommand(DryRun: false), default);

        Assert.Equal(0, result.OrphanedObjects);
        Assert.Equal(0, result.PurgedObjects);
        await _storage.DidNotReceive().DeleteObjectsAsync(Arg.Any<IEnumerable<string>>());
    }

    [Fact]
    public async Task Cleanup_RetentionDaysFueraDeRango_LanzaValidacion()
    {
        var handler = new CleanupOrphanedBlobsCommandHandler(
            _repository,
            _storage,
            NullLogger<CleanupOrphanedBlobsCommandHandler>.Instance
        );
        var validator = new CleanupOrphanedBlobsCommandValidator();

        Assert.False(
            (
                await validator.ValidateAsync(new CleanupOrphanedBlobsCommand(RetentionDays: 0))
            ).IsValid
        );
        Assert.False(
            (
                await validator.ValidateAsync(new CleanupOrphanedBlobsCommand(RetentionDays: 400))
            ).IsValid
        );
        Assert.True((await validator.ValidateAsync(new CleanupOrphanedBlobsCommand())).IsValid);
    }
}
