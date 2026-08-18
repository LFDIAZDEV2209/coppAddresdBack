# Normalización — Base de datos

Reglas del proyecto (skill `database-normalization`). Base de diseño: **3NF**.

## Niveles aplicados

| Nivel | Regla | Aplicación en el proyecto |
|---|---|---|
| 1NF | Valores atómicos | Tablas hijas en vez de listas separadas por comas |
| 2NF | No dependencia parcial de clave compuesta | PK surrogate (`uuid`), no claves compuestas artificiales |
| 3NF | No dependencia de columnas no clave | El dato se guarda en su tabla; si se duplica, decisión explícita |
| 4NF/5NF | Solo con análisis de dependencias multivaluadas | Raro; evaluar caso a caso |

## Reglas de decisión

1. Atributo derivable de otro dato → derivarlo en query o computar; no almacenar.
2. Mismo dato en dos tablas → normalizar o desnormalización documentada.
3. Agregación frecuente (contadores, totales) → índice + agregación SQL primero; read model desnormalizado solo si no escala.

## Decisiones de normalización (3NF)

### Catálogos clínicos del módulo de pacientes

Extraídos en la migración `NormalizeClinicalCatalogs`. La redundancia medida en los
datos de prueba justificó la extracción (no "por moda"):

| Columna original | Filas | Valores distintos | Repetición |
|---|---|---|---|
| `patient_allergies.allergen` | 45 417 | 10 | ~4 500× por alergeno |
| `patient_diagnoses.icd10_code` (+ `description`) | 69 947 | 46 | ~1 500× por código |
| `patient_medications.name` (+ `ndc`, `rx_norm`, `drug_class`) | 72 870 | 108 | ~675× por fármaco |

**Catálogos de referencia** en schema `app` (propiedad intrínseca del dato, no de
cada paciente):

- `app.allergens` (`name` único) — el alergeno es propiedad del catálogo.
- `app.icd10_codes` (`code` único) — la descripción es propiedad del **código**,
  no de cada diagnóstico; eliminó 2 875 diagnósticos con descripción nula.
- `app.medications` (`name` único) — NDC/RxNorm/clase farmacológica son del
  **fármaco**, no de cada prescripción.

**Tablas hijas modificadas** (FK a catálogos, `ON DELETE RESTRICT`):

- `patient_allergies`: `allergen_id → allergens(id)`; única `(patient_id, allergen_id)`.
- `patient_diagnoses`: `icd10_code_id → icd10_codes(id)`; se conserva `is_primary`.
- `patient_medications`: `medication_id → medications(id)`; se conservan
  `frequency` y `sort_order` (frecuencia y orden son específicos del paciente).

**Resolución de valores libres**: los valores libres que no existan en catálogo se
resuelven con `INSERT ... ON CONFLICT DO NOTHING` + releer la fila ganadora
(race-safe bajo concurrencia). La descripción/Ndc/RxNorm/clase solo se usan al
crear la fila del catálogo; las filas existentes **no** se sobrescriben.

**Migración**: backfill de catálogos desde `SELECT DISTINCT` + `gen_random_uuid()`;
las FKs se agregan nullable, se backfillean y luego se marcan `NOT NULL` (patrón
seguro con datos existentes); grants `SELECT, INSERT, UPDATE, DELETE` a `app_user`.

### Catálogos administrativos y geográficos (migración `AddPatientCatalogs`)

Segunda tanda de extracción, esta vez de valores que ya nacían del formulario
(tipo de documento, etnia, grupo sanguíneo) y de la geografía del paciente:

| Catálogo (`app.`) | Fuente original | Filas seed |
|---|---|---|
| `document_types` | texto libre en `patient_profiles.document_type` | 10 (SSN, licencias, pasaportes, etc.) |
| `ethnicities` | texto libre en `patient_profiles.ethnicity` | 9 (estándar OMB EE. UU.) |
| `blood_types` | texto libre en `patient_profiles.blood_type` | 8 (A+/A−/B+/B−/AB+/AB−/O+/O−) |
| `countries` | seed manual (86, incl. EE. UU.) | 86 |
| `states` | seed por país | 56 (EE. UU.) |
| `cities` | seed por estado | 305 |
| `postal_codes` | seed por ciudad | 25 (ZIPs de prueba) |

`patient_profiles` pasa a referenciarlos por FK: `document_type_id`,
`ethnicity_id`, `blood_type_id`, `country_id`, `state_id`, `city_id`. La columna
`phone` se sustituye por `phone_country_code` + `phone_number` (el país del
teléfono es dato del país, no texto del formulario). `marital_status` y el resto
de estilo de vida quedan como códigos estables en inglés con etiquetas en el
frontend.

**Backfill** (parte de la migración, 49 854 pacientes de prueba): cada fila se
une a su catálogo por nombre normalizado (`LOWER(TRIM(...))`) — 100 % de
cobertura en `ethnicity_id`/`blood_type_id`/`country_id`/`state_id`/`city_id`,
0 desajustes. Los valores de catálogo se escriben solo al crearlos (seed
idempotente); los del formulario siempre envían el `id` del catálogo
(`patient_input` con `*Id`), nunca el texto libre.

**Contrato de API actualizado**: los DTOs expiden tanto el `id` como el nombre
del catálogo (`documentTypeName`, `ethnicityName`, `bloodTypeName`,
`cityName`, `stateCode`, `countryName`, `phoneCountryCode`), y los endpoints de
catálogos (`GET /api/v1/catalogs/*`) alimentan los selects/autocompletes del
formulario (`cities?stateId=`, `postal-codes?cityId=`, `icd10-codes?search=`,
etc.).

## Registro de desnormalizaciones

| Problema | Motivo | Trade-off | Impacto lectura | Impacto escritura | Consistencia | Sincronización | Estrategia de actualización |
|---|---|---|---|---|---|---|---|
| — | — | — | — | — | — | — | — |

_(Vacío por esqueleto. Toda desnormalización futura se registra aquí — obligatorio.)_

## Regla de sincronización

- Una única estrategia por campo: misma transacción, evento de dominio + consumidor, o job background. Nunca trigger por defecto.
- Reconciliación periódica si el riesgo de divergencia lo amerita.
