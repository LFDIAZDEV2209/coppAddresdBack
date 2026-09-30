using CoppAddresd.Application.DTOs.Storage;
using CoppAddresd.Application.Features.Media;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Media;

/// <summary>
/// Ciclo de vida de medios (change erp-program-content-admin, Fase 2):
/// validación técnica estricta de publicación (REQ-PCA-02), transiciones
/// semánticas auditables (REQ-PCA-03) y consulta paginada server-side
/// (REQ-PCA-06). Handlers probados con dobles de IMediaItemRepository e
/// IObjectStorageService (los fakes son el patrón aceptado por la skill de
/// testing; la traducción SQL del repositorio se cubre en integración).
/// </summary>
public sealed class MediaLifecycleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly IMediaItemRepository _repository = Substitute.For<IMediaItemRepository>();
    private readonly IObjectStorageService _storage = Substitute.For<IObjectStorageService>();

    private static MediaItem DraftMedium(
        MediaType mediaType = MediaType.Podcast,
        string contentType = "audio/mpeg",
        long fileSize = 15_485_760,
        int duration = 930,
        string? thumbnailKey = null,
        MediaStatus status = MediaStatus.Draft,
        DateTimeOffset? publishedAt = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = "Introducción al Ritmo Circadiano",
            Author = "Dra. Elena Ramos",
            MediaType = mediaType,
            Category = MediaCategory.Biologia,
            StorageKey = "media/podcasts/circadiano.mp3",
            ThumbnailKey = thumbnailKey,
            ContentType = contentType,
            FileSizeBytes = fileSize,
            DurationSecs = duration,
            Status = status,
            SortOrder = 1,
            Day = 1,
            Month = 1,
            PublishedAt = publishedAt,
            CreatedAt = Now,
        };

    private PublishMediaItemCommandHandler CreatePublishHandler() =>
        new(_repository, _storage, NullLogger<PublishMediaItemCommandHandler>.Instance);

    private static ObjectMetadata Head(
        string key,
        long size = 15_485_760,
        string? contentType = "audio/mpeg"
    ) =>
        new(
            Key: key,
            Size: size,
            ETag: null,
            ContentType: contentType,
            LastModified: DateTimeOffset.UtcNow
        );

    // ===================== Publish: validación estricta (422) =====================

    [Fact]
    public async Task Publish_ArchivoInexistenteEnStorage_LanzaUnprocessableEntity()
    {
        var medium = DraftMedium();
        _repository.GetByIdAsync(medium.Id).Returns(medium);
        _storage.HeadObjectAsync(medium.StorageKey).Returns((ObjectMetadata?)null);

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            CreatePublishHandler().Handle(new PublishMediaItemCommand(medium.Id), default)
        );

        // El medio permanece Draft: nunca se persiste el cambio de estado.
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<MediaItem>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Publish_DuracionInvalida_LanzaUnprocessableEntity(int durationSecs)
    {
        var medium = DraftMedium(duration: durationSecs);
        _repository.GetByIdAsync(medium.Id).Returns(medium);
        _storage.HeadObjectAsync(medium.StorageKey).Returns(Head(medium.StorageKey));

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            CreatePublishHandler().Handle(new PublishMediaItemCommand(medium.Id), default)
        );
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<MediaItem>());
    }

    [Fact]
    public async Task Publish_DuracionNula_LanzaUnprocessableEntity()
    {
        var medium = DraftMedium(duration: 0);
        medium.DurationSecs = null;
        _repository.GetByIdAsync(medium.Id).Returns(medium);
        _storage.HeadObjectAsync(medium.StorageKey).Returns(Head(medium.StorageKey));

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            CreatePublishHandler().Handle(new PublishMediaItemCommand(medium.Id), default)
        );
    }

    [Fact]
    public async Task Publish_ContentTypeIncompatibleConPodcast_LanzaUnprocessableEntity()
    {
        var medium = DraftMedium(contentType: "application/pdf");
        _repository.GetByIdAsync(medium.Id).Returns(medium);
        _storage.HeadObjectAsync(medium.StorageKey).Returns(Head(medium.StorageKey));

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            CreatePublishHandler().Handle(new PublishMediaItemCommand(medium.Id), default)
        );
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<MediaItem>());
    }

    [Fact]
    public async Task Publish_VideoConContentTypeDeAudio_LanzaUnprocessableEntity()
    {
        var medium = DraftMedium(mediaType: MediaType.Video, contentType: "audio/mpeg");
        _repository.GetByIdAsync(medium.Id).Returns(medium);
        _storage.HeadObjectAsync(medium.StorageKey).Returns(Head(medium.StorageKey));

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            CreatePublishHandler().Handle(new PublishMediaItemCommand(medium.Id), default)
        );
    }

    [Fact]
    public async Task Publish_TamanoCero_LanzaUnprocessableEntity()
    {
        var medium = DraftMedium(fileSize: 0);
        _repository.GetByIdAsync(medium.Id).Returns(medium);
        _storage.HeadObjectAsync(medium.StorageKey).Returns(Head(medium.StorageKey));

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            CreatePublishHandler().Handle(new PublishMediaItemCommand(medium.Id), default)
        );
    }

    [Fact]
    public async Task Publish_AudioQueExcede250MB_LanzaUnprocessableEntity()
    {
        var medium = DraftMedium(fileSize: MediaPublishRules.MaxAudioBytes + 1);
        _repository.GetByIdAsync(medium.Id).Returns(medium);
        _storage.HeadObjectAsync(medium.StorageKey).Returns(Head(medium.StorageKey));

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            CreatePublishHandler().Handle(new PublishMediaItemCommand(medium.Id), default)
        );
    }

    [Fact]
    public async Task Publish_VideoDentroDe1GB_SePublica()
    {
        var medium = DraftMedium(
            mediaType: MediaType.Video,
            contentType: "video/mp4",
            fileSize: 900L * 1024 * 1024
        );
        _repository.GetByIdAsync(medium.Id).Returns(medium);
        _storage
            .HeadObjectAsync(medium.StorageKey)
            .Returns(Head(medium.StorageKey, contentType: "video/mp4"));

        var result = await CreatePublishHandler()
            .Handle(new PublishMediaItemCommand(medium.Id), default);

        Assert.Equal(MediaStatus.Published, result.Status);
        Assert.NotNull(result.PublishedAt);
    }

    [Fact]
    public async Task Publish_PortadaInexistente_LanzaUnprocessableEntity()
    {
        var medium = DraftMedium(thumbnailKey: "media/thumbnails/circadiano.jpg");
        _repository.GetByIdAsync(medium.Id).Returns(medium);
        _storage.HeadObjectAsync(medium.StorageKey).Returns(Head(medium.StorageKey));
        _storage.HeadObjectAsync(medium.ThumbnailKey!).Returns((ObjectMetadata?)null);

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            CreatePublishHandler().Handle(new PublishMediaItemCommand(medium.Id), default)
        );
    }

    [Fact]
    public async Task Publish_PortadaNoImagen_LanzaUnprocessableEntity()
    {
        var medium = DraftMedium(thumbnailKey: "media/thumbnails/circadiano.mp3");
        _repository.GetByIdAsync(medium.Id).Returns(medium);
        _storage.HeadObjectAsync(medium.StorageKey).Returns(Head(medium.StorageKey));
        _storage
            .HeadObjectAsync(medium.ThumbnailKey!)
            .Returns(Head(medium.ThumbnailKey!, contentType: "audio/mpeg"));

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            CreatePublishHandler().Handle(new PublishMediaItemCommand(medium.Id), default)
        );
    }

    // ===================== Publish: éxito =====================

    [Fact]
    public async Task Publish_BlobExistenteYMetadataConforme_TransicionaAPublished()
    {
        var medium = DraftMedium(thumbnailKey: "media/thumbnails/circadiano.jpg");
        _repository.GetByIdAsync(medium.Id).Returns(medium);
        _storage.HeadObjectAsync(medium.StorageKey).Returns(Head(medium.StorageKey));
        _storage
            .HeadObjectAsync(medium.ThumbnailKey!)
            .Returns(Head(medium.ThumbnailKey!, contentType: "image/jpeg"));

        var result = await CreatePublishHandler()
            .Handle(new PublishMediaItemCommand(medium.Id), default);

        Assert.Equal(MediaStatus.Published, result.Status);
        Assert.NotNull(result.PublishedAt);
        Assert.True(result.Validation.StorageKeyVerified);
        Assert.True(result.Validation.ThumbnailVerified);
        Assert.Equal(medium.FileSizeBytes, result.Validation.FileSizeBytes);
        Assert.Equal(medium.DurationSecs, result.Validation.DurationSecs);
        await _repository
            .Received(1)
            .UpdateAsync(
                Arg.Is<MediaItem>(m =>
                    m.Id == medium.Id && m.Status == MediaStatus.Published && m.PublishedAt != null
                )
            );
    }

    [Fact]
    public async Task Publish_YaPublicado_ConservaPublishedAtOriginal()
    {
        var original = DateTimeOffset.UtcNow.AddDays(-3);
        var medium = DraftMedium(status: MediaStatus.Published, publishedAt: original);
        _repository.GetByIdAsync(medium.Id).Returns(medium);
        _storage.HeadObjectAsync(medium.StorageKey).Returns(Head(medium.StorageKey));

        var result = await CreatePublishHandler()
            .Handle(new PublishMediaItemCommand(medium.Id), default);

        Assert.Equal(original, result.PublishedAt);
    }

    [Fact]
    public async Task Publish_MedioInexistente_LanzaNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns((MediaItem?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreatePublishHandler().Handle(new PublishMediaItemCommand(Guid.NewGuid()), default)
        );
    }

    // ===================== Unpublish / Archive =====================

    [Fact]
    public async Task Unpublish_MedioPublished_PasaADraft()
    {
        var medium = DraftMedium(status: MediaStatus.Published, publishedAt: Now);
        _repository.GetByIdAsync(medium.Id).Returns(medium);

        var handler = new UnpublishMediaItemCommandHandler(
            _repository,
            NullLogger<UnpublishMediaItemCommandHandler>.Instance
        );
        var result = await handler.Handle(new UnpublishMediaItemCommand(medium.Id), default);

        Assert.Equal(MediaStatus.Draft, result.Status);
        // Conserva el histórico de publicación (no se destruye el dato).
        Assert.NotNull(result.PublishedAt);
        await _repository
            .Received(1)
            .UpdateAsync(Arg.Is<MediaItem>(m => m.Status == MediaStatus.Draft));
    }

    [Fact]
    public async Task Unpublish_MedioYaDraft_IdempotenteSinEscritura()
    {
        var medium = DraftMedium(status: MediaStatus.Draft);
        _repository.GetByIdAsync(medium.Id).Returns(medium);

        var handler = new UnpublishMediaItemCommandHandler(
            _repository,
            NullLogger<UnpublishMediaItemCommandHandler>.Instance
        );
        var result = await handler.Handle(new UnpublishMediaItemCommand(medium.Id), default);

        Assert.Equal(MediaStatus.Draft, result.Status);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<MediaItem>());
    }

    [Fact]
    public async Task Unpublish_MedioArchivado_LanzaUnprocessableEntity()
    {
        var medium = DraftMedium(status: MediaStatus.Archived);
        _repository.GetByIdAsync(medium.Id).Returns(medium);

        var handler = new UnpublishMediaItemCommandHandler(
            _repository,
            NullLogger<UnpublishMediaItemCommandHandler>.Instance
        );

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            handler.Handle(new UnpublishMediaItemCommand(medium.Id), default)
        );
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<MediaItem>());
    }

    [Fact]
    public async Task Archive_MedioPublished_PasaAArchived()
    {
        var medium = DraftMedium(status: MediaStatus.Published, publishedAt: Now);
        _repository.GetByIdAsync(medium.Id).Returns(medium);

        var handler = new ArchiveMediaItemCommandHandler(
            _repository,
            NullLogger<ArchiveMediaItemCommandHandler>.Instance
        );
        var result = await handler.Handle(new ArchiveMediaItemCommand(medium.Id), default);

        Assert.Equal(MediaStatus.Archived, result.Status);
        await _repository
            .Received(1)
            .UpdateAsync(Arg.Is<MediaItem>(m => m.Status == MediaStatus.Archived));
    }

    [Fact]
    public async Task Archive_MedioYaArchivado_IdempotenteSinEscritura()
    {
        var medium = DraftMedium(status: MediaStatus.Archived);
        _repository.GetByIdAsync(medium.Id).Returns(medium);

        var handler = new ArchiveMediaItemCommandHandler(
            _repository,
            NullLogger<ArchiveMediaItemCommandHandler>.Instance
        );
        var result = await handler.Handle(new ArchiveMediaItemCommand(medium.Id), default);

        Assert.Equal(MediaStatus.Archived, result.Status);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<MediaItem>());
    }

    [Fact]
    public async Task Archive_MedioInexistente_LanzaNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns((MediaItem?)null);

        var handler = new ArchiveMediaItemCommandHandler(
            _repository,
            NullLogger<ArchiveMediaItemCommandHandler>.Instance
        );

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ArchiveMediaItemCommand(Guid.NewGuid()), default)
        );
    }

    // ===================== Reorder =====================

    [Fact]
    public async Task Reorder_DelegaEnRepositorioYDevuelveActualizados()
    {
        _repository
            .ReorderAsync(Arg.Any<IReadOnlyList<ReorderMediaItemEntry>>())
            .Returns(callInfo => callInfo.ArgAt<IReadOnlyList<ReorderMediaItemEntry>>(0).Count);

        var handler = new ReorderMediaItemsCommandHandler(
            _repository,
            NullLogger<ReorderMediaItemsCommandHandler>.Instance
        );
        var entries = new List<ReorderMediaItemEntry>
        {
            new(Guid.NewGuid(), 3),
            new(Guid.NewGuid(), 1),
        };

        var result = await handler.Handle(new ReorderMediaItemsCommand(entries), default);

        Assert.Equal(2, result.UpdatedCount);
        await _repository.Received(1).ReorderAsync(entries);
    }

    // ===================== GetMediaItemsQuery (paginación server-side) =====================

    private sealed class FakeMediaItemRepository : IMediaItemRepository
    {
        public List<MediaItem> Items { get; } = [];
        public MediaItemsPageRequest? LastRequest { get; private set; }

        public Task<MediaItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.Id == id));

        public Task<MediaItem?> GetByStorageKeyAsync(
            string storageKey,
            CancellationToken ct = default
        ) => Task.FromResult(Items.FirstOrDefault(i => i.StorageKey == storageKey));

        public Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MediaItem>>(Items);

        public Task<(IReadOnlyList<MediaItem> Items, int Total)> SearchPageAsync(
            MediaItemsPageRequest request,
            CancellationToken ct = default
        )
        {
            LastRequest = request;

            var query = Items.AsEnumerable();
            if (request.MediaType.HasValue)
            {
                query = query.Where(m => m.MediaType == request.MediaType.Value);
            }

            if (request.IncludedIds is not null)
            {
                query = query.Where(m => request.IncludedIds.Contains(m.Id));
            }

            if (request.ExcludedIds is not null)
            {
                query = query.Where(m => !request.ExcludedIds.Contains(m.Id));
            }

            var total = query.Count();
            var page = query
                .OrderBy(m => m.SortOrder)
                .ThenBy(m => m.CreatedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();
            return Task.FromResult(((IReadOnlyList<MediaItem>)page, total));
        }

        public Task<MediaUsageSnapshot> GetUsageSnapshotAsync(CancellationToken ct = default)
        {
            var counts = new Dictionary<Guid, int> { [Items[0].Id] = 3 };
            return Task.FromResult(new MediaUsageSnapshot(counts, new HashSet<Guid>(counts.Keys)));
        }

        public Task<int> ReorderAsync(
            IReadOnlyList<ReorderMediaItemEntry> entries,
            CancellationToken ct = default
        ) => Task.FromResult(0);

        public Task<MediaReferencesDto?> GetReferencesAsync(
            Guid mediaId,
            CancellationToken ct = default
        ) => Task.FromResult<MediaReferencesDto?>(null);

        public Task<IReadOnlyCollection<string>> GetAllStorageKeysAsync(
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyCollection<string>>([]);

        public Task<MediaItem> AddAsync(MediaItem item, CancellationToken ct = default)
        {
            Items.Add(item);
            return Task.FromResult(item);
        }

        public Task UpdateAsync(MediaItem item, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(MediaItem item, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private static FakeMediaItemRepository SeedCatalog()
    {
        var repo = new FakeMediaItemRepository();
        for (var i = 0; i < 25; i++)
        {
            repo.Items.Add(DraftMedium());
            repo.Items[i].Id = Guid.NewGuid();
            repo.Items[i].SortOrder = i;
            repo.Items[i].CreatedAt = Now.AddMinutes(-i);
        }
        return repo;
    }

    [Fact]
    public async Task Handle_EnvelopePaginado_CumpleElContrato()
    {
        var repo = SeedCatalog();
        var handler = new GetMediaItemsQueryHandler(repo);

        var result = await handler.Handle(new GetMediaItemsQuery(Page: 2, PageSize: 10), default);

        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(10, result.Items.Count);
    }

    [Fact]
    public async Task Handle_PaginaCeroSeNormalizaYPageSizeSeRecortaA100()
    {
        var repo = SeedCatalog();
        var handler = new GetMediaItemsQueryHandler(repo);

        var result = await handler.Handle(new GetMediaItemsQuery(Page: 0, PageSize: 500), default);

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task Handle_UsageAssigned_FiltraPorMediosReferenciados()
    {
        var repo = SeedCatalog();
        var handler = new GetMediaItemsQueryHandler(repo);

        await handler.Handle(new GetMediaItemsQuery(Usage: "assigned"), default);

        // El primer medio tiene 3 referencias (fake): assigned = includedIds con ese id.
        Assert.NotNull(repo.LastRequest?.IncludedIds);
        var included = repo.LastRequest!.IncludedIds!;
        Assert.Single(included);
        Assert.Equal(repo.Items[0].Id, included.First());
    }

    [Fact]
    public async Task Handle_UsageUnassigned_ExcluyeMediosReferenciados()
    {
        var repo = SeedCatalog();
        var handler = new GetMediaItemsQueryHandler(repo);

        await handler.Handle(new GetMediaItemsQuery(Usage: "unassigned"), default);

        Assert.NotNull(repo.LastRequest?.ExcludedIds);
        Assert.Single(repo.LastRequest!.ExcludedIds!);
    }

    [Fact]
    public async Task Handle_UsageDesconocido_NoFiltraPorUso()
    {
        var repo = SeedCatalog();
        var handler = new GetMediaItemsQueryHandler(repo);

        await handler.Handle(new GetMediaItemsQuery(Usage: "cualquier-cosa"), default);

        Assert.Null(repo.LastRequest!.IncludedIds);
        Assert.Null(repo.LastRequest!.ExcludedIds);
    }

    [Fact]
    public async Task Handle_UsageCount_DeLaEstatisticaDeUso()
    {
        var repo = SeedCatalog();
        var handler = new GetMediaItemsQueryHandler(repo);

        var result = await handler.Handle(new GetMediaItemsQuery(), default);

        Assert.Equal(3, result.Items.Single(i => i.Id == repo.Items[0].Id).UsageCount);
        Assert.Equal(0, result.Items.Single(i => i.Id == repo.Items[1].Id).UsageCount);
    }
}
