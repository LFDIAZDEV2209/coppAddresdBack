using CoppAddresd.Application.Features.Media;
using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio de <see cref="MediaItem"/>. Define las operaciones de
/// persistencia del módulo de medios; la implementación vive en Infrastructure.
/// </summary>
public interface IMediaItemRepository
{
    Task<MediaItem?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<MediaItem?> GetByStorageKeyAsync(string storageKey, CancellationToken ct = default);

    Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Búsqueda paginada server-side (REQ-PCA-06): traducción eficiente a SQL
    /// (Skip/Take, ILike multicampo en PostgreSQL, filtros combinados y orden
    /// con whitelist). <paramref name="request.IncludedIds"/>/
    /// <paramref name="request.ExcludedIds"/> materializan el filtro
    /// <c>usage=assigned/unassigned</c> (null = sin filtro de uso).
    /// </summary>
    Task<(IReadOnlyList<MediaItem> Items, int Total)> SearchPageAsync(
        MediaItemsPageRequest request,
        CancellationToken ct = default
    );

    /// <summary>
    /// Estadística set-based de uso de los medios en programas: referencias en
    /// <c>WeeklyDayTemplate</c> y en los snapshots <c>TasksSnapshot</c> de las
    /// semanas de pacientes.
    /// </summary>
    Task<MediaUsageSnapshot> GetUsageSnapshotAsync(CancellationToken ct = default);

    /// <summary>
    /// Reordenamiento en lote de <c>SortOrder</c> (REQ-PCA-03): una única
    /// escritura atómica (un <c>SaveChangesAsync</c>). Los ids inexistentes se
    /// ignoran. Devuelve la cantidad de filas realmente actualizadas.
    /// </summary>
    Task<int> ReorderAsync(
        IReadOnlyList<ReorderMediaItemEntry> entries,
        CancellationToken ct = default
    );

    /// <summary>
    /// "Dónde se usa" el medio (REQ-PCA-07): referencias en
    /// <c>WeeklyDayTemplate</c> y en los snapshots jsonb de las semanas de
    /// pacientes, con el total consolidado. Devuelve null si el medio no existe.
    /// </summary>
    Task<MediaReferencesDto?> GetReferencesAsync(Guid mediaId, CancellationToken ct = default);

    /// <summary>
    /// Conjunto de todas las claves en uso del catálogo (StorageKey +
    /// ThumbnailKey). Alimenta el contraste del recolector de huérfanos
    /// (REQ-PCA-08) en una sola consulta.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetAllStorageKeysAsync(CancellationToken ct = default);

    Task<MediaItem> AddAsync(MediaItem item, CancellationToken ct = default);

    Task UpdateAsync(MediaItem item, CancellationToken ct = default);

    Task DeleteAsync(MediaItem item, CancellationToken ct = default);
}
