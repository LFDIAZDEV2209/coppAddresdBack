---
name: pagination
description: 'Paginación estándar en CoppAddresd: offset vs keyset/cursor, límites, metadata, orden estable.'
---

# Paginación — Reglas del proyecto

Nunca endpoints que devuelvan cantidades ilimitadas. Toda lista con crecimiento real página.

## Parámetros estándar

```text
Default PageSize: 20
Maximum PageSize: 100
```

- `page` (1-based) para offset; `cursor` para keyset.
- `pageSize > 100` → rechazar con 400 o clampear a 100 (decidir y documentar por endpoint; por defecto rechazar con mensaje claro).
- `pageSize <= 0` → 400.

## Offset pagination (predeterminada para tablas pequeñas/medias)

```csharp
var query = db.Entities.AsNoTracking()
    .Where(...)
    .OrderBy(...);

var totalCount = await query.CountAsync(ct);
var items = await query
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .Select(...)                 // proyección a DTO
    .ToListAsync(ct);
```

- Siempre `CountAsync` para `totalCount` y `TotalPages`.
- Coste: `OFFSET` salta filas — degrada en páginas profundas de tablas grandes.

## Keyset / Cursor pagination (tablas grandes, alta concurrencia)

Preferir cuando la tabla supere ~100k filas, haya escrituras concurrentes o páginas profundas:

```csharp
var items = await db.Entities.AsNoTracking()
    .Where(e => e.Id > cursor)       // o (CreatedAt, Id) compuesto
    .OrderBy(e => e.Id)
    .Take(pageSize + 1)              // +1 para saber si hay siguiente
    .Select(...)
    .ToListAsync(ct);

var hasNext = items.Count > pageSize;
items = items.Take(pageSize);
```

- **Sin `OFFSET`, sin `COUNT`**: O(log n) por página, estable bajo escritura concurrente.
- Cursor codificado (base64/url-safe) del último elemento: `(createdAt, id)` para orden por fecha.
- Requiere índice que soporte el orden (`(created_at, id)`).
- Trade-off: no hay salto arbitrario de páginas ni `totalCount` barato — documentar por endpoint.

## Metadata (respuesta de lista)

```csharp
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,          // o string? Cursor (opcional)
    int PageSize,
    int TotalCount,    // solo offset; omitir en keyset
    int TotalPages,    // solo offset
    bool HasNextPage);
```

## Reglas

- Ordenamiento **obligatorio y estable**: incluir PK en el `ORDER BY` (p. ej. `ORDER BY CreatedAt DESC, Id DESC`) para paginación determinista.
- Filtros/orden antes de `Skip/Take`.
- Combinar con `AsNoTracking()` + proyección (skill `linq`).
- `CancellationToken` propagado a `CountAsync`/`ToListAsync` (skill `cancellation-token`).
- En tests de paginación cubrir: bordes (página vacía, última página incompleta), `pageSize` límite, orden estable con empates, keyset con datos nuevos insertados entre páginas.
