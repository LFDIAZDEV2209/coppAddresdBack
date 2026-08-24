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
| `ix_unit_of_measures_code` | `app.unit_of_measures` | `code` | UNIQUE (constraint) | Unicidad de la unidad del catálogo de mediciones | `WHERE code = ?` (get-or-create del seeder) | Lookup puntual por código | Coste de índice en inserts ocasionales del catálogo |
| `ix_measurement_metrics_code` | `app.measurement_metrics` | `code` | UNIQUE (constraint) | Unicidad de la métrica clínica del catálogo | `WHERE code = ?` (get-or-create del seeder) | Lookup puntual por código | Ídem |
| `ix_measurement_metrics_default_unit_id` | `app.measurement_metrics` | `default_unit_id` | B-tree | FK a catálogo (PG no indexa FKs solo) | JOIN agregado `measurement_metrics → unit_of_measures` | Index scan en JOIN | Coste de escritura por métrica |
| `ix_measurement_reference_ranges_metric_id` | `app.measurement_reference_ranges` | `metric_id` | B-tree | FK a catálogo; lookup de rangos de una métrica | `WHERE metric_id = ?` (resolución de rango por edad/género) | Lookup puntual por métrica | Coste de escritura por rango |
| `ix_measurement_reference_ranges_unit_id` | `app.measurement_reference_ranges` | `unit_id` | B-tree | FK a catálogo | JOIN agregado `measurement_reference_ranges → unit_of_measures` | Index scan en JOIN | Coste de escritura por rango |
| `ix_encounters_patient_id` | `app.encounters` | `patient_id` | B-tree | FK; historial de consultas de un paciente | `WHERE patient_id = ? ORDER BY started_at DESC` | Lookup puntual por paciente | Coste de escritura por encounter |
| `ix_encounters_professional_id` | `app.encounters` | `professional_id` | B-tree | FK; agenda del profesional | `WHERE professional_id = ? AND started_at >= ?` | Index scan en JOIN | Coste de escritura por encounter |
| `ix_encounters_status` | `app.encounters` | `status` | B-tree | Filtro por estado de consulta | `WHERE status = 'in_progress'` (consultas activas) | Index scan sobre conjunto pequeño | Coste de escritura por encounter |
| `ix_encounters_type` | `app.encounters` | `type` | B-tree | Filtro por tipo de consulta | `WHERE type = 'consulta_periodica'` | Index scan sobre conjunto pequeño | Coste de escritura por encounter |
| `ix_clinical_measurements_patient_id` | `app.clinical_measurements` | `patient_id` | B-tree | FK; serie temporal de un paciente | `WHERE patient_id = ?` | Lookup puntual por paciente | Coste de escritura por medición |
| `ix_clinical_measurements_metric_id` | `app.clinical_measurements` | `metric_id` | B-tree | FK a catálogo; evolución de una métrica | `WHERE metric_id = ?` (análisis por tipo de medición) | Index scan en JOIN | Coste de escritura por medición |
| `ix_clinical_measurements_encounter_id` | `app.clinical_measurements` | `encounter_id` | B-tree | FK a encounters (nullable, monitoreo autónomo) | JOIN agregado `clinical_measurements → encounters` | Index scan en JOIN | Coste de escritura por medición |
| `ix_clinical_measurements_patient_observed` | `app.clinical_measurements` | `(patient_id, observed_at DESC)` | B-tree compuesto (igualdad primero + orden descendente) | "Último valor por métrica" y tendencias por paciente ordenadas por fecha de observación | `WHERE patient_id = ? AND metric_id = ? ORDER BY observed_at DESC LIMIT 1` | Lookup puntual + top-1 sin sort | Coste de escritura por medición (índice compuesto) |

_Índices de catálogos y FKs creados con la migración `NormalizeClinicalCatalogs`._
_Los índices únicos de catálogo también sirven de índice a las FK (un lookup por `name`/`code`)._
_Los compuestos `(patient_id, ...)` existentes cubren las queries por paciente._
_Índices del módulo de mediciones clínicas creados con la migración `AddClinicalMeasurements` (catálogos + encounters + clinical_measurements)._

_(Registros creados con la migración `InitialAuditSchema`; se documentarán más índices conforme existan tablas de negocio y queries reales. GIN sobre `jsonb` descartado por ahora: sin queries de filtrado por contenido.)_

## Recordatorio de reglas

- FKs: índice obligatorio (PostgreSQL no lo crea solo).
- Orden de columnas: igualdad (`=`) primero, luego rango/orden.
- Tablas de alto INSERT: evaluar costo de escritura de cada índice extra.
- Validación con `EXPLAIN (ANALYZE, BUFFERS)` antes/después.
- Producción: `CREATE INDEX CONCURRENTLY` (no bloquea) — fuera de transacción (skill `migrations`).
- Evitar índices redundantes cubiertos por un compuesto.
