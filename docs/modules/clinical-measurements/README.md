# Módulo: Mediciones Clínicas (Clinical Measurements)

Skill: `documentation`. Módulo para persistir mediciones clínicas genéricas de pacientes
(monitoreo continuo + registro del profesional en consultas periódicas).

## Objetivo

Registrar mediciones clínicas de forma **genérica** (métrica + unidad + valor) con:

- **Monitoreo continuo**: lecturas de dispositivos/wearables y auto-reporte del paciente, sin encounter.
- **Consultas periódicas**: mediciones registradas por el profesional durante un encounter.

Modelo en tres capas: **catálogos** (qué se mide y en qué unidad) + **encounter canónico**
(contenedor de la visita) + **mediciones** (el valor con su contexto temporal y de origen).

## Alcance (esta iteración)

- ✅ Entidades + configuraciones EF + migración del core (schema `app`)
- ✅ Columna `encounter_id` + FK en `tele.clinical_encounters` (migración del módulo tele)
- ✅ Seed de catálogo: unidades + métricas + rangos de referencia iniciales
- ❌ DTOs, endpoints, frontend (fase posterior)
- ❌ Métricas `test_score` (cuando se conecten los tests)

## Decisiones de arquitectura (ADR)

### ADR-001 — Encounter canónico nuevo, tele linkeado (NO mover ClinicalEncounter)

`ClinicalEncounter` (módulo tele) ya es un encounter en espíritu (patient, professional, date,
status, notes, `clinical_data` JSONB), pero nació **atado** a `TelemedicineAppointment` (1:1,
`AppointmentId` obligatorio) y vive en un proyecto separado con **DbContext e historial de
migraciones propios** (`tele.__ef_migrations_history`).

Opciones evaluadas:

| Opción                                        | Costo                                        | Riesgo |
| --------------------------------------------- | -------------------------------------------- | ------ |
| Mover ClinicalEncounter al core               | Refactor ~20 archivos + migraciones cruzadas | Medio  |
| **Encounter canónico nuevo + link (elegida)** | Bajo, tele intacto                           | Bajo   |

**Decisión**: crear `app.encounters` como encounter canónico en el core. El de tele **queda
intacto** y se **linkea** con FK nullable `encounter_id` → `app.encounters.id`. No es duplicación:
es especialización — el de tele conserva su detalle de sesión (appointment/session), el canónico
es el registro clínico general que usan las mediciones.

**Regla crítica de frontera**: la FK `tele.clinical_encounters.encounter_id` se crea en una
migración del **módulo tele** (su propio historial), NUNCA del core. El core define la entidad
canónica; el tele solo agrega la columna que apunta a ella.

### ADR-002 — Observación con dos timestamps

`observed_at` (cuándo se tomó la medición) y `recorded_at` (cuándo se registró en el sistema).
Difieren cuando el profesional carga mediciones históricas en una consulta.

### ADR-003 — Rangos de referencia por edad/género en tabla separada

Los rangos normales varían por edad y género, por eso no viven en la métrica sino en
`measurement_reference_ranges` con `age_min`/`age_max`/`gender` nullable + `priority`.

### ADR-004 — Encounter canónico liviano

El canónico NO replica el `clinical_data` JSONB del tele. Queda estructurado y liviano
(`reason` + `notes`). El detalle clínico rico permanece solo en tele.

## Entidades (schema `app`)

### `unit_of_measures`

| Campo       | Tipo         | Notas                               |
| ----------- | ------------ | ----------------------------------- |
| `id`        | uuid PK      | default `gen_random_uuid()`         |
| `code`      | varchar(50)  | único — `mg/dL`, `kg`, `cm`, `mmHg` |
| `name`      | varchar(150) |                                     |
| `symbol`    | varchar(20)  |                                     |
| `is_active` | bool         | default `true`                      |

### `measurement_metrics`

| Campo             | Tipo                         | Notas                                                          |
| ----------------- | ---------------------------- | -------------------------------------------------------------- |
| `id`              | uuid PK                      | default `gen_random_uuid()`                                    |
| `code`            | varchar(50)                  | único — `glucose_fasting`, `weight`, `systolic_bp`             |
| `name`            | varchar(150)                 |                                                                |
| `description`     | text?                        |                                                                |
| `default_unit_id` | uuid FK → `unit_of_measures` | Restrict                                                       |
| `category`        | varchar(50)                  | `vital` \| `metabolic` \| `body_comp` \| `test_score` (futuro) |
| `is_active`       | bool                         | default `true`                                                 |

Índices: `ix_measurement_metrics_code` (único), `ix_measurement_metrics_default_unit_id`.

### `measurement_reference_ranges`

| Campo                     | Tipo                            | Notas                                             |
| ------------------------- | ------------------------------- | ------------------------------------------------- |
| `id`                      | uuid PK                         | default `gen_random_uuid()`                       |
| `metric_id`               | uuid FK → `measurement_metrics` | Cascade                                           |
| `age_min` / `age_max`     | int?                            | null = todos                                      |
| `gender`                  | varchar(1)?                     | `M` / `F` / `X`, null = todos                     |
| `min_value` / `max_value` | decimal?                        |                                                   |
| `unit_id`                 | uuid FK → `unit_of_measures`    | Restrict                                          |
| `priority`                | int                             | default `0` — resuelve solapamientos (mayor gana) |
| `notes`                   | text?                           |                                                   |
| `is_active`               | bool                            | default `true`                                    |

Índices: `ix_measurement_reference_ranges_metric_id`.

### `encounters` (canónico)

| Campo                     | Tipo                         | Notas                                                                       |
| ------------------------- | ---------------------------- | --------------------------------------------------------------------------- |
| `id`                      | uuid PK                      | default `gen_random_uuid()`                                                 |
| `patient_id`              | uuid FK → `patient_profiles` | Cascade                                                                     |
| `professional_id`         | uuid FK → `professionals`    | Restrict                                                                    |
| `type`                    | varchar(30)                  | `consulta_periodica` \| `telemedicina` \| `seguimiento`                     |
| `status`                  | varchar(20)                  | `planned` \| `in_progress` \| `completed` \| `cancelled`, default `planned` |
| `started_at` / `ended_at` | timestamptz?                 |                                                                             |
| `reason`                  | text?                        | motivo de consulta                                                          |
| `notes`                   | text?                        |                                                                             |
| `created_by`              | uuid?                        |                                                                             |
| `created_at`              | timestamptz                  | default `now()`                                                             |
| `updated_at`              | timestamptz?                 |                                                                             |

Índices: `ix_encounters_patient_id`, `ix_encounters_professional_id`, `ix_encounters_status`,
`ix_encounters_type`.

### `clinical_measurements`

| Campo          | Tipo                            | Notas                                            |
| -------------- | ------------------------------- | ------------------------------------------------ |
| `id`           | uuid PK                         | default `gen_random_uuid()`                      |
| `patient_id`   | uuid FK → `patient_profiles`    | Cascade                                          |
| `metric_id`    | uuid FK → `measurement_metrics` | Restrict                                         |
| `encounter_id` | uuid FK → `encounters`          | **NULL = monitoreo autónomo**, SetNull           |
| `value`        | decimal                         |                                                  |
| `unit_id`      | uuid FK → `unit_of_measures`    | Restrict                                         |
| `observed_at`  | timestamptz                     | cuándo se tomó                                   |
| `recorded_at`  | timestamptz                     | default `now()` — cuándo se registró             |
| `source`       | varchar(20)                     | `device` \| `patient` \| `professional` \| `lab` |
| `notes`        | text?                           |                                                  |
| `created_by`   | uuid?                           |                                                  |
| `created_at`   | timestamptz                     | default `now()`                                  |

Índices: `ix_clinical_measurements_patient_id`, `ix_clinical_measurements_metric_id`,
`ix_clinical_measurements_encounter_id`, `ix_clinical_measurements_patient_observed`
(`patient_id`, `observed_at` DESC) — para el "último valor por métrica" y series temporales.

## Cambio en módulo tele

`tele.clinical_encounters` + columna:

| Campo          | Tipo  | Notas                                 |
| -------------- | ----- | ------------------------------------- |
| `encounter_id` | uuid? | FK → `app.encounters.id`, **SetNull** |

- La entidad `ClinicalEncounter` del módulo tele gana `public Guid? EncounterId { get; set; }`
  y navegación `public Encounter? Encounter { get; set; }` (tipo del core).
- ⚠️ El módulo tele actualmente **no referencia otros proyectos**. Esta navegación requiere
  agregar referencia de proyecto a `CoppAddresd.Domain`. **Decisión pendiente de validación en
  apply**: si la referencia rompe el aislamiento, la alternativa es dejar `EncounterId` como
  `Guid?` sin navegación (FK por convención en configuración).

## Migraciones

| Contexto                       | Historial                      | Contenido                                                                     |
| ------------------------------ | ------------------------------ | ----------------------------------------------------------------------------- |
| `AppDbContext` (core)          | `public.__EFMigrationsHistory` | crear 5 tablas (schema `app`)                                                 |
| `TelemedicineDbContext` (tele) | `tele.__ef_migrations_history` | `ALTER TABLE tele.clinical_encounters ADD COLUMN encounter_id uuid NULL` + FK |

## Seed (catálogo)

Doble vía, ambas idempotentes:

1. **Migración `AddServerCatalogSeeds`** (`public.__EFMigrationsHistory`): recurso embebido
   `Migrations/Seed/AddServerCatalogs.sql` (generado por
   `scripts/generate_server_catalogs_seed.py`, no editar a mano). Siembra unidades, métricas,
   rangos, alérgenos, ICD-10, medicamentos, organización `medicare` y aseguradoras al correr
   `dotnet ef database update` — garantiza el catálogo en el servidor y en clones sin arrancar
   la API. `ON CONFLICT DO NOTHING` (los rangos usan guard `WHERE NOT EXISTS`, no tienen clave única).
2. **`ClinicalMeasurementsSeeder`** (`IHostedService` en `CoppAddresd.Api/Seeders`, como
   `AgentCatalogSeeder`): quedó como defensa en profundidad; al arrancar detecta el catálogo ya
   sembrado y no duplica (idempotente por `code`).

### Unidades iniciales

| code    | name                          | symbol |
| ------- | ----------------------------- | ------ |
| `mg_dl` | Miligramos por decilitro      | mg/dL  |
| `kg`    | Kilogramos                    | kg     |
| `cm`    | Centímetros                   | cm     |
| `mmhg`  | Milímetros de mercurio        | mmHg   |
| `bpm`   | Latidos por minuto            | bpm    |
| `pct`   | Porcentaje                    | %      |
| `kg_m2` | Kilogramos por metro cuadrado | kg/m²  |

### Métricas iniciales

| code              | name                         | category  | unidad |
| ----------------- | ---------------------------- | --------- | ------ |
| `glucose_fasting` | Glucosa en ayunas            | metabolic | mg/dL  |
| `weight`          | Peso                         | body_comp | kg     |
| `height`          | Talla                        | body_comp | cm     |
| `systolic_bp`     | Presión arterial sistólica   | vital     | mmHg   |
| `diastolic_bp`    | Presión arterial diastólica  | vital     | mmHg   |
| `heart_rate`      | Frecuencia cardíaca          | vital     | bpm    |
| `bmi`             | Índice de masa corporal      | body_comp | kg/m²  |
| `body_fat`        | Porcentaje de grasa corporal | body_comp | %      |

### Rangos de referencia iniciales (referencia ADA/OMS, a validar clínicamente)

| métrica           | edad  | género | min  | max  | prioridad |
| ----------------- | ----- | ------ | ---- | ---- | --------- |
| `glucose_fasting` | todos | todos  | 70   | 99   | 0         |
| `systolic_bp`     | todos | todos  | 90   | 120  | 0         |
| `diastolic_bp`    | todos | todos  | 60   | 80   | 0         |
| `bmi`             | todos | todos  | 18.5 | 24.9 | 0         |
| `heart_rate`      | todos | todos  | 60   | 100  | 0         |

## Fuera de alcance (fases futuras)

- DTOs + endpoints (registrar/consultar mediciones y encounters)
- Conexión de los tests de `antares-paciente` (métricas `test_score`)
- Frontend `coppaddresd-front` / `antares-paciente`

## Riesgos

- **Frontera tele/core**: la FK debe ir en migración del tele. No cruzar historiales.
- **Referencia de proyecto tele → Domain**: validar que no rompa el aislamiento del módulo.
  Fallback: `EncounterId` sin navegación.
- **Volumen de monitoreo continuo**: mediciones puntuales por ahora; series temporales agregadas
  serían una fase posterior (vista derivada, no duplicada).
