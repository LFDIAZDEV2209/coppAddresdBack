---
name: database-indexes
description: 'Índices en PostgreSQL: cuándo, qué tipo, orden de columnas, costo de escritura, documentación obligatoria de cada índice.'
---

# Índices — Reglas del proyecto

Índice sin justificación = deuda. Índice que falta = consulta lenta en producción. Cada índice relevante se decide y documenta.

## Cuándo crear

- Columnas usadas en `WHERE`/`JOIN`/`ORDER BY`/`GROUP BY` de queries frecuentes o críticas.
- **FKs siempre** (PostgreSQL NO crea índices automáticos en FKs).
- Contraints de unicidad → `UNIQUE INDEX` (los crea la constraint).
- Paginación keyset → índice del cursor compuesto (skill `pagination`).

## Cuándo NO crear

- Tablas pequeñas (< ~1k filas) sin volumen proyectado.
- Columnas de baja selectividad usadas solas (un `boolean` por sí solo casi nunca merece índice; evaluar compuesto o filtrado).
- "Por si acaso": cada índice extra paga en cada INSERT/UPDATE/DELETE y ocupa disco.

## Tipos útiles en este stack

| Tipo | Uso |
|---|---|
| B-tree (default) | La mayoría: igualdad, rango, orden |
| Compuesto | Filtros + orden frecuentes juntos; columnas en orden de uso: igualdad primero, luego rango |
| Filtrado (`WHERE ...`) | Subconjunto caliente (p. ej. `WHERE status = 'PENDING'`) |
| Único | Unicidad de negocio |
| `INCLUDE` (covering) | Evitar lectura de tabla: `(tenant_id) INCLUDE (name, created_at)` para queries que solo leen esas columnas |
| GIN | `jsonb`, arrays, ILIKE con trigram (`pg_trgm`), full-text |
| Hash | Igualdad simple en columnas largas (raro aquí) |

## Reglas de diseño

- **Orden de columnas**: `=` primero, luego rango/orden. `(tenant_id, created_at)` sirve a `WHERE tenant_id = X ORDER BY created_at`; `(created_at, tenant_id)` NO.
- Selectividad: columnas de alta cardinalidad primero.
- Índice compuesto puede servir a prefijos izquierdos; documentar qué queries cubre.
- Evitar índices redundantes: `(a, b)` ya cubre queries de `a` solo — no crear además `(a)` salvo necesidad de longitud/cobertura.
- Evaluar costo de escritura en tablas de alto INSERT (order-items, logs).

## Validación

- `EXPLAIN (ANALYZE, BUFFERS)` antes/después de crear el índice.
- Confirmar que el planner usa el índice (seq scan desaparece o es intencional).
- En migraciones de producción: `CREATE INDEX CONCURRENTLY` (no bloquea escritura) — ver skill `migrations`; no se puede correr dentro de transacción.

## Documentación obligatoria de cada índice

En `docs/database/indexes.md`:

```text
Index | Tabla | Columnas | Tipo | Motivo | Query que beneficia | Impacto esperado | Costo potencial
```

Sin documentación → no se crea en producción.
