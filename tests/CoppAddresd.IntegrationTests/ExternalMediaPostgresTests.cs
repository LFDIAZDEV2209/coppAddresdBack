using System.Text.Json;
using CoppAddresd.Application.Features.Media;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;

namespace CoppAddresd.IntegrationTests;

public sealed partial class NutritionPlanWorkflowTests
{
    [NutritionPostgresFact]
    public async Task MediosExternos_FiltrosSqlCombinadosTotalesPaginacionYContrato()
    {
        await using var db = Context(); var repository = new MediaItemRepository(db);
        var creator = new CreateMediaItemCommandHandler(repository, NullLogger<CreateMediaItemCommandHandler>.Instance);
        CreateMediaItemCommand Command(string url) => new("A YouTube", null, "Equipo", MediaType.Video,
            MediaCategory.Biologia, null, null, null, null, 45, MediaStatus.Published, 1, 0, 0,
            Tags: [" Movilidad ", "suave", "movilidad"], SourceKind: "external", Provider: "youtube", ExternalUrl: url);
        var youtube = await creator.Handle(Command("https://youtu.be/dQw4w9WgXcQ"), CancellationToken.None);
        // Cada POST/PUT HTTP tiene su propio seguimiento de EF.
        db.ChangeTracker.Clear();
        var replay = await creator.Handle(Command("https://www.youtube.com/watch?v=dQw4w9WgXcQ"), CancellationToken.None);
        Assert.Equal(youtube.Id, replay.Id);
        db.MediaItems.Add(new() { Id = Guid.NewGuid(), Title = "B Vimeo", Author = "Equipo", MediaType = MediaType.Video,
            Category = MediaCategory.Biologia, StorageKey = "external/vimeo/123456789", SourceKind = "external",
            ExternalUrl = "https://vimeo.com/123456789", Tags = ["movilidad"], Status = MediaStatus.Draft });
        db.MediaItems.Add(new() { Id = Guid.NewGuid(), Title = "C Archivo", Author = "Equipo", MediaType = MediaType.Video,
            Category = MediaCategory.Biologia, StorageKey = "media/a.mp4", Tags = ["movilidad"], Status = MediaStatus.Published });
        await db.SaveChangesAsync();
        var handler = new GetMediaItemsQueryHandler(repository);
        var first = await handler.Handle(new(PageSize: 1, SourceKind: "EXTERNAL", Tags: [" MOVILIDAD "], SortBy: "title"), CancellationToken.None);
        var second = await handler.Handle(new(Page: 2, PageSize: 1, SourceKind: "external", Tags: ["movilidad"], SortBy: "title"), CancellationToken.None);
        Assert.Equal(2, first.TotalCount); Assert.Equal(2, first.TotalPages); Assert.Single(first.Items); Assert.Single(second.Items);
        Assert.NotEqual(first.Items[0].Id, second.Items[0].Id);
        Assert.Equal("youtube", first.Items[0].Provider); Assert.Equal("vimeo", second.Items[0].Provider);
        var combined = await handler.Handle(new(SourceKind: "external", Tags: ["movilidad", "suave"], Status: MediaStatus.Published, MediaType: MediaType.Video, Category: MediaCategory.Biologia), CancellationToken.None);
        Assert.Equal(youtube.Id, Assert.Single(combined.Items).Id); Assert.Equal(1, combined.TotalCount);
        Assert.Equal("file", Assert.Single((await handler.Handle(new(SourceKind: "file", Tags: ["movilidad"]), CancellationToken.None)).Items).SourceKind);
        Assert.Empty((await handler.Handle(new(Tags: ["inexistente"]), CancellationToken.None)).Items);
        await Assert.ThrowsAsync<UnprocessableEntityException>(() => handler.Handle(new(SourceKind: "otro"), CancellationToken.None));
        var wire = JsonSerializer.SerializeToElement(combined.Items[0], new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("external", wire.GetProperty("source_kind").GetString());
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", wire.GetProperty("external_url").GetString());
        db.ChangeTracker.Clear();
        var storage = Substitute.For<IObjectStorageService>();
        var updated = await new UpdateMediaItemCommandHandler(repository, storage, NullLogger<UpdateMediaItemCommandHandler>.Instance)
            .Handle(new(youtube.Id, "A editado", null, "Equipo", MediaType.Video, MediaCategory.Biologia, null, null, null, null, null,
                MediaStatus.Draft, 1, 0, 0, SourceKind: "external", Provider: "vimeo", ExternalUrl: "https://vimeo.com/987654321", Tags: [" FUERZA "]), CancellationToken.None);
        Assert.NotNull(updated); Assert.Equal("vimeo", updated.Provider); Assert.Equal("fuerza", Assert.Single(updated.Tags!));
        Assert.Empty(storage.ReceivedCalls()); Assert.Equal(3, await db.MediaItems.CountAsync());
    }

    [NutritionPostgresFact]
    public async Task MediosOrigenTags_MigracionAditivaReversaProtegidaYVolumen()
    {
        await using var db = Context(); var migrator = db.GetService<IMigrator>();
        var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var current = migrations.Single(m => m.EndsWith("_AddMediaSourceAndTags"));
        var previous = migrations[Array.IndexOf(migrations, current) - 1];
        await migrator.MigrateAsync(previous);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO app.media_items (id,title,author,media_type,category,storage_key,status,sort_order,day,month)
            SELECT gen_random_uuid(),'Volumen','Equipo','Video','Biologia','media/volumen/' || n,'Draft',n,1,1 FROM generate_series(1,10000) n
            """);
        await migrator.MigrateAsync(current);
        Assert.Equal(10000, await db.MediaItems.CountAsync(m => m.SourceKind == "file"));
        var id = await db.MediaItems.Select(m => m.Id).FirstAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE app.media_items SET source_kind='external',external_url='https://vimeo.com/123456789',tags='[\"movilidad\"]'::jsonb WHERE id={id}");
        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous));
        Assert.Equal(1, await db.MediaItems.CountAsync(m => m.SourceKind == "external"));
        await db.Database.ExecuteSqlRawAsync("ANALYZE app.media_items");
        var plan = await db.Database.SqlQueryRaw<string>("EXPLAIN (ANALYZE, BUFFERS) SELECT id FROM app.media_items WHERE tags @> '[\"movilidad\"]'::jsonb").ToListAsync();
        Assert.Contains(plan, line => line.Contains("ix_media_items_tags"));
        // Solo se restablece metadata sintética de esta BD efímera para probar la reversa.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE app.media_items SET source_kind='file',external_url=NULL,tags='[]'::jsonb WHERE id={id}");
        await migrator.MigrateAsync(previous);
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM information_schema.columns WHERE table_schema='app' AND table_name='media_items' AND column_name IN ('source_kind','tags','external_url')").SingleAsync());
        await migrator.MigrateAsync(current);
        Assert.Equal(10000, await db.MediaItems.CountAsync());
    }
}
