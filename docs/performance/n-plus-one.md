# N+1 — Detección y soluciones registradas

Skill: `n-plus-one`.

## Patrones prohibidos

- Loops (`foreach`/`for`) con `await` a repositorio/EF/HttpClient.
- Navegaciones EF referenciadas fuera de la query raíz sin `Include`/proyección.
- Repositorio consultado repetidamente con los mismos ids en un request.
- `.First()` dentro de un `Select` de otra query.

## Soluciones en orden de preferencia

1. Proyección `Select` a DTO (una query).
2. `Include`/`ThenInclude` (con `AsSplitQuery()` si hay colecciones múltiples) — solo si se necesitan entidades completas.
3. Join explícito + proyección.
4. Batch query con `Contains(ids)` → `IN`.
5. Agregaciones/grouping en SQL.
6. `ExecuteUpdateAsync`/`ExecuteDeleteAsync` para mutaciones masivas.

## Verificación

- Dev: logging de SQL — un listado paginado no debe emitir N+1 sentencias.
- Límite pragmático: >~5-10 queries por request en un listado → investigar.

## Casos registrados

| Fecha | Ubicación | Patrón encontrado | Solución aplicada | Resultado |
|---|---|---|---|---|
| — | — | — | — | — |

_(Vacío por esqueleto.)_
