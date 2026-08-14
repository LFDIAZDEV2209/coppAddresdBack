# Índices — Registro

Reglas del proyecto (skill `database-indexes`). Cada índice creado en producción se registra aquí. Sin registro → no se crea.

## Registro

| Index | Tabla | Columnas | Tipo | Motivo | Query que beneficia | Impacto esperado | Costo potencial |
|---|---|---|---|---|---|---|---|
| `ix_activity_logs_occurred_at` | `audit.activity_logs` | `occurred_at` | B-tree | Filtros por rango temporal de actividad (reportería/consultas de auditoría) | `WHERE occurred_at BETWEEN ...` | Index scan en vez de seq scan sobre tabla creciente | Coste en cada INSERT del trigger (índice extra por fila auditada) |
| `ix_activity_logs_table_record` | `audit.activity_logs` | `(table_name, record_id)` | B-tree compuesto (igualdad primero) | Trazabilidad de una fila concreta: "historial de la fila X de la tabla Y" | `WHERE table_name = ? AND record_id = ? ORDER BY occurred_at` | Lookup puntual sin seq scan | Coste en cada INSERT del trigger |
| `ix_allergens_name` | `app.allergens` | `name` | UNIQUE (constraint) | Unicidad del alergeno del catálogo | `WHERE name = ?` (get-or-create) | Lookup puntual por nombre | Coste de índice en inserts ocasionales del catálogo |
| `ix_icd10_codes_code` | `app.icd10_codes` | `code` | UNIQUE (constraint) | Unicidad del código ICD-10 del catálogo | `WHERE code = ?` (get-or-create) | Lookup puntual por código | Ídem |
| `ix_medications_name` | `app.medications` | `name` | UNIQUE (constraint) | Unicidad del fármaco del catálogo | `WHERE name = ?` (get-or-create) | Lookup puntual por nombre | Ídem |
| `ix_patient_diagnoses_icd10_code_id` | `app.patient_diagnoses` | `icd10_code_id` | B-tree | FK a catálogo (PG no indexa FKs solo) | JOIN agregado `patient_diagnoses → icd10_codes` | Index scan en JOIN | Coste de escritura por fila de diagnóstico |
| `ix_patient_medications_medication_id` | `app.patient_medications` | `medication_id` | B-tree | FK a catálogo | JOIN agregado `patient_medications → medications` | Index scan en JOIN | Coste de escritura por fila de medicamento |
| `IX_patient_allergies_allergen_id` | `app.patient_allergies` | `allergen_id` | B-tree | FK a catálogo | JOIN agregado `patient_allergies → allergens` | Index scan en JOIN | Coste de escritura por fila de alergia |
| `ix_patient_allergies_patient_allergen` | `app.patient_allergies` | `(patient_id, allergen_id)` | UNIQUE (constraint) | Un paciente no repite el mismo alergeno | `WHERE patient_id = ? AND allergen_id = ?` | Lookup puntual | Coste de escritura por alergia |

_Índices de catálogos y FKs creados con la migración `NormalizeClinicalCatalogs`._
_Los índices únicos de catálogo también sirven de índice a las FK (un lookup por `name`/`code`)._
_Los compuestos `(patient_id, ...)` existentes cubren las queries por paciente._

_(Registros creados con la migración `InitialAuditSchema`; se documentarán más índices conforme existan tablas de negocio y queries reales. GIN sobre `jsonb` descartado por ahora: sin queries de filtrado por contenido.)_

## Recordatorio de reglas

- FKs: índice obligatorio (PostgreSQL no lo crea solo).
- Orden de columnas: igualdad (`=`) primero, luego rango/orden.
- Tablas de alto INSERT: evaluar costo de escritura de cada índice extra.
- Validación con `EXPLAIN (ANALYZE, BUFFERS)` antes/después.
- Producción: `CREATE INDEX CONCURRENTLY` (no bloquea) — fuera de transacción (skill `migrations`).
- Evitar índices redundantes cubiertos por un compuesto.
