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

    Task<MediaItem> AddAsync(MediaItem item, CancellationToken ct = default);

    Task UpdateAsync(MediaItem item, CancellationToken ct = default);

    Task DeleteAsync(MediaItem item, CancellationToken ct = default);
}
