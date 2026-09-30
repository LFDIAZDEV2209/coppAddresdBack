using System.Data;
using CoppAddresd.Application.Features.Media;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Persistencia de la biblioteca de medios. El filtro de uso en programas
/// (<c>usage</c> y <c>usageCount</c>) cruza <c>WeeklyDayTemplate</c> y los
/// snapshots jsonb de <c>app.program_weeks</c> con consultas set-based: una
/// pasada por plantillas (GroupBy) y una pasada por semanas (lateral
/// jsonb_array_elements), nunca queries dentro de loops.
/// </summary>
public sealed class MediaItemRepository(AppDbContext dbContext) : IMediaItemRepository
{
    public async Task<MediaItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.MediaItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<MediaItem?> GetByStorageKeyAsync(
        string storageKey,
        CancellationToken ct = default
    ) =>
        await dbContext
            .MediaItems.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StorageKey == storageKey, ct);

    public async Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct = default) =>
        await dbContext
            .MediaItems.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<MediaItem> Items, int Total)> SearchPageAsync(
        MediaItemsPageRequest request,
        CancellationToken ct = default
    )
    {
        var query = dbContext.MediaItems.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // ILike multicampo (case-insensitive en PostgreSQL). Se escapan los
            // comodines LIKE para que la búsqueda sea literal.
            var pattern = $"%{EscapeLikePattern(request.Search.Trim())}%";
            query = query.Where(m =>
                EF.Functions.ILike(m.Title, pattern)
                || EF.Functions.ILike(m.Author, pattern)
                || (m.Description != null && EF.Functions.ILike(m.Description, pattern))
            );
        }

        if (request.MediaType.HasValue)
        {
            query = query.Where(m => m.MediaType == request.MediaType.Value);
        }

        if (request.Category.HasValue)
        {
            query = query.Where(m => m.Category == request.Category.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(m => m.Status == request.Status.Value);
        }

        if (request.IncludedIds is not null)
        {
            // Lista vacía = "assigned sin referencias" → resultado vacío legítimo.
            var ids = request.IncludedIds.ToList();
            query = query.Where(m => ids.Contains(m.Id));
        }

        if (request.ExcludedIds is not null)
        {
            var ids = request.ExcludedIds.ToList();
            if (ids.Count > 0)
            {
                query = query.Where(m => !ids.Contains(m.Id));
            }
        }

        var total = await query.CountAsync(ct);

        var ordered = ApplySortOrder(query, request.SortBy, request.SortDirection);

        var items = await ordered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    /// <summary>
    /// Orden con whitelist (title/author/createdAt/durationSecs/sortOrder) y
    /// dirección asc/desc. El default es sortOrder + createdAt (orden editorial
    /// del catálogo). Siempre se agrega un desempate estable.
    /// </summary>
    private static IOrderedQueryable<MediaItem> ApplySortOrder(
        IQueryable<MediaItem> query,
        string? sortBy,
        string? sortDirection
    )
    {
        var descending = string.Equals(
            sortDirection?.Trim(),
            "desc",
            StringComparison.OrdinalIgnoreCase
        );

        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "title" => descending
                ? query.OrderByDescending(m => m.Title).ThenBy(m => m.CreatedAt)
                : query.OrderBy(m => m.Title).ThenBy(m => m.CreatedAt),
            "author" => descending
                ? query.OrderByDescending(m => m.Author).ThenBy(m => m.CreatedAt)
                : query.OrderBy(m => m.Author).ThenBy(m => m.CreatedAt),
            "createdat" => descending
                ? query.OrderByDescending(m => m.CreatedAt).ThenBy(m => m.Id)
                : query.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id),
            "durationsecs" => descending
                ? query.OrderByDescending(m => m.DurationSecs ?? 0).ThenBy(m => m.CreatedAt)
                : query.OrderBy(m => m.DurationSecs ?? 0).ThenBy(m => m.CreatedAt),
            "sortorder" => descending
                ? query.OrderByDescending(m => m.SortOrder).ThenBy(m => m.CreatedAt)
                : query.OrderBy(m => m.SortOrder).ThenBy(m => m.CreatedAt),
            // Whitelist: cualquier otra clave vuelve al orden editorial.
            _ => query.OrderBy(m => m.SortOrder).ThenBy(m => m.CreatedAt),
        };
    }

    private static string EscapeLikePattern(string term) =>
        term.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    public async Task<MediaUsageSnapshot> GetUsageSnapshotAsync(CancellationToken ct = default)
    {
        var counts = new Dictionary<Guid, int>();

        // 1) Referencias a nivel plantilla (WeeklyDayTemplate.media_id).
        var templateCounts = await dbContext
            .WeeklyDayTemplates.AsNoTracking()
            .Where(d => d.MediaId != null)
            .GroupBy(d => d.MediaId!.Value)
            .Select(g => new { MediaId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        foreach (var row in templateCounts)
        {
            counts[row.MediaId] = counts.GetValueOrDefault(row.MediaId) + row.Count;
        }

        // 2) Referencias en snapshots de semanas de pacientes (jsonb).
        foreach (var (mediaId, weekCount) in await QuerySnapshotMediaUsageAsync(ct))
        {
            counts[mediaId] = counts.GetValueOrDefault(mediaId) + weekCount;
        }

        return new MediaUsageSnapshot(counts, new HashSet<Guid>(counts.Keys));
    }

    /// <summary>
    /// Referencias por medio dentro de <c>app.program_weeks.tasks_snapshot</c>
    /// (jsonb): una sola pasada con <c>jsonb_array_elements</c> lateral sobre
    /// PostgreSQL (el mismo shape {weekday, task_code, …, media_id} que
    /// serializa ProgramRepository). Devuelve (mediaId, semanasConUso).
    /// </summary>
    private async Task<List<(Guid MediaId, int WeekCount)>> QuerySnapshotMediaUsageAsync(
        CancellationToken ct
    )
    {
        var connection = dbContext.Database.GetDbConnection();
        var ownsOpen = false;
        try
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(ct);
                ownsOpen = true;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT (t->>'media_id')::uuid AS media_id
                     , COUNT(DISTINCT pw.id)::int AS week_count
                FROM app.program_weeks pw
                CROSS JOIN LATERAL jsonb_array_elements(pw.tasks_snapshot) AS t
                WHERE jsonb_typeof(pw.tasks_snapshot) = 'array'
                  AND t ? 'media_id'
                  AND t->>'media_id' IS NOT NULL
                GROUP BY 1
                """;

            var result = new List<(Guid, int)>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                result.Add((reader.GetGuid(0), reader.GetInt32(1)));
            }

            return result;
        }
        finally
        {
            if (ownsOpen)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<int> ReorderAsync(
        IReadOnlyList<ReorderMediaItemEntry> entries,
        CancellationToken ct = default
    )
    {
        var ids = entries.Select(e => e.Id).Distinct().ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        var items = await dbContext.MediaItems.Where(m => ids.Contains(m.Id)).ToListAsync(ct);

        var orderBy = entries.GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First().SortOrder);

        var updated = 0;
        foreach (var item in items)
        {
            if (!orderBy.TryGetValue(item.Id, out var sortOrder) || item.SortOrder == sortOrder)
            {
                continue;
            }

            item.SortOrder = sortOrder;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            updated++;
        }

        if (updated > 0)
        {
            // Escritura atómica: un solo SaveChanges persiste todo el lote.
            await dbContext.SaveChangesAsync(ct);
        }

        return updated;
    }

    public async Task<MediaItem> AddAsync(MediaItem item, CancellationToken ct = default)
    {
        dbContext.MediaItems.Add(item);
        await dbContext.SaveChangesAsync(ct);
        return item;
    }

    public async Task UpdateAsync(MediaItem item, CancellationToken ct = default)
    {
        dbContext.MediaItems.Update(item);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(MediaItem item, CancellationToken ct = default)
    {
        dbContext.MediaItems.Remove(item);
        await dbContext.SaveChangesAsync(ct);
    }
}
