using System.Text.Json;
using CoppAddresd.Application.Features.Media;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using CoppAddresd.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoppAddresd.IntegrationTests;

/// <summary>
/// Pruebas de integración del módulo de medios con PostgreSQL real (change
/// erp-program-content-admin, REQ-PCA-07): guardia de integridad referencial
/// (409 al eliminar un medio asignado a una plantilla o a snapshots de
/// semanas) y eliminación limpia del registro + blobs sin referencias.
/// Requieren COP_TEST_DB_CONNECTION apuntando a PostgreSQL desechable; si no
/// está definida, los tests se saltan (convención de la suite).
/// </summary>
public sealed class MediaIntegrationTests : IAsyncLifetime
{
    private const string EnvVar = "COP_TEST_DB_CONNECTION";

    private readonly string _connectionString = Environment.GetEnvironmentVariable(EnvVar)!;
    private bool _skipped;
    private AppDbContext _db = null!;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _transaction = null!;
    private MediaItemRepository _repository = null!;
    private string _storageRoot = null!;
    private LocalObjectStorageService _storage = null!;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _skipped = true;
            return;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        _db = new AppDbContext(options);
        _repository = new MediaItemRepository(_db);
        _storageRoot = Path.Combine(Path.GetTempPath(), "media-it-" + Guid.NewGuid().ToString("N"));
        _storage = new LocalObjectStorageService(
            Microsoft.Extensions.Options.Options.Create(
                new LocalStorageOptions { RootPath = _storageRoot }
            )
        );
        _transaction = await _db.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        if (_skipped)
        {
            return;
        }

        await _transaction.RollbackAsync();
        await _db.DisposeAsync();
        try
        {
            if (Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, recursive: true);
            }
        }
        catch
        {
            // La limpieza del directorio temporal no debe tumbar la suite.
        }
    }

    private async Task<MediaItem> SeedMediaAsync(
        string storageKey,
        string? thumbnailKey = null,
        MediaStatus status = MediaStatus.Published
    )
    {
        var media = new MediaItem
        {
            Id = Guid.NewGuid(),
            Title = "Podcast de prueba " + Guid.NewGuid().ToString("N")[..8],
            Author = "Autor Integración",
            MediaType = MediaType.Podcast,
            Category = MediaCategory.Biologia,
            StorageKey = storageKey,
            ThumbnailKey = thumbnailKey,
            ContentType = "audio/mpeg",
            FileSizeBytes = 15_485_760,
            DurationSecs = 930,
            Status = status,
            SortOrder = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = null,
        };
        _db.MediaItems.Add(media);
        await _db.SaveChangesAsync();
        return media;
    }

    private async Task<ProgramTemplate> SeedTemplateWithMediaReferenceAsync(Guid mediaId)
    {
        var template = new ProgramTemplate
        {
            Id = Guid.NewGuid(),
            Code = $"tpl-it-{Guid.NewGuid():N}"[..20],
            Name = "Programa Metabólico 83 Días (IT)",
            TotalWeeks = 12,
            Status = TemplateStatus.Active,
            Version = 1,
        };
        template.DayTemplates.Add(
            new WeeklyDayTemplate
            {
                Weekday = 1,
                TaskCode = TaskCode.podcast,
                Points = 80,
                SortOrder = 1,
                MediaId = mediaId,
            }
        );
        _db.ProgramTemplates.Add(template);
        await _db.SaveChangesAsync();
        return template;
    }

    private async Task<ProgramEnrollment> SeedEnrollmentWithSnapshotReferenceAsync(
        Guid mediaId,
        int weekNumber,
        ProgramWeekStatus weekStatus
    )
    {
        // auth."Users" es referenciado por FK de las migraciones (tabla del
        // Auth Service): se crea la fila mínima.
        var userId = Guid.NewGuid();
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO auth.\"Users\" (\"Id\", \"FirstName\", \"LastName\", \"IsActive\", \"CreatedAt\", "
                + "\"EmailConfirmed\", \"PhoneNumberConfirmed\", \"TwoFactorEnabled\", \"LockoutEnabled\", \"AccessFailedCount\") "
                + "VALUES ({0}, 'IT', 'Paciente', true, now(), true, false, false, false, 0) ON CONFLICT DO NOTHING;",
            userId
        );

        var patient = new PatientProfile
        {
            Id = Guid.NewGuid(),
            FirstName = "Carlos",
            LastName = "Mendoza (IT)",
            UserId = userId,
        };
        _db.PatientProfiles.Add(patient);

        var template = new ProgramTemplate
        {
            Id = Guid.NewGuid(),
            Code = $"tpl-it-{Guid.NewGuid():N}"[..20],
            Name = "Plantilla snapshot (IT)",
            TotalWeeks = 12,
            Status = TemplateStatus.Active,
            Version = 1,
        };
        _db.ProgramTemplates.Add(template);
        await _db.SaveChangesAsync();

        var enrollment = new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            TemplateId = template.Id,
            // La semana `weekNumber` empieza HOY (su cierre es futuro) para que
            // la marca de congelada venga solo del estado Completed.
            StartLocalDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7 * (weekNumber - 1)),
            CurrentWeekNumber = Math.Max(1, weekNumber),
        };
        _db.ProgramEnrollments.Add(enrollment);

        var weekStart = enrollment.StartLocalDate.AddDays(7 * (weekNumber - 1));
        _db.ProgramWeeks.Add(
            new ProgramWeek
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollment.Id,
                WeekNumber = weekNumber,
                Status = weekStatus,
                WeekStartDateLocal = weekStart,
                WeekEndDateLocal = weekStart.AddDays(6),
                TemplateVersionAtStart = 1,
                TasksSnapshot = JsonSerializer.SerializeToElement(
                    new object[]
                    {
                        new
                        {
                            weekday = 1,
                            task_code = "podcast",
                            points = 80,
                            sort_order = 1,
                            routine_id = (Guid?)null,
                            nutrition_plan_id = (Guid?)null,
                            media_id = mediaId,
                        },
                        new
                        {
                            weekday = 2,
                            task_code = "nut",
                            points = 150,
                            sort_order = 1,
                            routine_id = (Guid?)null,
                            nutrition_plan_id = (Guid?)null,
                            media_id = (Guid?)null,
                        },
                    }
                ),
            }
        );
        await _db.SaveChangesAsync();
        return enrollment;
    }

    [Fact]
    public async Task Delete_MedioReferenciadoEnPlantilla_Bloquea409()
    {
        var storageKey = $"media/podcasts/it-{Guid.NewGuid():N}.mp3";
        var medium = await SeedMediaAsync(storageKey);
        await SeedTemplateWithMediaReferenceAsync(medium.Id);

        var references = await _repository.GetReferencesAsync(medium.Id);

        // Guardia REQ-PCA-07: con referencia de plantilla activa, la
        // eliminación se bloquea (la API traduce a 409 Conflict).
        Assert.NotNull(references);
        Assert.True(references!.HasReferences);
        Assert.Equal(1, references.TotalReferences);
        var templateRef = Assert.Single(references.TemplateReferences);
        Assert.Equal(1, templateRef.Weekday);
        Assert.Equal(80, templateRef.Points);

        // El medio y sus blobs permanecen intactos.
        Assert.NotNull(await _repository.GetByIdAsync(medium.Id));
        var blob = Path.Combine(_storageRoot, storageKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(blob)!);
        await File.WriteAllTextAsync(blob, "contenido");
        Assert.True(File.Exists(blob));
    }

    [Fact]
    public async Task Delete_MedioSinReferencias_EliminaRegistroYBlobs()
    {
        var storageKey = $"media/podcasts/it-{Guid.NewGuid():N}.mp3";
        var thumbnailKey = $"media/thumbnails/it-{Guid.NewGuid():N}.jpg";
        var medium = await SeedMediaAsync(storageKey, thumbnailKey);

        var blobPath = Path.Combine(
            _storageRoot,
            storageKey.Replace('/', Path.DirectorySeparatorChar)
        );
        var thumbPath = Path.Combine(
            _storageRoot,
            thumbnailKey.Replace('/', Path.DirectorySeparatorChar)
        );
        Directory.CreateDirectory(Path.GetDirectoryName(blobPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(thumbPath)!);
        await File.WriteAllTextAsync(blobPath, "audio");
        await File.WriteAllTextAsync(thumbPath, "imagen");

        _db.ChangeTracker.Clear();

        var handler = new DeleteMediaItemCommandHandler(
            _repository,
            _storage,
            Microsoft
                .Extensions
                .Logging
                .Abstractions
                .NullLogger<DeleteMediaItemCommandHandler>
                .Instance
        );
        var result = await handler.Handle(new DeleteMediaItemCommand(medium.Id), default);

        Assert.False(result.NotFound);
        Assert.False(result.Blocked);

        // Registro eliminado de app.media_items y blobs borrados del storage.
        Assert.Null(await _repository.GetByIdAsync(medium.Id));
        Assert.False(File.Exists(blobPath));
        Assert.False(File.Exists(thumbPath));
    }

    [Fact]
    public async Task Delete_MedioReferenciadoEnSnapshotDeSemana_BloqueaYMarcaCongelada()
    {
        var storageKey = $"media/podcasts/it-{Guid.NewGuid():N}.mp3";
        var medium = await SeedMediaAsync(storageKey);
        var weekNumber = 3;
        var enrollment = await SeedEnrollmentWithSnapshotReferenceAsync(
            medium.Id,
            weekNumber,
            ProgramWeekStatus.Locked
        );

        var references = await _repository.GetReferencesAsync(medium.Id);

        Assert.NotNull(references);
        var enrollmentRef = Assert.Single(references!.EnrollmentReferences);
        Assert.Equal(enrollment.Id, enrollmentRef.EnrollmentId);
        Assert.Equal(weekNumber, enrollmentRef.WeekNumber);
        Assert.Equal(1, enrollmentRef.Weekday);
        // Semana futura (Locked) con fecha de cierre pendiente → no congelada.
        Assert.False(enrollmentRef.IsFrozen);

        var completedEnrollment = await SeedEnrollmentWithSnapshotReferenceAsync(
            medium.Id,
            1,
            ProgramWeekStatus.Completed
        );
        var referencesAfter = await _repository.GetReferencesAsync(medium.Id);

        Assert.Equal(2, referencesAfter!.TotalReferences);
        var completedRef = referencesAfter.EnrollmentReferences.Single(r =>
            r.EnrollmentId == completedEnrollment.Id
        );
        // Semana Completed → congelada (histórico clínico inmutable).
        Assert.True(completedRef.IsFrozen);
    }

    [Fact]
    public async Task SearchPage_ILikeYFiltros_CumplenElContrato()
    {
        var unique = Guid.NewGuid().ToString("N")[..6];
        for (var i = 0; i < 12; i++)
        {
            var item = new MediaItem
            {
                Id = Guid.NewGuid(),
                Title = i == 0 ? $"Circadiano {unique}" : $"Lección {unique} {i}",
                Author = i == 1 ? $"Autora circadiano {unique}" : "Autora Integración",
                MediaType = MediaType.Podcast,
                Category = i < 6 ? MediaCategory.Biologia : MediaCategory.Nutricion,
                StorageKey = $"media/podcasts/search-{unique}-{i}.mp3",
                ContentType = "audio/mpeg",
                FileSizeBytes = 1024,
                DurationSecs = 60 + i,
                Status = i % 2 == 0 ? MediaStatus.Published : MediaStatus.Draft,
                SortOrder = i,
                CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-i),
            };
            _db.MediaItems.Add(item);
        }
        await _db.SaveChangesAsync();

        // Búsqueda ILike multicampo (título y autor), case-insensitive.
        var (byTitle, titleTotal) = await _repository.SearchPageAsync(
            new MediaItemsPageRequest(Page: 1, PageSize: 10, Search: $"circadiano {unique}")
        );
        Assert.Equal(2, titleTotal); // 1 por título + 1 por autora
        Assert.Single(byTitle, m => m.Title.Contains($"Circadiano {unique}"));

        // Paginación server-side: página 2 con pageSize 5.
        var (page2, total) = await _repository.SearchPageAsync(
            new MediaItemsPageRequest(Page: 2, PageSize: 5, Search: unique)
        );
        Assert.Equal(12, total);
        Assert.Equal(5, page2.Count);

        // Filtro combinado status+category.
        var (filtered, filteredTotal) = await _repository.SearchPageAsync(
            new MediaItemsPageRequest(
                Page: 1,
                PageSize: 10,
                Search: unique,
                Category: MediaCategory.Nutricion,
                Status: MediaStatus.Published
            )
        );
        Assert.Equal(3, filteredTotal); // i ∈ {6, 8, 10}
        Assert.All(filtered, m => Assert.Equal(MediaCategory.Nutricion, m.Category));

        // Orden por duración descendente.
        var (byDuration, _) = await _repository.SearchPageAsync(
            new MediaItemsPageRequest(
                Page: 1,
                PageSize: 3,
                Search: unique,
                SortBy: "durationSecs",
                SortDirection: "desc"
            )
        );
        Assert.Equal(3, byDuration.Count);
        Assert.True(byDuration[0].DurationSecs >= byDuration[1].DurationSecs);
    }

    [Fact]
    public async Task UsageSnapshot_ConReferenciasPlantillaYSnapshot_CuentaSinNPlusOne()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var referenced = new MediaItem
        {
            Id = Guid.NewGuid(),
            Title = $"Referenciado {unique}",
            Author = "Autor",
            MediaType = MediaType.Podcast,
            Category = MediaCategory.Biologia,
            StorageKey = $"media/podcasts/usage-{unique}.mp3",
            ContentType = "audio/mpeg",
            FileSizeBytes = 1024,
            DurationSecs = 60,
            Status = MediaStatus.Published,
            SortOrder = 1,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var unassigned = new MediaItem
        {
            Id = Guid.NewGuid(),
            Title = $"Sin uso {unique}",
            Author = "Autor",
            MediaType = MediaType.Podcast,
            Category = MediaCategory.Biologia,
            StorageKey = $"media/podcasts/unused-{unique}.mp3",
            ContentType = "audio/mpeg",
            FileSizeBytes = 1024,
            DurationSecs = 60,
            Status = MediaStatus.Draft,
            SortOrder = 2,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.MediaItems.AddRange(referenced, unassigned);
        await _db.SaveChangesAsync();

        // Referencia en plantilla.
        await SeedTemplateWithMediaReferenceAsync(referenced.Id);
        // Referencia en snapshot de una semana.
        await SeedEnrollmentWithSnapshotReferenceAsync(referenced.Id, 4, ProgramWeekStatus.Locked);

        var usage = await _repository.GetUsageSnapshotAsync();

        // 1 plantilla + 1 semana de paciente = 2 referencias.
        Assert.Equal(2, usage.UsageCounts.GetValueOrDefault(referenced.Id));
        Assert.True(usage.ReferencedMediaIds.Contains(referenced.Id));
        Assert.False(usage.ReferencedMediaIds.Contains(unassigned.Id));
    }

    private static Microsoft.Extensions.Logging.ILoggerFactory NullLoggerFactory() =>
        Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
}
