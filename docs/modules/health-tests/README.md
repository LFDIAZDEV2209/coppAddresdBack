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

| Tabla                             | Descripción                                                                                                                                 |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `health_test_instruments`         | Catálogo de tests (código único, categoría, orden)                                                                                          |
| `health_test_versions`            | Versiones snapshot (número, estado draft/active/retired, estrategia, peso)                                                                  |
| `health_test_questions`           | Preguntas por versión (sección, tipo scale/single/multi/open/num, dirección, metadata num: unit/min/max/default, min_label/max_label, hint) |
| `health_test_answer_options`      | Opciones por pregunta con `score_value`                                                                                                     |
| `health_test_score_ranges`        | Rangos de interpretación por versión (etiqueta + severidad)                                                                                 |
| `health_test_batteries`           | Baterías configurables                                                                                                                      |
| `health_test_battery_items`       | Ítems de batería (instrumento + versión opcional + orden + obligatorio + periodicidad)                                                      |
| `health_test_battery_assignments` | Batería asignada a un paciente (agrupación semántica)                                                                                       |
| `health_test_assignments`         | Test asignado a un paciente (unidad de trabajo del ERP/mobile)                                                                              |
| `health_test_evaluations`         | Ejecución de una versión por un paciente (score + porcentaje)                                                                               |
| `health_test_responses`           | Respuestas del paciente a cada pregunta                                                                                                     |
| `health_test_results`             | Resultados calculados (score/subscale/indicator), append-only                                                                               |
| `health_test_indicator_defs`      | Definición de indicadores (`computation` jsonb)                                                                                             |
| `health_test_alert_rules`         | Reglas de alerta (`condition` jsonb)                                                                                                        |
| `health_test_alerts`              | Alertas generadas (estado active/reviewing/resolved/closed)                                                                                 |
| `health_test_comments`            | Comentarios de revisión del profesional                                                                                                     |
| `health_test_notification_templates` | Plantillas editables de notificación a pacientes (canal community/sms, alcance por severidad/categoría/indicador, cuerpo con placeholders) |
| `health_test_notification_template_versions` | Snapshot por versión de plantilla (historial + restauración)                                                                      |
| `health_test_notifications`       | Log de entregas a pacientes (alerta, canal, destinatario, cuerpo renderizado, plantilla, proveedor, estado queued/sent/failed/skipped, error) |

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

### ADR-008 — Filtro geográfico acumulado del dashboard (set-based + rollup snapshot, rev. 2026-09-15)

El dashboard ERP permite acotar las series, los KPIs y el mapa por **uno o varios estados**
(`state`, repetible, ej. `?state=NY&state=FL`: unión de zonas combinada en un solo conjunto de
pacientes) o por ciudad (`cityId`, con precedencia). El porcentaje de riesgo por ciudad se calcula
**solo sobre pacientes evaluados** (`evaluatedCount`): una ciudad con pacientes mapeados pero sin
evaluaciones devuelve `highRiskPct = null` ("Sin datos", gris) en lugar de un 0% verde engañoso.

**Revisión 2026-09-15** (antes era "lectura en vivo, sin pre-agregación"):

- `GET /stats?state=&cityId=`: **una consulta set-based** (`GetHealthTestStatsForZoneAsync` — CTE
  `zone` sobre `patient_profiles → cities → states` con EXISTS para el alcance `ViewOwn` + 5 conteos)
  en lugar de materializar la lista de GUIDs de la zona y lanzar 5 counts con IN list.
- `GET /master?state=&cityId=`: el JOIN por zona ocurre **en SQL**
  (`ListAssignmentsWithPatientDataForZoneAsync`); alertas activas por paciente y nombres de
  profesionales quedan acotados a la zona (antes eran agregados globales en memoria).
- `GET /geo`: lee el **rollup snapshot** `app.health_test_geo_rollups` (migración
  `AddHealthTestGeoRollups`), mantenido por backfill idempotente al arranque + recomputo incremental
  por ciudad en el processor de eventos; fallback a la agregación en memoria original si el rollup
  está vacío. Contrato (DTO) sin cambios.
- Nuevo `GET /coverage-trend`: últimos 12 meses leídos del rollup diario
  (`assignments_count/completed` global); el frontend lo usa sin filtro geo y conserva el cálculo
  cliente con filtro (master ya acotado).
- Índices: `ix_patient_profiles_city_id`, `ix_patient_profiles_state_id`,
  `ix_health_test_geo_rollups_state_code` (documentados en `docs/database/indexes.md`).
- La clave de caché de `GET /master`, `GET /stats` y `GET /coverage-trend` incluye el hash del filtro
  (`CacheKeys.HashScope(profesional|global, estados normalizados, cityId)`), por lo que zonas distintas
  no comparten caché ni exponen datos de otra zona.

Detalle completo del pipeline de analítica: `docs/modules/health-tests/analytics.md`.

### ADR-009 — Notificaciones a pacientes: plantillas versionadas + log de entregas (SPEC A13, rev. 2026-09-16)

Las alertas se comunican al paciente con **canales desacoplados** de la generación de la alerta:
`community` (mensaje directo en la app del paciente, vía Community) y `sms` (abstracción
`ISmsSender`, hoy `NoOpSmsSender` en Development). Decisiones:

- **Plantillas editables en BD** (`health_test_notification_templates`) con alcance opcional
  (severidad, categoría, indicador) e **historial de versiones** (`..._template_versions`): cada
  cambio de contenido crea una versión y la restauración se aplica como versión **nueva**
  (append-only, auditable). El borrado es lógico (`is_active = false`).
- **Todo envío se registra** en `health_test_notifications` (canal, destinatario, cuerpo renderizado,
  plantilla, proveedor, estado `queued/sent/failed/skipped`, error). El log alimenta el historial y
  los gráficos; las pruebas del Template Studio se registran con `alert_id = null`.
- **Contacto ausente ⇒ `skipped` con motivo** (sin teléfono / sin cuenta en la app); nunca bloquea el
  resto del envío masivo. `preview = true` no envía ni registra.
- **Community**: la entrega la hace el **backend** (`CommunityMessageSender` → endpoint interno
  `POST /api/internal/messages/direct` del servicio Community, protegido por `X-Internal-Key`), que
  escribe un `Message` del **perfil de sistema** al perfil del paciente mapeado por `Profile.UserId`
  y emite el `FeedEvent` correspondiente. Contingencia ERP: `features/community/erp-provider.tsx`.
- **SMS**: `ISmsSender` como abstracción; `NoOpSmsSender` en dev (marca `sent`, no llama a ningún
  proveedor). Un proveedor real (Twilio Messages) se conecta detrás sin cambiar el contrato.
- **Permiso propio** `HealthTests.Notify` (el envío va más allá de `HealthTests.Review`).
- **Analítica**: los gráficos de alertas/notificaciones se calculan al vuelo con consultas indexadas
  sobre `health_test_alerts` y `health_test_notifications` (volumen bajo, ventana de días). Se evaluó
  el modelo CQRS de pre-agregación (Channel Pattern) y **no aplica** por ahora: no se agrega un dato
  nuevo de conteo al dashboard que justifique un rollup; el rollup geo (`health_test_geo_rollups`)
  sigue siendo el único del módulo.

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
GET    /health-tests/master?state&state&cityId                  View/ViewOwn (tabla maestra del dashboard; unión de estados, cityId con precedencia)
GET    /health-tests/stats?state&state&cityId                   View/ViewOwn (KPIs/series del dashboard; unión de estados, cityId con precedencia)
GET    /health-tests/geo                                        View/ViewOwn (mapa de calor; SIEMPRE global; evaluatedCount + highRiskPct sobre evaluados)
```

### ERP — notificaciones de alertas (permiso `HealthTests.Notify`, SPEC A13)

```
GET    /health-tests/notification-templates?channel&search&isActive&page&pageSize   Notify
POST   /health-tests/notification-templates                                          Notify
GET    /health-tests/notification-templates/{id}                                     Notify
PUT    /health-tests/notification-templates/{id}                                     Notify   (crea versión si cambia el contenido)
DELETE /health-tests/notification-templates/{id}                                     Notify   (baja lógica: is_active=false)
POST   /health-tests/notification-templates/{id}/activate|deactivate                  Notify
POST   /health-tests/notification-templates/{id}/clone                                Notify
GET    /health-tests/notification-templates/{id}/versions                             Notify
POST   /health-tests/notification-templates/{id}/versions/{version}/restore           Notify
POST   /health-tests/notification-templates/{id}/test                                 Notify   (envío de prueba)
GET    /health-tests/notification-templates/{id}/preview?alertId&channel&bodyOverride  Notify   (render con datos reales de una alerta)
POST   /health-tests/alerts/notify                                                    Notify   (masivo; preview=true no envía ni registra)
GET    /health-tests/notifications?alertId&patientId&channel&status&from&to&page&pageSize   Notify
GET    /health-tests/notifications/charts?days                                        Notify
```

Placeholders del cuerpo: `[paciente]` `[documento]` `[test]` `[indicador]` `[valor]` `[umbral]`
`[severidad]` `[accion]` `[profesional]` `[fecha]` (corchetes; las llaves `{clave}` del formato
anterior se migraron con `20260916205732_ConvertNotificationPlaceholdersToBrackets`). Los
placeholders desconocidos se conservan literales y los valores nulos se sustituyen por vacío; la
severidad se rotula baja/media/alta/crítica y la fecha `dd/MM/yyyy`. La plantilla se elige
explícitamente o se autoselecciona (match por indicador > severidad > alcance nulo, y la más reciente).

`GET /notification-templates/{id}/preview` devuelve el render con **datos reales**: usa la alerta
indicada (o, sin `alertId`, la más reciente que tenga resultado asociado) y su paciente, e informa
`isReachable` + `skipReason` del canal y `missingPlaceholders` (datos que la alerta no puede rellenar,
p. ej. `[accion]`). El frontend lo usa tanto en el Template Studio (selector de paciente de ejemplo)
como en el asistente de envío.

**Cuentas de paciente y entrega por comunidad (dev)**: `PatientAccountDemoSeeder` (servicio Auth,
config `PatientAccountDemo` con `Enabled`/`Password`/`MaxAccounts`, no-op en prod) crea las cuentas
`auth.users` deterministas (MD5 del documento, app `app`) de los pacientes sin cuenta y las de los
perfiles de comunidad huérfanos. Como un paciente puede tener cuenta sin haber abierto nunca la app,
el endpoint interno `POST /api/internal/messages/direct` **auto-provisiona** el perfil de comunidad
(`Profile{UserId, DisplayName, Status=Active}`) cuando no existe, igual que la auto-provisión de
`CommunityQuery.Me`; así la notificación clínica no se pierde por onboarding pendiente.


Frontend: `/health-tests/alertas` (selección múltiple + asistente de envío en 3 pasos con resultados
por paciente, filtros por indicador/severidad/estado/fechas, gráficos e historial de entregas) y
`/health-tests/alertas/plantillas` (Template Studio: galería, editor con chips de placeholders,
previsualización SMS/Comunidad, versiones + restauración, clonado y envío de prueba).

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
resumen con 6 scores), de modo que la UX mobile no cambia al conectar el backend. El render mobile es
un wizard de **una pregunta a la vez**: escala y selección única avanzan automáticamente al responder;
multi/num/texto requieren el botón Continuar. Las preguntas `num` (biometría) responden por
`value_text` y no puntúan.

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

**Fuente de verdad del seed**: `scripts/generate_health_tests_seed.py` — contenido **fiel a
`ANTARES_Tests_Perfil_Salud (1).html`** (preguntas, opciones, secciones, hints y biometría con
unidad/rango; espejo en `antares-paciente/src/data/tests.ts` para el modo demo de la app).
Editar el script, regenerar el SQL, nunca editar el SQL a mano.

**Reemplazo del contenido inicial** (p. ej. al cambiar las preguntas del onboarding): la migración
`ReplaceInitialBatterySeed` borra en orden FK-safe evaluaciones/respuestas/asignaciones y
preguntas/opciones/rangos de la v1 de los 9 instrumentos y vuelve a sembrar el recurso regenerado
(los instrumentos/versiones/batería/indicadores/reglas conservan su identidad). Es una operación
**destructiva** sobre los datos de la batería (solo aplica en la transición del contenido).

**Datos de demostración del dashboard (solo Development)**: `HealthTestsDemoSeeder`
(`src/CoppAddresd.Api/Seeders/`) siembra al arrancar la API, solo en Development, pacientes
evaluados y pendientes repartidos por estados de EE. UU. (40 estados con evaluaciones y 10 sin
ninguna para el "Sin datos" del mapa; ~459 evaluaciones con severidades, ~70 alertas activas y
fechas repartidas en los últimos 12 meses). Es idempotente (omite el seed si ya existen
asignaciones) y no se registra fuera de Development; no reemplaza el seed del catálogo.

## Cómo extender

- **Nuevo test**: INSERT instrumento + versión + preguntas + opciones + rangos (datos).
- **Nuevo test con scoring distinto**: usar una estrategia existente o agregar una clase
  `IScoreStrategy` + test (código acotado).
- **Cambiar rangos/severidad**: UPDATE `health_test_score_ranges` (afecta solo evaluaciones nuevas).
- **Nuevo indicador**: INSERT `health_test_indicator_defs` con `computation` válido.
- **Nueva regla de alerta**: INSERT `health_test_alert_rules` con `condition` válido.
- **Nueva plantilla de notificación**: crearla en el Template Studio (o INSERT en
  `health_test_notification_templates` + su versión 1); los placeholders disponibles están en
  `HealthTestTemplateRenderer.Placeholders`.
- **Nuevo canal de notificación**: implementar `ISmsSender`/`ICommunityMessageSender` (o un nuevo
  contrato) + registrar en DI; agregar el miembro al enum `NotificationChannel` y a los mapas del
  frontend. El log y el asistente no cambian.
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
