# Módulo: Tests de Salud (Health Tests)

Skill: `documentation`. Módulo del ERP para la evaluación de pacientes con **instrumentos
psicométricos/clínicos versionados** (tests), baterías configurables, asignaciones,
evaluaciones, resultados, indicadores derivados y alertas clínicas. Lo consumen el **ERP**
(configuración, asignación, monitoreo) y la **app móvil** (ejecución de la batería inicial ANTARES
y resultados).

Spec de origen: `openspec/specs/pacientes/health-tests/spec.md` (fuente de verdad del negocio).

## Objetivo

Modelar la batería de evaluación inicial del programa (9 tests ANTARES) y permitir crecer hacia
**cualquier** test/batería/indicador/regla **sin migraciones estructurales**:

- **Catálogo versionado por snapshot**: instrumento → versiones inmutables → preguntas → opciones.
- **Baterías configurables**: conjuntos de tests con orden, obligatoriedad y periodicidad.
- **Ejecución**: asignaciones por test, evaluaciones con respuestas, scoring por estrategia,
  resultados snapshot (append-only), indicadores y alertas derivados.
- **Scoring/interpretación/reglas por datos**: crear un test nuevo, cambiar rangos, definir un
  indicador o una regla de alerta es **DML**, no código.

## Conceptos (no mezclar)

| Concepto           | Entidad                  | Rol                                                                                                |
| ------------------ | ------------------------ | -------------------------------------------------------------------------------------------------- |
| Instrumento (Test) | `HealthTestInstrument`   | El test a nivel lógico, reutilizable por baterías                                                  |
| Versión            | `HealthTestVersion`      | **Snapshot inmutable** de preguntas/opciones/scoring/rangos; las evaluaciones apuntan SIEMPRE aquí |
| Pregunta           | `HealthTestQuestion`     | Pertenece a una versión; tiene sección (subescala), tipo y dirección de scoring                    |
| Opción             | `HealthTestAnswerOption` | Lleva `score_value` (el scoring es configuración)                                                  |
| Batería            | `HealthTestBattery`      | Conjunto configurable de tests                                                                     |
| Asignación         | `HealthTestAssignment`   | Test (versión) asignado a un paciente, con estado y fechas                                         |
| Evaluación         | `HealthTestEvaluation`   | Una ejecución concreta de una versión por un paciente                                              |
| Respuesta          | `HealthTestResponse`     | La respuesta a una pregunta (opción y/o texto)                                                     |
| Resultado          | `HealthTestResult`       | Score/subescala/indicador calculado, snapshot inmutable                                            |
| Indicador          | `HealthTestIndicatorDef` | Fórmula configurable sobre resultados                                                              |
| Regla de alerta    | `HealthTestAlertRule`    | Condición configurable → alerta                                                                    |
| Alerta             | `HealthTestAlert`        | Condición que requiere atención, con ciclo de vida                                                 |

## Tablas (schema `app.`)

| Tabla                             | Descripción                                                                            |
| --------------------------------- | -------------------------------------------------------------------------------------- |
| `health_test_instruments`         | Catálogo de tests (código único, categoría, orden)                                     |
| `health_test_versions`            | Versiones snapshot (número, estado draft/active/retired, estrategia, peso)             |
| `health_test_questions`           | Preguntas por versión (sección, tipo scale/single/multi/open, dirección)               |
| `health_test_answer_options`      | Opciones por pregunta con `score_value`                                                |
| `health_test_score_ranges`        | Rangos de interpretación por versión (etiqueta + severidad)                            |
| `health_test_batteries`           | Baterías configurables                                                                 |
| `health_test_battery_items`       | Ítems de batería (instrumento + versión opcional + orden + obligatorio + periodicidad) |
| `health_test_battery_assignments` | Batería asignada a un paciente (agrupación semántica)                                  |
| `health_test_assignments`         | Test asignado a un paciente (unidad de trabajo del ERP/mobile)                         |
| `health_test_evaluations`         | Ejecución de una versión por un paciente (score + porcentaje)                          |
| `health_test_responses`           | Respuestas del paciente a cada pregunta                                                |
| `health_test_results`             | Resultados calculados (score/subscale/indicator), append-only                          |
| `health_test_indicator_defs`      | Definición de indicadores (`computation` jsonb)                                        |
| `health_test_alert_rules`         | Reglas de alerta (`condition` jsonb)                                                   |
| `health_test_alerts`              | Alertas generadas (estado active/reviewing/resolved/closed)                            |
| `health_test_comments`            | Comentarios de revisión del profesional                                                |

Estados: instrumento/versión `draft/active/retired` · asignación `pending/in_progress/completed/expired/cancelled` ·
evaluación `started/completed/abandoned` · alerta `active/reviewing/resolved/closed`. Se almacenan como
`varchar` (configurables por datos, no enums cerrados), salvo la estrategia de scoring que sí es enum
registrado en código.

## Decisiones de arquitectura (ADR)

### ADR-001 — Versionado por snapshot de instrumento (no por pregunta)

Cada instrumento tiene versiones inmutables; preguntas/opciones/estrategia/rangos cuelgan de la
versión; las evaluaciones apuntan siempre a `version_id`. Publicar = `draft → active` (retira la
anterior); modificar = clonar a `N+1`. Los históricos quedan intactos por construcción.

- Alternativa rechazada: versionar pregunta por pregunta — explota el grafo de dependencias sin
  beneficio real (el cliente cambia instrumentos completos).
- Alternativa rechazada: edición in-place con `is_active` — rompe históricos.

### ADR-002 — Scoring híbrido: configuración en BD + estrategias registradas

Las opciones llevan `score_value`; las preguntas llevan dirección (`positive`/`reverse`); la versión
declara `scoring_strategy`. Un registry DI (`ScoreStrategyRegistry`) resuelve la implementación por
código. Estrategias actuales: `sum`, `percentage`, `subscale` (por sección), `inventory` (conteo de
señales multi), `weighted` (0-100 por sección, pesos iguales).

- Alternativa rechazada: Rules Engine externo — sobreingeniería para 5 estrategias.
- Alternativa rechazada: todo hardcode en handlers — cada test nuevo exigiría código.
- **Crear un test nuevo = solo datos.** **Nueva estrategia = una clase + test unitario.**

### ADR-003 — Resultados como snapshot persistido (append-only)

Al completar se persisten score total, porcentaje, subescalas e indicadores en `health_test_results`
(`result_type` = `score|subscale|indicator`). Derivado persistido **a propósito**: el histórico de una
evaluación no se re-computa si las reglas/rangos cambian después. Las agregaciones poblacionales
(cobertura, promedios, DOFA) se calculan en lectura, no se almacenan.

### ADR-004 — Indicadores y alertas como configuración JSON + motores en código

- `indicator_defs.computation` (jsonb): `{"formula":"weighted|sum|avg","sources":[{"resultType","code","weight"}]}`.
- `alert_rules.condition` (jsonb): `{"when":{"resultType","code","severity":[]}}` + `message_template`
  con placeholders `{code}/{value}/{label}/{severity}`.
- Los motores (`IndicatorEngine`, `AlertEngine`) evalúan en el submit (transacción). Deduplicación de
  alertas por `(result_id, rule_id)`.
- **Nuevo indicador/regla = INSERT en BD.** Fórmula nueva = clase + test.

### ADR-005 — Asignación a nivel test + batería opcional

`health_test_assignments` (test) con `battery_assignment_id` opcional a
`health_test_battery_assignments` (batería). Soporta asignar un test suelto (ERP), una batería
completa (mobile/onboarding) y el "seguimiento por test pendiente" del ERP.

### ADR-006 — Permisos sin acoplar el modelo a un rol

`HealthTests.Manage` (catálogo), `Assign` (asignación), `Review` (alertas), `View`/`ViewOwn`
(lectura global/propia). El alcance `ViewOwn` se resuelve vía `patient_professionals` +
`ICurrentContext.GetProfessionalIdAsync()` (el profesional SIEMPRE viene del JWT). Mobile: JWT
`aud=app`, paciente por `patient_profiles.user_id`, 404 a recursos ajenos.

### ADR-007 — Auto-asignación de la batería inicial al crear el paciente

La batería con `auto_assign_on_patient_create = true` (la inicial ANTARES) se asigna automáticamente
a cada paciente recién creado (estado `Pending`), sin duplicar si ya existe una asignación
pendiente/en curso. El profesional puede asignar más baterías manualmente.

## Flujo de datos

```
ERP (config):  instrumento → versión → preguntas → opciones → rangos
               batería → ítems → asignación (test/batería, individual o masiva)
Backend:       valida → persiste (schema app.)
Mobile:        GET /me/assignments → GET /me/tests/{id} → POST start → POST submit
Backend:       transacción → evaluation + responses + scoring → results
               → indicadores → alert_rules → alerts
ERP:           GET paciente → evaluaciones → resultados → indicadores → alertas
               → gestiona alertas (review/resolve/close) → comentarios
```

Ambas aplicaciones consumen la misma fuente de verdad (`app.patient_profiles` + módulo de tests).
Correlación por **UUID del paciente**: la mobile lo resuelve del JWT (`patient_profiles.user_id`),
el ERP del directorio.

## API

### ERP — `/api/v1/health-tests` (permisos `HealthTests.*`)

```
GET    /health-tests?search&category&isActive&page&pageSize     View
POST   /health-tests                                            Manage
GET    /health-tests/{id}                                       View
PUT    /health-tests/{id}                                       Manage
GET    /health-tests/{id}/versions                              View
POST   /health-tests/{id}/versions                              Manage        (crear versión draft)
POST   /health-tests/versions/{id}/publish                      Manage        (draft → active)
POST   /health-tests/versions/{id}/retire                       Manage
POST   /health-tests/versions/{id}/clone                        Manage        (N+1 copiando la origen)
GET    /health-tests/versions/{id}/questions                    View
GET    /health-tests/batteries?search&isActive&page             View
POST   /health-tests/batteries                                  Manage
GET    /health-tests/assignments?patientId&status&page          View/ViewOwn (scoped)
POST   /health-tests/assignments                                Assign
POST   /health-tests/batteries/{id}/assign                      Assign        (masiva)
POST   /health-tests/assignments/{id}/cancel                    Assign
GET    /health-tests/patients/{id}/evaluations?page&pageSize&status&from&to&category   View/ViewOwn (scoped, paginado)
GET    /health-tests/patients/{id}/evaluations/{evaluationId}   View/ViewOwn (scoped)
GET    /health-tests/patients/{id}/evaluations/{evaluationId}/comments   View/ViewOwn (scoped)
GET    /health-tests/patients/{id}/results                      View/ViewOwn (scoped; TODAS las evaluaciones, orden desc)
GET    /health-tests/indicators                                 View
GET    /health-tests/alerts?patientId&status&severity&page      View/ViewOwn (scoped)
POST   /health-tests/alerts/{id}/review|resolve|close           Review
POST   /health-tests/comments                                   Review
GET    /health-tests/stats                                      View/ViewOwn (dashboard ERP)
```

### Mobile — `/api/v1/health-tests/me` (JWT `aud=app`, paciente por `user_id`)

```
GET  /me/assignments    tests pendientes, en curso y completados (shape TestMeta); no incluye cancelled/expired
GET  /me/batteries      baterías asignadas con progreso
GET  /me/tests/{id}     preguntas de la versión (shape ScaleQ + opciones)
POST /me/tests/{id}/start      → evaluación Started
POST /me/tests/{id}/submit     → transacción + scoring + resultados + alertas
GET  /me/results        scores por dimensión + tipificación (derivación determinista)
GET  /me/history        historial de evaluaciones (evolución)
```

Los shapes `/me/*` replican los de `antares-paciente` (`TestMeta`, `ScaleQ`, respuestas por opción,
resumen con 6 scores), de modo que la UX mobile no cambia al conectar el backend.

En la batería inicial, un test completado **sigue en la lista** (estado `completed`, badge
«Hecho»); no se oculta. Tras **al menos 3** evaluaciones completadas, la app permite omitir el
resto o continuar después.

### Detalle de evaluación (hub del paciente ERP)

`GET /health-tests/patients/{id}/evaluations/{evaluationId}` devuelve el detalle completo de una
evaluación: test (nombre/código/categoría), versión snapshot (número, nombre, estrategia de scoring),
estado, fechas, **número de intento** (orden cronológico entre evaluaciones del mismo test del
paciente), score/porcentaje, resultados persistidos (score/subescala/indicador con severidad),
respuestas por pregunta (texto de pregunta, sección, opción elegida con su texto y valor, o texto
libre), comentarios de la evaluación e **intentos del mismo test** (`attempts`) para la comparativa
histórica. 404 si la evaluación no existe o pertenece a otro paciente.

`GET /health-tests/patients/{id}/evaluations` está paginado (`page`/`pageSize`, default 20, máx 100)
y acepta filtros opcionales `status`, `from`/`to` (rango de `completed_at`) y `category`.

`GET /health-tests/patients/{id}/results` devuelve los resultados de **todas** las evaluaciones del
paciente ordenados por fecha de finalización desc (fix del bug que solo devolvía el primer intento y
respondía 500 sin evaluaciones).

## Seed del catálogo ANTARES

Los 9 instrumentos de la batería inicial (historia clínica, temperamento, nutricional, movimiento,
sueño, IAC-ADRESD, ORP, ERS, batería ANTARES), sus preguntas, opciones con `score_value`, rangos,
la batería inicial, los indicadores (IAC-ADRESD, sospecha de apnea) y las reglas de alerta se siembran
con la migración `AddHealthTestsCatalogSeed` (SQL embebido, idempotente).

**Fuente de verdad del seed**: `scripts/generate_health_tests_seed.py` (fiel a
`antares-paciente/src/data/tests.ts`). Editar el script, regenerar el SQL, nunca editar el SQL a mano.

## Cómo extender

- **Nuevo test**: INSERT instrumento + versión + preguntas + opciones + rangos (datos).
- **Nuevo test con scoring distinto**: usar una estrategia existente o agregar una clase
  `IScoreStrategy` + test (código acotado).
- **Cambiar rangos/severidad**: UPDATE `health_test_score_ranges` (afecta solo evaluaciones nuevas).
- **Nuevo indicador**: INSERT `health_test_indicator_defs` con `computation` válido.
- **Nueva regla de alerta**: INSERT `health_test_alert_rules` con `condition` válido.
- **Preguntas condicionales (futuro)**: campos `depends_on_question_id`/`depends_on_option_id` ya
  previstos en las opciones (sin migración).
- **Repetir un test cada N días**: `frequency_days` en `health_test_battery_items` (re-asignación
  futura por job).
- **Nueva batería para un grupo**: asignación masiva `POST /batteries/{id}/assign` con los
  destinatarios.

## Validación E2E

La validación integral del módulo (9 tests con Playwright, casos de error, ERP, consistencia,
rendimiento con volumen sintético, concurrencia y extensibilidad) está documentada en
`docs/modules/health-tests/VALIDACION.md`. Cambios derivados de la validación:

- El resultado `score` usa el **código del instrumento** (ej: `orp`) como `code` (antes `score_total`)
  para que las reglas de alerta lo referencien naturalmente.
- Índice `ix_health_test_results_type_severity` (migración `AddHealthTestResultsSeverityIndex`) para
  la agregación por severidad del dashboard.
- Mobile: render genérico de preguntas del backend (escala/multi/open), acceso a la batería desde el
  Perfil, y corrección del bug de `openId=0`.

## Fronteras / fuera de alcance

- NO hay volcado de resultados a `app.clinical_measurements` todavía (la categoría `test_score` está
  reservada; decisión diferida y documentada en el spec).
- NO hay narrativa IA del resultado (el `/me/results` es derivación determinista; la narrativa con AI
  Service es iteración futura sin cambio de contrato).
- Los índices están documentados en `docs/database/indexes.md` (migración `AddHealthTestsModule`).
