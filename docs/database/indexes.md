# Índices — Registro

Reglas del proyecto (skill `database-indexes`). Cada índice creado en producción se registra aquí. Sin registro → no se crea.

## Registro

| Index | Tabla | Columnas | Tipo | Motivo | Query que beneficia | Impacto esperado | Costo potencial |
|---|---|---|---|---|---|---|---|
| `ix_activity_logs_occurred_at` | `audit.activity_logs` | `occurred_at` | B-tree | Filtros por rango temporal de actividad (reportería/consultas de auditoría) | `WHERE occurred_at BETWEEN ...` | Index scan en vez de seq scan sobre tabla creciente | Coste en cada INSERT del trigger (índice extra por fila auditada) |
| `ix_activity_logs_table_record` | `audit.activity_logs` | `(table_name, record_id)` | B-tree compuesto (igualdad primero) | Trazabilidad de una fila concreta: "historial de la fila X de la tabla Y" | `WHERE table_name = ? AND record_id = ? ORDER BY occurred_at` | Lookup puntual sin seq scan | Coste en cada INSERT del trigger |

_(Registros creados con la migración `InitialAuditSchema`; se documentarán más índices conforme existan tablas de negocio y queries reales. GIN sobre `jsonb` descartado por ahora: sin queries de filtrado por contenido.)_

## Recordatorio de reglas

- FKs: índice obligatorio (PostgreSQL no lo crea solo).
- Orden de columnas: igualdad (`=`) primero, luego rango/orden.
- Tablas de alto INSERT: evaluar costo de escritura de cada índice extra.
- Validación con `EXPLAIN (ANALYZE, BUFFERS)` antes/después.
- Producción: `CREATE INDEX CONCURRENTLY` (no bloquea) — fuera de transacción (skill `migrations`).
- Evitar índices redundantes cubiertos por un compuesto.
