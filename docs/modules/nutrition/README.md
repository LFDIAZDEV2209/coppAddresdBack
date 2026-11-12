# Módulo: Nutrición — generación, revisión clínica y lectura del paciente

## Cambio vigente: publicación automática (KAN-61, 2026-11-12)

Este requisito sustituye la aprobación profesional obligatoria descrita en las olas históricas de este documento. Al completar la batería inicial configurada, el worker genera y valida el mes, publica con el actor técnico `system:nutrition` y después marca la reserva como lista. No solicita `Nutrition.Approve` ni simula una revisión clínica. Conserva validaciones estructurales, versiones, vigencia de 30 días, exclusión de solapamientos y auditoría. El estado persistido `aprobado` significa publicado por compatibilidad del contrato; el nuevo evento es `publish`, con `AutoApproved=true` para el actor técnico y sin `CriticalConfirmed`.

`POST /prescriptions/{id}/publish` permite publicar un borrador válido con `Nutrition.Manage`, alcance y revisión vigente. Los endpoints de revisión anteriores se conservan para compatibilidad. La reconciliación recupera reservas técnicas listas con planes no publicados y generaciones legacy completas pendientes de revisión; un plan incompleto o una generación fallida no se presenta como disponible. El aviso íntegro reside en `NutritionRecommendationNotice.Text`; se incluye en la lectura móvil, contexto de agentes y snapshot de auditoría. App y ERP muestran el texto exacto. El contexto del agente incluye además fecha inicial/final y día actual; entregar el aviso en WhatsApp requiere que su integración consuma ese contexto.

Verificación local: Playwright usa APIs sintéticas para las pantallas, sin datos clínicos reales. Las pruebas de `NutritionPlanWorkflowTests` usan PostgreSQL aislado y sustituyen el proveedor IA. No acredita una prueba del canal WhatsApp ni publicación en producción.


### Horario individual de comidas en la app

La lectura `GET /api/v1/me/nutrition-plan` añade el campo opcional
`days[].meals[].suggestedTime` (`HH:mm`) a partir del horario persistido en
las prescripciones publicadas. Conserva `Snack` como tipo de merienda y
`sortOrder` como orden del menú. En planes legacy, que solo conservan un
horario general, el campo individual es `null`: la app no asigna la lista
completa de horarios a cada comida ni infiere uno. Es un cambio aditivo de
lectura; no requiere migración, no altera dietas aprobadas ni añade tiempos
de comida al motor. Las meriendas ausentes se muestran como pendientes de
indicación profesional, sin proponer alimentos automáticamente.

### Indicaciones bilingües en la lectura móvil

La consulta de `GET /api/v1/me/nutrition-plan` conserva la fuente de
`days[].meals[].notes` y expone `notesEn` desde `NutritionPrescription.InstructionsEn`
solo cuando la comida usa la indicación general (`meal.Notes == null`) y
`InstructionsEnSourceHash` coincide con el SHA-256 UTF-8 de la fuente exacta.
Una nota específica, incluso vacía, nunca recibe la traducción general.
Las parejas históricas sin hash o con hash distinto devuelven `notesEn: null`;
en una lectura inglesa el filtro traduce una copia de la fuente vigente.
La app prefiere la variante inglesa verificada disponible. La migración
`AddNutritionInstructionTranslationProvenance` añade únicamente una columna
nullable de 64 caracteres, sin backfill ni cambios a las indicaciones existentes.
Véase [procedencia y ediciones](instructions-localization.md).

## Ola 2: motor y prescripciones profesionales

Los catálogos de Auth y API conservan los permisos legacy y añaden
`Nutrition.Manage` (generar/editar/versionar/enviar/regenerar) y
`Nutrition.Approve` (aprobar/devolver). Ambos permiten leer bajo alcance. El
rol funcional `NutritionReviewer` recibe ambas capacidades y mantiene acceso
solo a pacientes asignados; no obtiene `Patients.View` global. El Admin usa el
catálogo completo. No hay migración de Auth: su seeder es idempotente.

La política `NutritionErpStaff` exige audiencia `erp` e identificador válido.
Las capacidades se resuelven con `ICurrentContext.HasPermissionAsync`, incluyendo
introspección scoped; Application comprueba la capacidad específica y luego
`patient_professionals` activo. Únicamente el claim global `Patients.View`
habilita alcance global. La revocación de la asignación durante la llamada a IA
impide persistir. Los tests HTTP firman JWT, verifican 401/403/404 y demuestran
que el cuerpo no puede modificar actor ni capacidades. Los tests PostgreSQL
verifican alcance real, independencia del aprobador y cancelación sin escritura.

`NutritionStateMachine` declara las transiciones de borrador/devuelto a revisión
y de revisión a aprobación/devolución; cualquier escritura de una versión
aprobada requiere versionado. La vigencia se calcula con la zona IANA del
paciente: inicio local y fin 29 días después, ambos incluidos. Una nueva versión
del mismo ciclo conserva fechas; un ciclo terminado requiere uno nuevo. No se
admite publicación retroactiva ni ciclos publicados solapados. `por_renovar`
es virtual desde cinco días antes del final, también tras vencer; no altera el
estado histórico. La regeneración recupera nacimiento y datos explícitos previos
como respaldo de los medidos actuales y conserva metas/menús/indicaciones
confirmadas salvo reemplazo solicitado expresamente por el profesional.

El motor puro `Application/Features/Nutrition/Engine` reproduce siete vectores
de metas, menús, búsqueda por roles, redondeos JS, topes y alertas de la fuente
validada. El mes base satisface las bandas; Sofía 1350 reproduce el original,
incluida desviación máxima 15,93% y dos avisos técnicos. No se ocultan esos
avisos alterando las reglas. Una versión aprobada conserva metas/comidas;
editar requiere crear una versión nueva. La Ola 3 incorpora el adaptador móvil.

Contratos ERP de esta ola, bajo JWT y alcance profesional:

- `GET /api/v1/nutrition/patients/{patientId}/prescriptions?page=1&pageSize=20`.
- `POST` en esa ruta: `NutritionGenerateInput` (`manual`, `birthDate` opcionales
  si están medidos; peso/talla/nacimiento/sexo son obligatorios para calcular).
- `GET /{id}` y `GET /{id}/history` para detalle/bitácora.
- `POST /{id}/{action}` con `revision` observada y campos de la acción:
  `save`, `apply-day`, `create-version`, `submit`, `approve`, `return`, `regenerate`.
  `edit` admite metas, comidas parciales, indicaciones ES/EN y confirmación de
  ajustes manuales; `sourceDay` aplica ese día a los 30. Aprobación recibe
  `criticalConfirmed` y fecha local opcional; devolución exige `comment`.

El actor nunca procede del cuerpo: API lo deriva de JWT y permisos efectivos.
La capa Application verifica permiso, alcance, revisión, forma de comidas y
catálogo. Infrastructure bloquea el paciente en Read Committed dentro de la
estrategia retriable; escrituras de versión, hijos, publicación y evento se
confirman juntas. La IA ocurre antes del lock y se vuelve a comprobar alcance
y revisión al entrar. La migración `AddNutritionCaseAudit` solo añade columnas
JSON con defaults válidos y la tabla `app.nutrition_prescription_events`.
Su índice de historia se describe en `docs/database/indexes.md`.

La lectura clínica reutiliza mediciones consolidadas y biométricos válidos de
la batería. No convierte scores a categorías clínicas sin un mapeo publicado:
lo no disponible debe completarlo el profesional y queda en `missingData`.
Se marca provisional hasta completar los 9 instrumentos y las entradas.
Al llegar 9/9 se ofrece regeneración, nunca se sustituye el trabajo en silencio.

`POST /internal/nutrition/plan-draft` del AI Service recibe solo metas, catálogo
y hasta 16 menús, sin identidad. Redacta ES/EN y devuelve JSON estructurado.
Timeout, fallo o formato inválido usan fallback determinista explícito. El
backend vuelve a comprobar catálogo, claves, topes y bandas; una propuesta
inválida conserva el menú determinista y sus alertas. `NarrativeSource` informa
del origen. La cancelación del cliente no se convierte en fallback.

## Fundamentos del nuevo modelo de prescripción (grupos 1–2)

La migración `20261105145334_AddNutritionPrescriptions` añade únicamente cuatro
tablas de `app`: `nutrition_food_items`, `nutrition_prescriptions`,
`nutrition_prescription_meals` y `nutrition_prescription_items`. Conserva tablas,
asignaciones y datos legacy. Los grupos 1–2 dejaron el modelo sin motor ni
endpoints; la Ola 2 incorpora motor y CQRS ERP. La Ola 3 incorpora el adaptador
móvil y retira el flujo ERP de asignación. El cutover ECS sigue a cargo del owner.

Una fila de prescripción identifica paciente, ciclo y versión; guarda estado,
provisionalidad, vigencia local (`date`), metas, indicaciones ES/EN y autoría.
`por_renovar` será un estado derivado, no persistido. La revisión es un token de
concurrencia. El índice parcial sobre `is_published` admite una sola versión
publicada por ciclo; las versiones históricas conservan estado `aprobado`.
Las comidas tienen día 1–30 y claves `des/alm/mer/cen` a las 07:00/12:00/15:30/19:00.
Los ítems referencian el catálogo mediante sus códigos estables y gramos finitos
positivos. Borrador sin fechas es válido; una aprobación exige fechas y autoría.

El catálogo conserva los 35 alimentos y todos los valores por 100 g de
`engine-rules-spec.md` §5, incluidas kcal declaradas y equivalencias ausentes.
Los nutrientes usan `double precision` para la futura paridad del motor con JS;
las kcal declaradas no sustituyen el cálculo Atwater de los siguientes grupos.
Regenerar con `python scripts/generate_nutrition_seed.py`; verificar sin escribir
con `python scripts/generate_nutrition_seed.py --check`. La migración ejecuta ese
SQL como recurso embebido; sus `ON CONFLICT` permiten repetirlo sin duplicados.

`INutritionPrescriptionRepository` vive en Application; EF y su implementación
viven en Infrastructure. Lee sin tracking, lista por paciente con paginación y
guarda el agregado completo en un solo `SaveChangesAsync`. Actualizar reconcilia
altas, cambios y eliminaciones de comidas/ítems, con revisión optimista y token
de cancelación en toda E/S. Las políticas de publicación y transiciones se
implementarán en Application en el grupo 4.

El legacy conserva macros, alérgenos, horarios y agua en crear/actualizar/clonar.
Update recarga dentro de la transacción en cada reintento. Wellness valida
alcance también para asignaciones de ejercicio, creación de rutina por paciente
y filtro por plan nutricional; un recurso ajeno devuelve 404. Los listados
generales filtran por asignación profesional activa. Las rutinas compartidas
siguen siendo biblioteca. La lectura self-service desempata por inicio más
reciente y luego ID mayor usando la fecha local existente.

Pruebas: `NutritionLegacyPreservationTests`, `WellnessPatientScopeTests`,
`GetMyNutritionPlanQueryHandlerTests` y las extensiones de
`NutritionPlanWorkflowTests` (`NutritionBackendFoundationTests.cs` y
`NutritionPrescriptionPersistenceTests.cs`). La integración crea bases aisladas
con PostgreSQL real mediante `COP_TEST_DB_CONNECTION`: rollback intermedio,
alcance SQL, agregado, concurrencia, cancelación, seed idempotente y Down/Up
con datos legacy preservados. No se aplica esta migración a producción.

## Flujo desde las baterías

El último envío de una evaluación solicita el trabajo después del commit. Todas
las baterías asignadas no canceladas deben tener completos sus instrumentos
obligatorios; una batería ausente o vacía no habilita el plan. Los instrumentos
opcionales no bloquean. Una evaluación completada conserva su historial: la
cancelación usa un UPDATE condicional para no sobrescribir una finalización
concurrente.

`NutritionPlanGenerationWorker` reconcilia pacientes elegibles cada minuto, en
lotes de 25, y consume el outbox persistido en `app.nutrition_plans`. La reserva
única `generation_key` evita duplicar el mismo ciclo entre réplicas/reinicios.
Una lease de 5 minutos, revisión optimista y hasta 5 intentos con backoff
recuperan fallos; cada llamada a IA tiene un máximo de 4 minutos y corre fuera
de la transacción. Un reinicio en el último intento termina en `failed`, nunca
en una generación perpetua. Ante datos inválidos se descarta el grafo sucio y
se persiste el fallo con una fila limpia. El profesional puede completar el
borrador manualmente; no se inventa una dieta alternativa.

La IA existente recibe el contexto clínico, restricciones de seguridad y los
últimos resultados persistidos por código de las baterías (hasta 250, sin nombre,
documento ni respuestas abiertas). El AI Service acepta `assessments` y solicita
un ciclo de 30 días con menú semanal completo: desayuno, almuerzo, **merienda**
y cena. El menú se repite desde el inicio del ciclo; el día clínico del ciclo y
el número del día del menú son conceptos distintos.

El borrador no crea una asignación activa. Solo el proceso de aprobación puede
publicarlo. La lectura del paciente, el resolvedor y la consulta SQL final del
snapshot del protocolo verifican propietario, estado activo y aprobación. Una
edición revoca esa aprobación y exige revisar la nueva versión; no se filtra la
comida nueva entre una consulta del resolvedor y la proyección del snapshot.

## Revisión, permisos y trazabilidad

`NutritionPlans.Approve` es independiente de `Wellness.Manage`. El rol funcional
`NutritionReviewer` (nutricionista revisor) se siembra con revisión/aprobación y
acceso a pacientes propios; el administrador le asigna el rol al usuario clínico
y mantiene su asignación activa a cada paciente. El permiso global
`Patients.View` permite revisar toda la población. Un permiso con scope no se
interpreta como acceso global. Los listados, detalles, generación, asignaciones
y decisiones nutricionales aplican el mismo alcance por profesional.

- `POST /api/v1/wellness/nutrition-plans/{id}/approve`:
  `{ "revision": 3, "notes": "Revisión clínica" }`.
- `POST /api/v1/wellness/nutrition-plans/approve`:
  `{ "plans": [{ "id": "...", "revision": 3, "notes": "..." }] }`.
  Entre 1 y 100 planes distintos, un plan por paciente, en una sola transacción:
  si uno falla no se publica ninguno.
- `GET /api/v1/wellness/nutrition-plans/{id}/history`: actor, nombre clínico
  cuando existe, fecha UTC, acción, revisión y notas. Los snapshots completos
  de cada decisión/modificación se conservan en `nutrition_plan_review_events`.
- `PUT /api/v1/wellness/nutrition-plans/{id}` requiere la `revision` cargada.
  El actor procede del JWT; no del cuerpo. Guardar cambia a `pending_review`.

La aprobación toma locks de pacientes en orden, recarga los planes y las
baterías dentro de la transacción, verifica la revisión observada y guarda
planes/asignaciones/eventos juntos. Los reintentos recargan entidades nuevas.
Un ciclo vigente de otro plan impide reemplazos concurrentes silenciosos.
Reaprobar una modificación del mismo plan conserva las fechas del ciclo.
Los endpoints legado de asignación no pueden crear, redatar ni reactivar un
ciclo; publicar requiere la revisión clínica.

El ERP muestra la revisión individual, confirmación explícita, notas, historial
y selección masiva con las revisiones capturadas al seleccionar. Un 409 conserva
el error visible y no se presenta como éxito. Los templates no guardan paciente:
un payload mal formado no puede convertir una plantilla pública en una fuga
de identidad.

## Ciclos de 30 días y estado móvil

La aprobación inicia el ciclo en la fecha local de la inscripción del paciente
(IANA) y termina 29 días después, ambos extremos incluidos. El protocolo calcula
el día del menú según los días transcurridos desde el inicio, sin usar el día
de la semana. Después del día 30 se retira la dieta vencida; el reconciliador
prepara el siguiente borrador a partir de los resultados más recientes y vuelve
a esperar la aprobación.

`GET /api/v1/me/nutrition-plan/status?timezone=America/Bogota` devuelve solamente
estado y calendario, sin comida del borrador: `assessments_pending`,
`no_assessment_assignment`, `no_plan`, `generating`, `generation_failed`,
`pending_review`, `active` o `expired`. Incluye `durationDays: 30`,
`todayLocalDate` y `revision` (cuando existe plan), y para el ciclo activo
`startDate`, `endDate`, `currentDay` y `planId`. La revisión permite invalidar
la caché móvil aunque se reapruebe el mismo plan/día. Sin timezone del dispositivo usa la inscripción del paciente; sin
ambas usa UTC.

El plan aprobado agrega `startDate`, `endDate` y `currentDay` al contrato móvil.
El campo `days` conserva los días del menú; `currentDay` representa el día del
ciclo de 30 días. Los pacientes sin aprobación siguen recibiendo 404 en la
lectura de comidas.

## Migración y despliegue

La lectura del paciente incluye `revision`, igual que el estado del flujo,
para que la app no muestre un menú anterior cuando cambie la versión aprobada.

`20261103214607_AddNutritionReviewWorkflowAndEmergencyContacts` agrega revisión,
outbox e historial, además de ampliar `patient_profiles.emergency_contact` a
`text` para los dos contactos. Los planes históricos conservan su contenido y
quedan pendientes de una aprobación real: no se atribuyen aprobaciones ficticias
a un usuario. Las plantillas archivadas no bloquean nuevos ciclos.

Desplegar API, Auth (seeder de permiso/rol), AI Service (contexto de baterías),
ERP y app, aplicando la migración habitual. Reutiliza la configuración de IA
existente; no crea otra credencial ni llama proveedores reales en pruebas.

Validación: `NutritionPlanReviewRulesTests`,
`GetMyNutritionPlanQueryHandlerTests`, `NutritionPlanWorkflowTests` contra
PostgreSQL aislado (`COP_TEST_DB_CONNECTION`), pruebas Python de generación y
`e2e/nutrition-plan-review.spec.ts` del ERP con todas las APIs interceptadas.

---

Lectura propia del paciente para la APP móvil (`aud=app`): plan de
alimentación activo asignado desde el ERP, con días y comidas. El paciente se
deriva SIEMPRE del JWT (`ICurrentContext.UserId` →
`patient_profiles.user_id` en el handler, anti-IDOR): sin perfil → 404, nunca
datos ajenos. Sin permisos ERP (convención `me/*`).

## Endpoint

| Método y ruta                                    | Respuesta                                                                                                                |
| ------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------ |
| `GET /api/v1/me/nutrition-plan` (sin parámetros) | `200` con `MyNutritionPlanDto`; `401` sin JWT; `404` sin perfil o sin plan activo asignado (mensaje propio, no es error) |

## Resolución del plan activo

La asignación (`app.nutrition_plan_assignments`) es la fuente de verdad del
plan asignado — la aprobación crea la asignación en la misma transacción. No
basta con consultar `nutrition_plans` por `PatientId`. Además se exige la
aprobación vigente de esa revisión y un ciclo de hasta 30 días. De las
asignaciones con `Status == Active` y ventana vigente (hoy UTC), gana el
`StartDate` más reciente; luego se carga el plan con sus días ordenados
(`DayNumber`, `SortOrder`). Ventana vencida o plan huérfano → `null` → 404.

## Contrato `MyNutritionPlanDto`

`id, name, targetCondition?, durationDays, dailyCalorieTarget?, dailyProteinTarget?,
dailyCarbsTarget?, dailyFatTarget?, dailyFiberTarget?, dailyWaterMl, allergens?,
mealTiming?, status ("Active"), days[]`. Cada día: `dayNumber, dailyWaterMl,
meals[]`. Cada comida: `mealType` (nombre del enum de dominio:
`Desayuno`/`Almuerzo`/`Cena`/`Snack`, mismo contrato que el ERP — los códigos
`des/alm/mer/cen` pertenecen al log de ingesta, no al plan), `description?,
foods?, calories?, proteinG?, carbsG?, fatG?, fiberG?, waterMl?, notes?,
sortOrder`. `dailyWaterMl` del plan = el del primer día (convención: todas las
filas del día llevan el mismo valor; default 2000 de la columna).

### Ejemplo de respuesta (200)

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Plan control glucémico",
  "targetCondition": "Diabetes",
  "durationDays": 30,
  "dailyCalorieTarget": 1800,
  "dailyProteinTarget": 90,
  "dailyCarbsTarget": 200,
  "dailyFatTarget": 60,
  "dailyFiberTarget": 25,
  "dailyWaterMl": 2500,
  "allergens": "Frutos secos",
  "mealTiming": "7:00, 12:00, 19:00",
  "status": "Active",
  "days": [
    {
      "dayNumber": 1,
      "dailyWaterMl": 2500,
      "meals": [
        {
          "mealType": "Desayuno",
          "description": "Avena con frutos rojos",
          "foods": "Avena 40g, leche descremada 200ml",
          "calories": 350,
          "proteinG": 12.5,
          "carbsG": 55.0,
          "fatG": 8.0,
          "fiberG": 6.0,
          "waterMl": 500,
          "notes": null,
          "sortOrder": 1
        }
      ]
    }
  ]
}
```

## Food AI — trazabilidad de `foodAnalysisId` (verificado, sin cambios)

`POST /api/v1/program/nutrition/log` ya persiste `Intake.FoodAnalysisId`
cuando viene informado: el controlador lo pasa verbatim al comando
(`ProgramController.cs` → `LogNutritionCommand.Intake`) y el repositorio lo
guarda en `app.nutrition_intake_logs.food_analysis_id` en el insert
(`ProgramRepository.cs`: creación y re-activación) y en el update, con chequeo
de ownership D6 contra `foodai.food_analyses` (la violación de FK es el
backstop). Sin `Intake` el shape anterior sigue intacto.

## Archivos

- `src/CoppAddresd.Application/Features/Nutrition/Queries/GetMyNutritionPlan/`
  (DTOs + query + handler)
- `GET /api/v1/me/nutrition-plan` en
  `src/CoppAddresd.Api/Controllers/MyPatientProfileController.cs`
- Tests: `tests/CoppAddresd.UnitTests/Nutrition/GetMyNutritionPlanQueryHandlerTests.cs`


## Refinamientos de revisión y análisis (noviembre 2026)

El reconciliador y el bloqueo de ciclos concurrentes consideran ciclos
personalizados aprobados, no plantillas antiguas sin aprobación. La publicación
completa las asignaciones previas dentro de la misma transacción. Se mantiene
el conflicto si otro plan personalizado aprobado conserva un ciclo vigente.
Modificar el plan publicado revoca la aprobación y exige revisión de la nueva
versión; al reaprobar el mismo plan se conservan sus fechas vigentes.

`coppaddresd-front/e2e/nutrition-plan-real-db.mjs` prueba edición, aprobación
individual, nueva edición y lote exitoso de dos planes contra APIs reales.
Valida actor, fecha y revisión por API y consulta directa a PostgreSQL, sin
interceptar peticiones. Requiere registros QA aislados y no debe ejecutarse
contra pacientes reales. El revisor de prueba tiene NutritionReviewer,
profesional vinculado, onboarding completo y dos pacientes activos; no tiene
Patients.View global. Producción requiere asignar expresamente el rol y alcance.

La migración `20261104154415_AddAssessmentProfileInsights` agrega explicaciones
de evaluaciones por paciente, hash de resultados e idioma. GET/POST
`/api/v1/health-tests/me/profile-analysis` obtiene/genera el análisis del paciente
resuelto por JWT. POST admite 3 peticiones/minuto y comunica fallos como 503,
sin análisis ficticio. La llamada externa a IA queda fuera de una transacción.
El hash incorpora resultados vigentes y versión de guía, y la unicidad permite
reutilizar una respuesta concurrente. Un cambio de cuenta/idioma descarta
respuestas en vuelo en la app. Los DTO internos de IA no llevan identificadores.

## Composición self-service de prescripciones (Ola 3)

`GET /api/v1/me/nutrition-plan` mantiene exactamente `MyNutritionPlanDto`,
sus días y comidas. Se resuelve el paciente por JWT y el día mediante
`patient_profiles.timezone` y `TimeProvider`. Primero se busca una prescripción
`aprobado`, publicada y vigente (ventana inclusiva de hasta 30 días); gana el
inicio más reciente y luego el UUID mayor. Esa prescripción prevalece incluso
si una asignación legacy vigente empezó después. Sin prescripción vigente,
se conserva la lectura legacy aprobada, con el mismo desempate.

El adaptador proyecta metas, revisión, fechas, día actual, agua y alimentos con
gramos/unidades; calcula macros por comida mediante Atwater y los redondeos
del motor. Los tipos siguen siendo `Desayuno`, `Almuerzo`, `Snack` (merienda)
y `Cena`. Las indicaciones se incluyen en notas si la comida no tiene notas.
No se inventan alérgenos ni condición clínica. El nombre estable es
«Plan nutricional». La consulta EF carga el grafo completo sin consultas por
comida. Contrato, coexistencia, cancelación, fechas locales y desempates
están cubiertos en las suites de lectura y PostgreSQL de Nutrition.

## Cierre backend de Ola 3 — verificación local 2026-11-05

Tareas 4.3, 4.5 y 4.6 completas en `feature/nutrition-backend`. El contrato
móvil se conserva; toda la API ERP `/api/v1/program/assignments*` queda
retirada. Perfil medido, snapshot, registro, narrativa y recuperación/avisos
permanecen; el fallback usa únicamente defaults de plantilla mediante un
comando interno. El flag ECS continúa gestionado por el owner.

`dotnet build --nologo -v quiet`: 0 errores, 11 advertencias preexistentes en
rebuild. Ejecución completa `dotnet test --no-build --nologo -v quiet --logger trx`
con PostgreSQL aislado migrado mediante Auth y API:

| Proyecto | Aprobados | Fallos preexistentes en 56ccacb | Omitidos |
|---|---:|---:|---:|
| UnitTests | 1702 | 9 Programa/Scores | 0 |
| IntegrationTests | 88 | 2 KPIs | 1 |
| Telemedicine.UnitTests | 413 | 0 | 0 |
| Telemedicine.IntegrationTests | 44 | 0 | 0 |
| Community.IntegrationTests | 66 | 3 Comunidad | 0 |
| Total global | 2313 | 14 | 1 |

Los 14 fallos coinciden por nombre con los reproducidos previamente sobre
`56ccacb`; no se modifican ni se atribuyen a Nutrition. Cero fallos nuevos.
El test omitido de narrativa HTTP se ejecutó después, con la simulación
Postman existente en loopback: **1/1 aprobado**, dos HTTP 200 y cinco 503,
incluida apertura de circuito. Acumulado de tests distintos: 2314 aprobados,
14 fallos preexistentes, ninguno pendiente de ejecutar.

En el global pasa el filtro Nutrition unit 217: 203 en sus clases principales
(lectura 9, alertas 21, autorización 12,
calculadora 11, cliente 4, paridad 13, preservación legacy 3, solver 14,
revisión 18, estados 35, scope 63) y 14 regresiones transversales de permisos,
validadores, score y análisis de alimentos. Pasan Nutrition PostgreSQL 30 y Assignment
PostgreSQL 7. El filtro combinado unit Nutrition/Assignment/Narrative pasó
292/292. Los tests de jobs, perfil, narrativa y notices se conservaron.

Listado nominal de fallos heredados, comandos e inventario de cada referencia
retirada en `openspec/changes/plan-nutricional-erp/NOTES-backend.md` del
workspace compartido. Esta evidencia es local y simulada; no certifica
frontend conectado ni despliegue. Sin push, deploy ni cambios de app móvil.

## Ola 4B: bandeja de integración

`GET /api/v1/nutrition/inbox?page=1&pageSize=20&state=sin_plan&search=Ana`
requiere JWT ERP y Nutrition.Manage o Nutrition.Approve. Devuelve
`{items:[{patientId,patientName,state,prescription}],total,page,pageSize}`;
prescription es null para sin_plan o un resumen con id, version, revision,
state, provisional, k, startDate, endDate, isPublished y criticalAlertCount.
Incluye todos los pacientes no borrados del alcance del actor. Relación
patient_professionals Active para propios; Patients.View habilita alcance
global. No acepta alcance ni identidad desde query/body.

Última prescripción por createdAt DESC/id DESC, incluidos borradores. Sin
plan primero por nombre/patientId; resto por fecha descendente/patientId.
State y búsqueda por nombre se filtran en PostgreSQL antes de contar/paginar;
por_renovar se calcula con la fecha local de cada paciente, igual que el módulo.
Críticas se cuentan con ClinicalFlags sin cargar comidas; dos consultas por
página, sin N+1. Search es parcial/case-insensitive y trata comodines como
texto; vacío no filtra. Page mínimo 1, pageSize 1–100, search máximo 200 y
state reconocido; límites inválidos devuelven 400. Un actor con permiso pero
sin pacientes ve página vacía. Se conserva el listado por paciente anterior.

Contrato completo y ejemplos literales en el `CONTRACT.md` de OpenSpec.

Medición local EXPLAIN ANALYZE (2000 pacientes/4000 prescripciones): página de
20 filas en 14,704 ms, planning 0,305 ms. Usa el índice existente
ix_nutrition_prescriptions_patient_state_validity en la búsqueda por paciente;
sin bloques temporales. Prueba verifica dos consultas y cero tracking. No se
añade índice/migración sin necesidad medida. No extrapola latencia productiva.

`GET /api/v1/nutrition/foods` requiere los mismos permisos M o A y devuelve
array común sin paginación, orden code ASC: code/name/group/role/kcal100/
carbs100/protein100/fat100/fiber100/householdEquivalent. Equivalencia nullable
con unitSingular, unitPlural, gramsPerUnit y unitStep. Reutiliza ListFoodsAsync
y los 35 alimentos del seed, sin mezclar FoodAI. Code se usa como foodItemId
al editar. Group es etiqueta de C/P/F/V, no taxonomía adicional. Conserva kcal
declaradas por 100 g; el motor conserva su cálculo Atwater independiente.
No inventa unidades si faltan; el catálogo no depende de tener pacientes asignados.

Verificación final 4B: `dotnet build -c Release --nologo -v quiet`, 0 errores
(11 avisos preexistentes en rebuild). Filtro FullyQualifiedName~Nutrition:
unit **254/254**; integración PostgreSQL **38/38** (35 Workflow, incluidos 4
inbox y 1 foods nuevos, más 3 FoodAiNutrition existentes), sin omisiones.
El filtro amplio necesita COP_TEST_DB_CONNECTION a una base aislada con
migraciones Auth/API para los tests antiguos de FoodAI; Workflow crea además
una base propia por caso. No se usan BD de pacientes reales ni se cambian los
14 fallos históricos ajenos. Las rutas anteriores y el DTO móvil no cambian.

## Ola 6: borrador automático y reserva durable

`BatteryCompletedEvent` se emite tras el commit clínico sin depender del flag de
asignación ni de la existencia de una inscripción. El consumidor legacy mantiene
su propio gate. `NutritionBatteryCompletedHandler` valida la batería inicial y
reserva `(patient_id,battery_assignment_id)`; el worker invoca el caso Generate
con identidad técnica `system:nutrition`, sin aprobar. Un reconciliador independiente
recupera hechos perdidos desde baterías persistidas, en lotes de 25 por minuto.

La reserva usa estados `pending/generating/ready/failed`, lease de tres minutos,
claim token, cinco intentos y espera de un minuto tras fallo. El ID de reserva es
también el ID del borrador: reiniciar después de guardar el plan pero antes de
confirmar la reserva reutiliza exactamente ese plan y su evento. La transacción
del agregado y la confirmación de outbox son cortas y separadas; el estado puede
quedar transitoriamente generating, nunca crear otro borrador por ese corte.
La IA se invoca antes de la transacción y su fallback queda identificado.

La batería inicial activa/autoasignada pertenece al productor nuevo, aun si no llegó
su evento; también se excluyen pacientes con reserva. Así el worker legacy no
encola ni procesa otro plan por anticiparse al evento. Publicaciones y datos
legacy existentes se conservan; el productor viejo sigue para sus otros pacientes.
La identidad técnica tiene GUID reservado `00000000-0000-0000-0000-000000000061`,
sin usuario autenticable ni permiso de aprobar; la bitácora lo identifica con
comentario `system:nutrition`. GUC transmite SYSTEM, user_id NULL y user_role
system:nutrition; el scope restaura la identidad al salir. El trigger excluye
claim_token. No se alteran el handler legacy, ECS, la app ni su DTO.

La migración agrega únicamente `app.nutrition_draft_reservations`; Down conserva
la tabla por diseño y documenta retirada manual. Up idempotente permite reaplicar
tras rollback del binario sin perder reservas ni tocar datos legacy.

### Recuperación de generación (Ola 8, 8.3)

`POST /api/v1/nutrition/patients/{patientId}/draft-generation`: Nutrition.Manage,
actor JWT y 404 fuera de alcance. Devuelve 202 con reserva sin contenido parcial,
o 200 con prescriptionId si ya está lista. La reserva paciente+batería reutiliza
ON CONFLICT dentro de una transacción corta; reprogramar failed conserva attempts.
Pending permite un intento manual adicional tras agotar los cinco automáticos.
La bandeja expone estado/fallback/motivo técnico/fecha de la última reserva usando
los mismos dos SQL por página, sin cargar comidas ni añadir consultas por fila.

### Vigencia en bandeja (Ola 8, 8.4)

La cabecera del último plan y la publicación vigente se resuelven por separado.
La fila añade hasPublishedToday/publishedCycleStart/publishedCycleEnd,
latestDraftState y hasFutureDraft por fecha local del paciente, sin depender de
CycleId. C2 en revisión nunca oculta C1 vigente. Se mantienen dos SQL por página
con joins laterales sobre cabeceras, sin comidas. Tests con reloj fijo cubren
el cambio a medianoche en Bogotá, solo borrador futuro y paginación estable;
el test de 2.000 pacientes valida presupuesto de consultas y EXPLAIN real.

### Misiones desde Nutrition (Ola 8, 8.5)

ProgramContentResolver prioriza la prescripción publicada vigente por fecha local,
incluso sin asignación wellness. SourcePrescriptionId/PrescriptionDayNumber son
referencias internas distintas de NutritionPlanId/NutritionPlanDayNumber.
El rango usa una carga de 100 cabeceras por paciente y decide por cada fecha.
La misión lee solo comidas del día seleccionado y usa NutritionEngine.SumItems
para Atwater; metas desde cabecera, menú y notas desde prescripción.
El snapshot conserva nombres de campos: nutritionPlanId null, día de ciclo en
nutritionPlanDayNumber; no se añaden campos al DTO móvil ni al de registro.
El gate nut exige las comidas prescritas del día. Las escrituras de completación
y de ingesta conservan sus FKs wellness null; CHECK weekday y FK no se alteran.
Sin prescripción se conserva el fallback wellness y el ejercicio es independiente.
Las pruebas PostgreSQL verifican el snapshot final, calorías Atwater frente a
Calories100 deliberadamente distinto, el gate, replay, FKs nulas y fallback.
