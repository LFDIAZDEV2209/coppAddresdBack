using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using MediatR;

namespace CoppAddresd.Application.Features.Media;

/// <summary>
/// Consulta paginada server-side de la biblioteca de medios (REQ-PCA-06):
/// búsqueda textual, filtros combinados (tipo/categoría/estado/uso) y
/// ordenación con whitelist. El envelope es
/// <c>{ items, totalCount, page, pageSize, totalPages }</c>.
/// </summary>
/// <param name="Page">Página base 1 (por defecto 1).</param>
/// <param name="PageSize">Elementos por página (default 20, máximo 100).</param>
/// <param name="Search">Búsqueda case-insensitive sobre título, autor y descripción.</param>
/// <param name="MediaType">Filtro opcional por tipo (Podcast/Video/Audio).</param>
/// <param name="Category">Filtro opcional por categoría temática.</param>
/// <param name="Status">Filtro opcional por estado (Draft/Published/Archived).</param>
/// <param name="Usage">
/// Filtro de uso en programas: <c>all</c> (default), <c>assigned</c> (con al
/// menos una referencia en WeeklyDayTemplate o snapshot de semana) o
/// <c>unassigned</c> (sin ninguna referencia).
/// </param>
/// <param name="SortBy">title | author | createdAt | durationSecs | sortOrder (default).</param>
/// <param name="SortDirection">asc | desc (default asc).</param>
public record GetMediaItemsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    MediaType? MediaType = null,
    MediaCategory? Category = null,
    MediaStatus? Status = null,
    string? Usage = null,
    string? SortBy = null,
    string? SortDirection = null
) : IRequest<PagedMediaItemsResult>;

/// <summary>
/// Parámetros ya normalizados que el handler envía al repositorio. Los ids
/// de uso (asignado/no asignado) se resuelven ANTES de paginar para que el
/// filtro participe del total y del corte de página (nunca después).
/// </summary>
public sealed record MediaItemsPageRequest(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    MediaType? MediaType = null,
    MediaCategory? Category = null,
    MediaStatus? Status = null,
    IReadOnlyCollection<Guid>? IncludedIds = null,
    IReadOnlyCollection<Guid>? ExcludedIds = null,
    string? SortBy = null,
    string? SortDirection = null
);

/// <summary>
/// Estadística de uso de los medios en los programas: cuántas referencias
/// activas tiene cada medio (plantillas + semanas de pacientes) y el conjunto
/// de medios con al menos una referencia. La resuelve el repositorio en una
/// pasada set-based (sin N+1).
/// </summary>
public sealed record MediaUsageSnapshot(
    IReadOnlyDictionary<Guid, int> UsageCounts,
    IReadOnlySet<Guid> ReferencedMediaIds
);

/// <summary>Representación de un medio para el listado paginado (sin capítulos/takeaways) + uso.</summary>
public record MediaItemSummaryDto(
    Guid Id,
    string Title,
    string? Description,
    string Author,
    MediaType MediaType,
    MediaCategory Category,
    string StorageKey,
    string? ThumbnailKey,
    string? ContentType,
    long? FileSizeBytes,
    int? DurationSecs,
    MediaStatus Status,
    int SortOrder,
    int Day,
    int Month,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid? CreatedBy,
    int UsageCount
)
{
    public static MediaItemSummaryDto FromEntity(MediaItem entity, int usageCount) =>
        new(
            entity.Id,
            entity.Title,
            entity.Description,
            entity.Author,
            entity.MediaType,
            entity.Category,
            entity.StorageKey,
            entity.ThumbnailKey,
            entity.ContentType,
            entity.FileSizeBytes,
            entity.DurationSecs,
            entity.Status,
            entity.SortOrder,
            entity.Day,
            entity.Month,
            entity.PublishedAt,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.CreatedBy,
            usageCount
        );
}

/// <summary>Envelope estándar del listado paginado (REQ-PCA-06).</summary>
public sealed record PagedMediaItemsResult(
    IReadOnlyList<MediaItemSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public sealed class GetMediaItemsQueryHandler(IMediaItemRepository repository)
    : IRequestHandler<GetMediaItemsQuery, PagedMediaItemsResult>
{
    public async Task<PagedMediaItemsResult> Handle(
        GetMediaItemsQuery request,
        CancellationToken ct
    )
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 20 : request.PageSize, 1, 100);

        // Uso en programas: el snapshot de referencias resuelve assigned/
        // unassigned y alimenta usageCount de cada fila del resultado
        // (set-based, sin N+1).
        var usageSnapshot = await repository.GetUsageSnapshotAsync(ct);

        IReadOnlyCollection<Guid>? includedIds = null;
        IReadOnlyCollection<Guid>? excludedIds = null;
        var usage = request.Usage?.Trim().ToLowerInvariant();
        if (usage == "assigned")
        {
            includedIds = usageSnapshot.ReferencedMediaIds.ToList();
        }
        else if (usage == "unassigned")
        {
            excludedIds = usageSnapshot.ReferencedMediaIds.ToList();
        }

        var (items, total) = await repository.SearchPageAsync(
            new MediaItemsPageRequest(
                Page: page,
                PageSize: pageSize,
                Search: string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
                MediaType: request.MediaType,
                Category: request.Category,
                Status: request.Status,
                IncludedIds: includedIds,
                ExcludedIds: excludedIds,
                SortBy: request.SortBy,
                SortDirection: request.SortDirection
            ),
            ct
        );

        var summaries = items
            .Select(e =>
                MediaItemSummaryDto.FromEntity(e, usageSnapshot.UsageCounts.GetValueOrDefault(e.Id))
            )
            .ToList();

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return new PagedMediaItemsResult(summaries, total, page, pageSize, totalPages);
    }
}
