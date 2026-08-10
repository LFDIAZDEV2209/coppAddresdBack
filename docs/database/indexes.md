# Índices — Registro

Reglas del proyecto (skill `database-indexes`). Cada índice creado en producción se registra aquí. Sin registro → no se crea.

## Registro

| Index | Tabla | Columnas | Tipo | Motivo | Query que beneficia | Impacto esperado | Costo potencial |
|---|---|---|---|---|---|---|---|
| — | — | — | — | — | — | — | — |

_(Vacío por esqueleto: sin tablas aún.)_

## Recordatorio de reglas

- FKs: índice obligatorio (PostgreSQL no lo crea solo).
- Orden de columnas: igualdad (`=`) primero, luego rango/orden.
- Tablas de alto INSERT: evaluar costo de escritura de cada índice extra.
- Validación con `EXPLAIN (ANALYZE, BUFFERS)` antes/después.
- Producción: `CREATE INDEX CONCURRENTLY` (no bloquea) — fuera de transacción (skill `migrations`).
- Evitar índices redundantes cubiertos por un compuesto.
