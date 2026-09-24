namespace CoppAddresd.Application.Features.Measurements.Queries.GetMyMeasurements;

/// <summary>
/// Resultado paginado por cursor opaco para lecturas móviles.
/// <typeparamref name="T"/> es el DTO de cada fila (mínimo, sin PHI sensible).
/// <c>NextCursor</c> es opaco para el cliente: lo genera el repositorio
/// (siguiente tarea) y el móvil lo devuelve tal cual para la página siguiente.
/// <c>HasNextPage</c> en false con <c>NextCursor</c> null indica última página.
/// </summary>
public sealed record CursorPagedResult<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    bool HasNextPage
);
