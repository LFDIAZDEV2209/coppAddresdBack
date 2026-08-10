# LINQ / EF Core — Reglas y decisiones de rendimiento

Reglas del proyecto (skills `linq`, `entity-framework`, `query-performance`).

## Reglas resumidas

- `IQueryable` se ejecuta en SQL, de forma diferida; `IEnumerable`/`List` en memoria.
- Prohibido: `ToList()` temprano + filtrar en memoria; consultas en loops (N+1); enumerar dos veces el mismo `IQueryable`.
- Orden de composición: `AsNoTracking()` → filtros → orden → paginación → proyección → `ToListAsync(ct)` al final.
- Lecturas sin mutación → `AsNoTracking()`.
- Proyectar a DTO con `Select` (no cargar entidades completas).
- Async siempre (`ToListAsync`/`FirstOrDefaultAsync`/`CountAsync`/`AnyAsync`...) con `CancellationToken`.
- `AsSplitQuery()` con Includes de colecciones (evaluar join cartesiano).
- Funciones CLR no traducibles → NO en `Where`/`OrderBy` (evalúa en cliente).

## Decisiones registradas

| Fecha | Módulo | Decisión | Motivo |
|---|---|---|---|
| — | — | — | — |

_(Vacío por esqueleto.)_
