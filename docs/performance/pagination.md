# Paginación — Reglas del proyecto

Skill: `pagination`.

## Estándar

- Default `pageSize`: **20**. Máximo: **100** (mayor → 400).
- `page` 1-based (offset) o `cursor` (keyset).
- Ordenamiento obligatorio y estable: incluir PK en `ORDER BY` (`ORDER BY CreatedAt DESC, Id DESC`).
- Filtros/orden antes de `Skip`/`Take`.
- `CountAsync` para `totalCount`/`TotalPages` (offset).
- `AsNoTracking()` + proyección + `CancellationToken`.

## Cuándo keyset/cursor

Tablas grandes (>~100k filas), páginas profundas, escrituras concurrentes, o presupuesto de latencia que el `OFFSET` no cumple.

Keyset: `WHERE (created_at, id) < (cursor)` con índice `(created_at, id)`, `Take(pageSize + 1)` para `hasNext`. Sin `OFFSET`, sin `COUNT`. Trade-off: sin salto de página ni totalCount barato.

## Decisiones por módulo

| Módulo | Estrategia | Notas |
|---|---|---|
| — | — | _(pendiente: sin módulos aún)_ |
