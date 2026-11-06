# Contrato real de Nutrition — soporte de integración, Olas 4 y 4B

Auditado el 2026-11-05 sobre `coppAddresdBack`, rama
`feature/nutrition-backend`, base `01635ec`. La Ola 4B incorpora los endpoints
faltantes autorizados por el owner. Este documento describe el código local,
no un contrato futuro ni lo desplegado. No se renombraron rutas existentes;
el endpoint móvil mantiene su ruta y DTO congelados.

## 1. Disponibilidad después del soporte de integración

| Necesidad del front                                                 | Disponibilidad real                                                                                                                           |
| ------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| Bandeja paginada con un estado por paciente                         | **Disponible:** `GET /api/v1/nutrition/inbox`, envelope `{items,total,page,pageSize}`, incluye sin_plan y resumen de última prescripción. §4. |
| Ficha del plan por paciente                                         | Existe, pero requiere **patientId y prescriptionId**. No hay alias `current` ni ficha automática por patientId solamente.                     |
| Generar, guardar, enviar a aprobación, aprobar, devolver, regenerar | Disponibles en las rutas detalladas abajo.                                                                                                    |
| Catálogo de alimentos                                               | **Disponible:** `GET /api/v1/nutrition/foods`, array común de 35 alimentos con nutrientes por 100 g y equivalencia casera nullable. §13.      |
| Lectura del paciente en la app                                      | Existe `GET /api/v1/me/nutrition-plan`, fuera del prefijo ERP por compatibilidad deliberada.                                                  |

Los endpoints de soporte no son aliases ni renombrados. El front no debe sustituir esas
operaciones por las rutas de Wellness, FoodAI o la antigua cola de Programa.
Tampoco debe consultar un detalle por cada paciente para fabricar una bandeja.
Los endpoints legacy de Wellness/NutritionPlanReview y FoodAI conservan sus
contratos propios; no se renombran como aliases de las nuevas prescripciones.

## 2. Inventario exacto de rutas existentes

Abreviaturas solo en la columna de permisos: **M** = `Nutrition.Manage`,
**A** = `Nutrition.Approve`. Todas las rutas ERP requieren JWT válido, audiencia
`erp` y usuario GUID no vacío (`NutritionErpStaff`). Lecturas permiten M **o** A;
las mutaciones exigen la capacidad indicada. No se exige un nombre de rol.

`patientId` es UUID del perfil de paciente; `id` es UUID de prescripción.
El actor nunca viaja en el cuerpo. Además del permiso, Application comprueba
la relación profesional-paciente activa. El claim global `Patients.View`
habilita alcance global, pero no reemplaza M/A. Fuera de alcance: 404.

| Método | Path exacto                                                                | Permiso                      | Query/body                                    | Éxito y forma                                                 |
| ------ | -------------------------------------------------------------------------- | ---------------------------- | --------------------------------------------- | ------------------------------------------------------------- |
| GET    | `/api/v1/nutrition/inbox`                                                  | M o A                        | `page=1&pageSize=20&state?&search?`; sin body | 200, `{items: NutritionInboxItem[], total, page, pageSize}`   |
| GET    | `/api/v1/nutrition/foods`                                                  | M o A                        | Sin query ni body, sin paginación             | 200, `NutritionFoodDto[]`                                     |
| GET    | `/api/v1/nutrition/patients/{patientId}/prescriptions`                     | M o A                        | `page=1&pageSize=20`; sin body                | 200, `NutritionView[]`, sin envelope; `meals:[]`, `alerts:[]` |
| GET    | `/api/v1/nutrition/patients/{patientId}/prescriptions/{id}`                | M o A                        | Sin query ni body                             | 200, `NutritionView` con comidas/alertas                      |
| GET    | `/api/v1/nutrition/patients/{patientId}/prescriptions/{id}/history`        | M o A                        | Sin query ni body                             | 200, `NutritionPrescriptionEvent[]`                           |
| POST   | `/api/v1/nutrition/patients/{patientId}/prescriptions`                     | M                            | `NutritionGenerateInput`                      | 201, `NutritionView` + header Location de la ficha            |
| POST   | `/api/v1/nutrition/patients/{patientId}/prescriptions/{id}/save`           | M                            | `{revision,edit}`                             | 200, `NutritionView`                                          |
| POST   | `/api/v1/nutrition/patients/{patientId}/prescriptions/{id}/apply-day`      | M                            | `{revision,edit:{sourceDay}}`                 | 200, `NutritionView`                                          |
| POST   | `/api/v1/nutrition/patients/{patientId}/prescriptions/{id}/create-version` | M                            | `{revision}`                                  | 200, `NutritionView` de **otro id**, mismo ciclo              |
| POST   | `/api/v1/nutrition/patients/{patientId}/prescriptions/{id}/submit`         | M                            | `{revision}`                                  | 200, `NutritionView`, estado `en_aprobacion`                  |
| POST   | `/api/v1/nutrition/patients/{patientId}/prescriptions/{id}/approve`        | A                            | `{revision,criticalConfirmed,startDate?}`     | 200, `NutritionView`, `aprobado`, publicado                   |
| POST   | `/api/v1/nutrition/patients/{patientId}/prescriptions/{id}/return`         | A                            | `{revision,comment}`                          | 200, `NutritionView`, `devuelto`                              |
| POST   | `/api/v1/nutrition/patients/{patientId}/prescriptions/{id}/regenerate`     | M                            | `{revision,generate?}`                        | 200, `NutritionView`, `borrador_ia`                           |
| GET    | `/api/v1/me/nutrition-plan`                                                | Usuario autenticado; sin M/A | Sin query ni body; paciente derivado del JWT  | 200, `MyNutritionPlanDto`; 404 sin plan vigente               |

El controller registra una única ruta de acción `{id:guid}/{verb}` y discrimina
los siete verbos de la tabla con un switch exacto. Deben enviarse en minúsculas:
`approve` funciona; `Approve`, `draft` o `generate` en esa posición producen 404.
Generar es POST a la colección, **no** `POST /generate`. Guardar es POST `/save`,
**no** PUT/PATCH. Las restricciones `:guid` son del router, no texto del URL.

## 3. Serialización y DTO común de respuesta ERP

Todas las acciones después de `{id}/` enlazan el mismo
`NutritionActionRequest`. Esta es su envoltura JSON completa con defaults;
cada acción utiliza el subconjunto indicado en su sección. `revision` debe
tomarse de la respuesta más reciente, no de este ejemplo de forma:

```json
{
  "revision": 1,
  "edit": null,
  "generate": null,
  "comment": null,
  "criticalConfirmed": false,
  "startDate": null
}
```

Esto no es el body de generar a la colección: ese usa directamente
`NutritionGenerateInput` (§6). Omitir generate en `/regenerate` utiliza sus
defaults; omitir edit en `/save` no aplica una edición de campos.

JSON en camelCase, `Content-Type: application/json`. Los null se incluyen;
arrays vacíos son `[]`. UUID como string; fechas locales como `YYYY-MM-DD`;
timestamps UTC como ISO 8601. No existe wrapper `{data:...}`.

`NutritionView` contiene exactamente estos campos:

| Campos                                                                | Tipo y significado                                                                              |
| --------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `id`, `patientId`, `cycleId`                                          | UUID string                                                                                     |
| `version`, `revision`                                                 | integer; `revision` es el token de concurrencia que debe reenviarse                             |
| `state`                                                               | `borrador_ia`, `en_aprobacion`, `aprobado`, `devuelto` o estado derivado `por_renovar`          |
| `provisional`, `isPublished`, `menusOutdated`, `manualEditsConfirmed` | boolean                                                                                         |
| `startDate`, `endDate`                                                | fecha local o null                                                                              |
| `targets`                                                             | objeto de metas descrito a continuación                                                         |
| `instructions`, `instructionsEn`                                      | string o null, indicaciones ES/EN                                                               |
| `narrativeSource`                                                     | `llm` o `deterministic_fallback`                                                                |
| `missingData`                                                         | string[], códigos de datos ausentes; no etiquetas traducidas                                    |
| `alerts`                                                              | `{s:string,m:string,tec:boolean}[]`; `s`: `crit`, `warn`, `info`; `tec` distingue aviso técnico |
| `meals`                                                               | `NutritionMealEdit[]`, descrito debajo                                                          |
| `canRegenerate`                                                       | boolean: provisional y batería de tests completa; no reemplaza la validación de permiso/estado  |
| `createdBy`                                                           | UUID del actor creador                                                                          |
| `approvedBy`, `approvedAt`                                            | UUID/timestamp o null                                                                           |

`targets` usa claves del motor, no las del mock frontend:

| Campo                                 | Unidad/semántica                                            |
| ------------------------------------- | ----------------------------------------------------------- |
| `k`                                   | kcal/día                                                    |
| `c`, `p`, `f`, `fi`                   | gramos/día de carbohidratos, proteína, grasa y fibra        |
| `vasos`                               | vasos de 250 ml; agua diaria = `vasos * 250`                |
| `bmr`, `tdee`                         | kcal/día                                                    |
| `deficit`                             | fracción, p. ej. 0.1 = 10%                                  |
| `floor`                               | piso de kcal                                                |
| `fase`                                | `perder`, `mantener` o `subir`                              |
| `metaKg`, `kgLeft`, `wkKg`, `semanas` | meta de peso, kg por perder, estimación kg/semana y semanas |

`NutritionMealEdit` conserva este shape en request y response:

```json
{
  "dayNumber": 1,
  "mealType": "des",
  "menuName": "Avena con frutos rojos y claras",
  "items": [{ "foodItemId": "avena", "grams": 40, "notes": null }],
  "notes": null,
  "waterMl": null
}
```

En ERP, comidas `des`/`alm`/`mer`/`cen`; día 1–30. No se expone `scheduledTime`
ni id de comida/ítem en este DTO. El orden de items determina SortOrder.
`menuName`, `notes`, `waterMl` pueden ser null. No hay nombre/icono ni macros
por ítem: solo código y gramos. La ficha tampoco expone nombre/demografía del
paciente, perfil clínico completo, timezone, `returnComment` ni acciones
disponibles. El comentario de devolución se consulta en history.

Ejemplo **HTTP 200 literal** capturado con `NutritionAuthorizationTests.StartAsync`
y el fixture de `HttpRead_EnforcesJwtAudiencePermissionsAndOwnScope`.
Su fixture deliberadamente reducido tiene cuatro comidas de un solo día;
sirve para contrato/autorización, **no** como plan válido para submit/approve.
`cycleId` fue generado por el fixture en esta captura, no es una constante del test.

```json
{
  "id": "55555555-5555-5555-5555-555555555555",
  "patientId": "11111111-1111-1111-1111-111111111111",
  "cycleId": "91ea69d6-a99b-479d-ba66-0e699c68bcee",
  "version": 1,
  "revision": 1,
  "state": "borrador_ia",
  "provisional": false,
  "isPublished": false,
  "menusOutdated": false,
  "manualEditsConfirmed": false,
  "startDate": null,
  "endDate": null,
  "targets": {
    "k": 1850,
    "c": 217,
    "p": 93,
    "f": 68,
    "fi": 26,
    "vasos": 10,
    "bmr": 1340,
    "tdee": 2077,
    "deficit": 0.1,
    "floor": 1200,
    "fase": "perder",
    "metaKg": 62.2,
    "kgLeft": 6.3,
    "wkKg": 0.2,
    "semanas": 32
  },
  "instructions": null,
  "instructionsEn": null,
  "narrativeSource": "deterministic_fallback",
  "missingData": [],
  "alerts": [
    {
      "s": "info",
      "m": "HbA1c 5.9% (prediabetes): carbohidratos complejos, fibra y proteína en cada comida.",
      "tec": false
    },
    {
      "s": "warn",
      "m": "Algún día se desvía 67% de la meta calórica (tolerancia 8%).",
      "tec": true
    },
    {
      "s": "warn",
      "m": "Proteína mínima de 20 g (meta 93 g).",
      "tec": true
    },
    {
      "s": "warn",
      "m": "Fibra mínima de 16 g (meta 26 g).",
      "tec": true
    },
    {
      "s": "warn",
      "m": "Carbohidratos se desvían 50% de la meta.",
      "tec": true
    },
    {
      "s": "warn",
      "m": "Grasas se desvían 82% de la meta.",
      "tec": true
    }
  ],
  "meals": [
    {
      "dayNumber": 1,
      "mealType": "des",
      "menuName": null,
      "items": [
        {
          "foodItemId": "avena",
          "grams": 40,
          "notes": null
        }
      ],
      "notes": null,
      "waterMl": null
    },
    {
      "dayNumber": 1,
      "mealType": "alm",
      "menuName": null,
      "items": [
        {
          "foodItemId": "avena",
          "grams": 40,
          "notes": null
        }
      ],
      "notes": null,
      "waterMl": null
    },
    {
      "dayNumber": 1,
      "mealType": "mer",
      "menuName": null,
      "items": [
        {
          "foodItemId": "avena",
          "grams": 40,
          "notes": null
        }
      ],
      "notes": null,
      "waterMl": null
    },
    {
      "dayNumber": 1,
      "mealType": "cen",
      "menuName": null,
      "items": [
        {
          "foodItemId": "avena",
          "grams": 40,
          "notes": null
        }
      ],
      "notes": null,
      "waterMl": null
    }
  ],
  "canRegenerate": false,
  "createdBy": "44444444-4444-4444-4444-444444444444",
  "approvedBy": null,
  "approvedAt": null
}
```

## 4. Bandeja global: GET /api/v1/nutrition/inbox

M o A, misma política JWT ERP. El alcance procede exclusivamente del actor:
Patients.View global ve todos los pacientes no borrados; profesional ve solo
sus asignaciones patient_professionals Active. Sin asignación activa devuelve
200 con página vacía; no revela pacientes ajenos ni sus conteos.

Query opcional: `page=1`, `pageSize=20` (1–100), `state`, `search` (nombre completo,
trim, máximo 200 caracteres, búsqueda parcial sin distinguir mayúsculas).
Page <1, pageSize fuera de 1–100, state desconocido o search excesivo: 400.
Search vacío equivale a sin filtro; `%`, `_` y barra invertida son texto literal.
State acepta `sin_plan`, `borrador_ia`, `en_aprobacion`, `aprobado`, `devuelto`,
`por_renovar`. No recibe actor, professionalId ni viewAll desde el cliente.

```text
GET /api/v1/nutrition/inbox?page=1&pageSize=20&state=sin_plan&search=Ana
```

Una fila por paciente. Última prescripción = createdAt DESC y UUID DESC,
incluidos borradores; no se elige la de inicio más reciente ni se sustituye
por la publicada. Filtro de estado se aplica a esa última versión, no a toda
la historia. Sin prescripción nueva: state sin_plan y prescription null,
aunque existan planes legacy. No se carga el mes para generar una cabecera.

Estado por_renovar replica la regla del módulo: aprobado, publicado y endDate
≤ hoy local +5 días, incluido vencido. SQL usa la zona de cada paciente y el
reloj UTC de la petición, antes de contar y paginar. Zona inválida: 422.

Orden estable: sin_plan primero por nombre ASC y patientId ASC; después,
fecha de creación de última prescripción DESC y patientId ASC. Los filtros
de alcance, estado y nombre preceden a total/offset. Una página posterior al
final devuelve items vacío conservando total. Total cuenta pacientes filtrados,
no prescripciones; no hay totalPages adicional.

Ejemplo JSON literal de `NutritionInboxQueryTests.Reviewer_EmptySearch_AndNoPrescription_HaveStableEnvelope`
y del fixture HTTP `NutritionAuthorizationTests.HttpInbox_UsesJwtScope_IgnoresClientActor_AndKeepsEnvelope`:

```json
{
  "items": [
    {
      "patientId": "11111111-1111-1111-1111-111111111111",
      "patientName": "Ana Ruiz",
      "state": "sin_plan",
      "prescription": null,
      "generationState": null,
      "generationFallback": false,
      "generationReason": null,
      "generationUpdatedAt": null
    }
  ],
  "total": 1,
  "page": 1,
  "pageSize": 20
}
```

Ejemplo de la página con prescripción de
`Page_PropagatesScopeFiltersClockAndToken_CountsClinicalCriticalsWithoutMeals`:

```json
{
  "items": [
    {
      "patientId": "22222222-2222-2222-2222-222222222222",
      "patientName": "Ana Ruiz",
      "state": "por_renovar",
      "prescription": {
        "id": "55555555-5555-5555-5555-555555555555",
        "version": 2,
        "revision": 7,
        "state": "por_renovar",
        "provisional": true,
        "k": 1850,
        "startDate": "2026-10-12",
        "endDate": "2026-11-10",
        "isPublished": true,
        "criticalAlertCount": 4
      },
      "generationState": "ready",
      "generationFallback": false,
      "generationReason": null,
      "generationUpdatedAt": "2026-11-06T02:00:00Z"
    }
  ],
  "total": 11,
  "page": 2,
  "pageSize": 10
}
```

State de fila y prescription.state coinciden. K son kcal/día nullable, no
objeto targets; criticalAlertCount cuenta alertas `s=crit` del motor con
perfil/metas de esa última versión. Los avisos técnicos son warn y no se
cuentan. Versión/revisión son enteros; fechas nullable para borradores.
No hay perfil clínico, comidas ni array completo de alertas en este resumen.

### Listado de prescripciones de un paciente: se conserva sin cambios

```text
GET /api/v1/nutrition/patients/11111111-1111-1111-1111-111111111111/prescriptions?page=1&pageSize=20
Authorization: Bearer <JWT ERP con M o A>
```

Sin request JSON. `page` se normaliza a mínimo 1; `pageSize` se limita a 1–100.
Orden: `startDate` descendente, luego UUID descendente (la ordenación de nulls
es la de PostgreSQL DESC). Cada elemento tiene el DTO de §3 pero **siempre**
`meals:[]` y `alerts:[]`, incluso cuando el detalle contiene comidas y alertas.
No confundir arrays vacíos de cabecera con ausencia clínica de alertas.
No hay `total`, `totalPages`, `page`, `pageSize` ni `items` en la respuesta.
Puede haber varias versiones/ciclos del mismo paciente en una página.

Ejemplo HTTP 200 literal contra el fixture de autorización sin listado configurado:

```json
[]
```

## 5. Ficha por paciente y prescripción

```text
GET /api/v1/nutrition/patients/11111111-1111-1111-1111-111111111111/prescriptions/55555555-5555-5555-5555-555555555555
```

M o A, sin query ni body. HTTP 200 con el JSON completo de §3. Requiere que
la prescripción pertenezca al patientId y esté en alcance; ambos cruces fallan
con 404, sin revelar existencia. Puede leer borradores e historia aprobada;
no selecciona automáticamente la prescripción vigente.

## 6. Generar propuesta con IA

```text
POST /api/v1/nutrition/patients/11111111-1111-1111-1111-111111111111/prescriptions
```

Permiso M; sin query. Body `NutritionGenerateInput`:
`manual?: NutritionPatient`, `birthDate?: fecha`,
`preserveConfirmedEdits?: boolean = true` (relevante al regenerar).
Se puede enviar `{}` si las fuentes medidas aportan peso, talla, nacimiento
y sexo válidos. Esos datos no se inventan. La edad se recalcula desde nacimiento
y hoy local; `manual.edad` no sustituye `birthDate` del input/contexto.

Ejemplo serializado a partir de `NutritionAuthorizationTests.Patient` y
`HttpGenerate_ActorComesFromJwt_AndBodyCannotGrantApproval`. Se omite el campo
extra `actor` que ese test envía para demostrar que no otorga permisos:

```json
{
  "manual": {
    "peso": 68.5,
    "talla": 161,
    "edad": 38,
    "sexo": "F",
    "imc": 26.4,
    "hba1c": 5.9,
    "mov": "MARCHA",
    "objetivo": null,
    "objFijo": false,
    "nutRiesgo": null,
    "sleepRF": null,
    "sleepNivel": null,
    "adh": null,
    "riesgoNum": null,
    "estres": null,
    "deficit": null,
    "kcal": null,
    "carbPct": null,
    "protG": null,
    "fiber": null,
    "vasos": null,
    "birthDate": null
  },
  "birthDate": "1988-01-01"
}
```

`manual` usa estos nombres exactos, incluidos `sleepRF`, `nutRiesgo`,
`objFijo`, `riesgoNum`, `protG`, `carbPct`, `fiber`, `kcal` y `vasos`.
No espera `weight`, `height`, `calorieTarget` ni `dailyWaterMl` en manual.
Peso en kg, talla en cm, IMC kg/m², HbA1c en porcentaje. `mov` usa claves del
motor como `MARCHA`; `adh` usa `A0`–`A3`. Campos opcionales ausentes se reflejan
en missingData y provisionalidad. Un `manual.birthDate` anidado no sustituye
la resolución de nacimiento: usar `birthDate` a nivel del input.

HTTP **201** + Location de la ficha; body completo `NutritionView` con
120 comidas, estado `borrador_ia`. El proveedor puede fallar y aun así responder
201 usando fallback determinista; comprobar `narrativeSource`, no inferirlo del
status HTTP. La llamada externa ocurre antes de la transacción. Cancelación
no se transforma en fallback.

Fragmento literal de la respuesta capturada con el mismo fixture (solo campos
seleccionados; el body completo tiene todos los campos de §3 y 120 comidas):

```json
{
  "id": "c3bd578d-eeb4-4cc8-a375-82ab62039014",
  "patientId": "11111111-1111-1111-1111-111111111111",
  "version": 1,
  "revision": 1,
  "state": "borrador_ia",
  "provisional": true,
  "isPublished": false,
  "instructions": "ES\nES",
  "instructionsEn": "EN\nEN",
  "narrativeSource": "deterministic_fallback",
  "missingData": [
    "bateria_9_tests",
    "nutRiesgo",
    "sleepRF",
    "sleepNivel",
    "adh",
    "riesgoNum",
    "estres"
  ],
  "canRegenerate": false,
  "createdBy": "44444444-4444-4444-4444-444444444444"
}
```

## 7. Guardar borrador y editar metas/comidas

```text
POST /api/v1/nutrition/patients/{patientId}/prescriptions/{id}/save
```

M, sin query. No publica ni envía a revisión. Solo estados persistidos
`borrador_ia`/`devuelto`; versión aprobada es inmutable.

Ejemplo de `Cqrs_GenerateEditApproveNewVersion_PreservesPublicationAndActorHistory`:

```json
{
  "revision": 1,
  "edit": {
    "instructions": "Edición profesional",
    "instructionsEn": "Professional edit",
    "confirmManualEdits": true
  }
}
```

`edit` admite `targets`, `meals`, `instructions`, `instructionsEn`,
`confirmManualEdits` (default false) y `sourceDay` (usado por apply-day).
Omitir un campo conserva el valor previo. Texto vacío permite vaciar
indicaciones; null no las reemplaza. `meals` es una lista de reemplazos por
par `(dayNumber,mealType)`, **no** reemplaza todo el mes ni permite eliminar una
comida: para una comida enviada, `items` reemplaza sus ítems completos.

`targets` acepta el objeto de metas de §3, pero solo cambia
`k,c,p,f,fi,vasos`; conserva métricas derivadas del motor. Cambiar metas marca
`menusOutdated:true`; no escala gramos automáticamente. Confirmar únicamente
indicaciones no limpia ese flag. Confirmar ediciones con comidas o apply-day
sí lo limpia. La aprobación rechaza menús pendientes de actualización.

HTTP 200, mismo `NutritionView`. El test anterior verifica las indicaciones
literales y la conservación al versionar; EF incrementa `revision` a 2 en
ese guardado. Reemplazar siempre la revisión local por la devuelta.

## 8. Aplicar día, crear versión y enviar a aprobación

Todas son POST con permiso M, sin query y HTTP 200 con `NutritionView`:

| Path después de `{id}/` | Body literal de los tests                                         | Efecto                                                                                                                                                                                       |
| ----------------------- | ----------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `apply-day`             | `{"revision":1,"edit":{"sourceDay":1,"confirmManualEdits":true}}` | Copia las cuatro comidas del día origen a los 30 días; caso `Cqrs_ApplyDay_ChangesOnlyOwnPlan_AndStaleRevisionCannotWrite`                                                                   |
| `create-version`        | `{"revision":4}`                                                  | Requiere aprobado; crea otro id y revision 1, mismo cycleId, version siguiente, borrador no publicado; secuencia de `Cqrs_GenerateEditApproveNewVersion_PreservesPublicationAndActorHistory` |
| `submit`                | `{"revision":2}`                                                  | Borrador/devuelto → `en_aprobacion`; secuencia del mismo test después de save                                                                                                                |

Los números de revisión de los ejemplos pertenecen a esa secuencia específica;
no son constantes de API. Crear versión devuelve 200, no 201. Cambiar el id que
guarda el front al recibir la nueva versión; no seguir editando la aprobada.
Submit/aprobar validan 30 días × 4 comidas, sin duplicados, 1–20 ítems por
comida, alimento de catálogo y gramos finitos >0 y ≤2000. Guardar también
valida el mes; no admite un plan incompleto como el fixture reducido de §3.

## 9. Aprobar con confirmación de críticas

```text
POST /api/v1/nutrition/patients/{patientId}/prescriptions/{id}/approve
```

A, sin query. Antes debe hacerse `/submit`: solo `en_aprobacion` es aprobable.
Ejemplo de la secuencia generate/save/submit/approve del test CQRS:

```json
{ "revision": 3, "criticalConfirmed": true }
```

`criticalConfirmed` default false: exige true si existen alertas `s:"crit"`.
No otorga permiso ni reemplaza la revisión personal. El test
`State_RejectsInvalidTransitionsAndEmptyReturn_ConfirmsCriticalAlertsWithActor`
verifica 422 sin confirmación cuando hay críticas y aprobación con confirmación.
No hay parámetro `force`, `approvedBy` ni `autoApprove` aceptado por el DTO.

`startDate` es opcional: fecha ISO local. Si falta, usa hoy del paciente;
fin = inicio +29 días, inclusivos. Nueva versión de un ciclo conserva sus
fechas; no admite retroactividad ni solapamiento con otro ciclo publicado.
`State_LocalCycleHasThirtyDays_RenewalAtFiveDays_AndOverlapsAreRejected`
usa reloj `2026-11-06T02:00:00Z` en Bogotá y verifica este fragmento de valores:

```json
{
  "state": "aprobado",
  "isPublished": true,
  "startDate": "2026-11-05",
  "endDate": "2026-12-04"
}
```

La respuesta HTTP 200 es el DTO **completo** de §3, no solo ese fragmento.
Incluye revisión incrementada y `approvedBy`/`approvedAt` del actor/reloj del
servidor. Sustituye la publicación anterior del mismo ciclo conservando su
historia. Provisional no es un estado: el test CQRS permite aprobar manteniendo
el badge provisional. `por_renovar` es estado de lectura de una versión
publicada cuando faltan ≤5 días, también después de vencer; no es una transición
que el front deba guardar. Autoaprobación significa mismo creador/aprobador,
queda en el evento `autoApproved`; no implica aprobación automática por IA.

## 10. Devolver con comentario

```text
POST /api/v1/nutrition/patients/{patientId}/prescriptions/{id}/return
```

A, sin query; requiere `en_aprobacion`. Ejemplo de
`State_RejectsInvalidTransitionsAndEmptyReturn_ConfirmsCriticalAlertsWithActor`:

```json
{ "revision": 2, "comment": "Revisar clínica" }
```

HTTP 200, DTO completo de §3 con `state:"devuelto"` y nueva revisión. Comentario
obligatorio, no vacío/espacios, máximo 4000 caracteres. El comentario de la
devolución no es un campo del NutritionView: el front debe consultar history
para recuperarlo. Un usuario con solo M recibe 403 aunque envíe `actor` con
canApprove=true (test HTTP de autorización).

## 11. Regenerar

```text
POST /api/v1/nutrition/patients/{patientId}/prescriptions/{id}/regenerate
```

M, sin query; solo borrador/devuelto. Body mínimo del test
`State_NineCompletedTestsOfferRegeneration_AndConfirmedEditsSurvive` después de save:

```json
{ "revision": 2 }
```

También admite `generate` con el mismo input de §6. Variante explícita que
expresa el default probado por ese test:

```json
{ "revision": 2, "generate": { "preserveConfirmedEdits": true } }
```

HTTP 200, `NutritionView` completo. Si manualEditsConfirmed y
preserveConfirmedEdits=true (default), conserva metas/menús/indicaciones
confirmadas, actualiza perfil/faltantes/provisionalidad y vuelve a borrador.
No enviar false por defecto: solicita sustituir los ajustes confirmados.
Cuando se completan los 9 tests, `canRegenerate:true` informa que hay mejores
insumos; no se sobrescribe el plan automáticamente. Regenerar no crea otro id.

## 12. History

```text
GET /api/v1/nutrition/patients/{patientId}/prescriptions/{id}/history
```

M o A; sin query/body, sin paginación. Response array de esta forma:

```text
[{ id: UUID, prescriptionId: UUID, actorUserId: UUID, occurredAt: ISO-UTC,
   action: string, revision: integer, comment: string|null,
   criticalCount: integer, criticalConfirmed: boolean, autoApproved: boolean,
   snapshotJson: string }]
```

`snapshotJson` es **string con JSON**, no objeto expandido. Acciones registradas:
`generate`, `save`, `applyday`, `createversion`, `submit`, `approve`, `return`,
`regenerate`, `publication_replaced`. Atención: `applyday`/`createversion` de
auditoría no llevan los guiones del path. Los tests CQRS comprueban eventos
`approve`, `autoApproved:true`, actor JWT y `publication_replaced`.

## 13. Catálogo de alimentos: GET /api/v1/nutrition/foods

M o A bajo JWT ERP. Sin query ni body, sin paginación. Devuelve HTTP 200 y
array directo `NutritionFoodDto[]`, ordenado por code ASC ordinal. El seed
actual tiene 35 alimentos. Reutiliza ListFoodsAsync; no consulta FoodAI/USDA.
El catálogo es común: un actor autorizado sin pacientes puede leerlo completo;
no concede acceso a pacientes ni expone información clínica individual.

| Campo JSON                                        | Tipo y significado                                                                                |
| ------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| `code`                                            | string; id de `nutrition_food_items`, se envía como `foodItemId` al editar, p. ej. avena; no UUID |
| `name`                                            | string; nombre del catálogo en español                                                            |
| `group`                                           | etiqueta de agrupación derivada del rol: Carbohidratos, Proteínas, Grasas, Vegetales              |
| `role`                                            | código literal C/P/F/V del motor; group no es una segunda taxonomía clínica                       |
| `kcal100`                                         | number, kcal declaradas por 100 g                                                                 |
| `carbs100`, `protein100`, `fat100`, `fiber100`    | number, gramos declarados por 100 g                                                               |
| `householdEquivalent`                             | null si no hay equivalencia, u objeto con los cuatro campos de abajo                              |
| `householdEquivalent.unitSingular`, `.unitPlural` | string, unidad casera en singular y plural                                                        |
| `householdEquivalent.gramsPerUnit`                | number, gramos por una unidad                                                                     |
| `householdEquivalent.unitStep`                    | number, paso permitido en unidades (0.5 = media unidad)                                           |

No recalcular kcal100 con Atwater: la fuente conserva kcal declaradas y el
motor calcula energía de porciones con sus reglas propias. No se inventan
tazas/rebanadas cuando falta equivalencia: el picker permite gramos. Group es
solo etiqueta del rol, incluidas frutas/legumbres en C y té en V según la fuente.

Ejemplo literal del DTO del fixture
`NutritionFoodCatalogTests.Catalog_PreservesDeclaredNutrients_SortsCodes_AndHasStablePickerContract`:
el test usa dos alimentos, mientras la consulta al catálogo sembrado devuelve
los 35. `HttpFoods_ReturnsSeededPickerFields_AndHouseholdUnits` y la integración
`Foods_SeededCatalog_MapsRealPostgresForBothPermissions_WithoutPatientScope`
verifican la forma HTTP y los valores del seed real:

```json
[
  {
    "code": "avena",
    "name": "Avena en hojuelas (seca)",
    "group": "Carbohidratos",
    "role": "C",
    "kcal100": 379,
    "carbs100": 67.7,
    "protein100": 13.2,
    "fat100": 6.5,
    "fiber100": 10.1,
    "householdEquivalent": null
  },
  {
    "code": "panint",
    "name": "Pan integral 100%",
    "group": "Carbohidratos",
    "role": "C",
    "kcal100": 250,
    "carbs100": 42,
    "protein100": 12.5,
    "fat100": 3.5,
    "fiber100": 6,
    "householdEquivalent": {
      "unitSingular": "rebanada",
      "unitPlural": "rebanadas",
      "gramsPerUnit": 30,
      "unitStep": 0.5
    }
  }
]
```

Auth: sin JWT 401; audiencia no ERP/identidad inválida/sin M ni A 403.
El actor no se acepta del cliente. No hay endpoint `/food-items` o `/catalog`:
el path de integración es exactamente `/api/v1/nutrition/foods`.

## 14. Adaptador app: contrato congelado

```text
GET /api/v1/me/nutrition-plan
Authorization: Bearer <JWT del paciente>
```

Solo autenticación, sin Nutrition.Manage/Approve; no recibe patientId, id,
timezone ni fecha por query/body. Resuelve el paciente desde userId del JWT.
No se mueve a `/api/v1/nutrition/me/...`: cambiarlo rompería la app.

Prioridad por hoy local: prescripción aprobada/publicada vigente, luego legacy
aprobado con asignación Active vigente si no hay prescripción nueva vigente.
En cada familia gana inicio más reciente y luego UUID mayor. Nueva prevalece
incluso si legacy inició después. Ventana nueva inclusiva de 30 días.

HTTP 200, DTO distinto del ERP. Ejemplo literal serializado por el handler a
partir de los datos de
`GetMyNutritionPlanQueryHandlerTests.Composition_LocalDate_Coexistence_AndFrozenJsonContract(true)`;
solo el UUID se genera en cada ejecución. El fixture reducido tiene un día/una
comida para probar la composición y no es un mes aprobable:

```json
{
  "id": "4685fc65-a5c0-4fe9-ad32-6dc7f7c8727f",
  "name": "Plan nutricional",
  "targetCondition": null,
  "durationDays": 30,
  "dailyCalorieTarget": 1500,
  "dailyProteinTarget": null,
  "dailyCarbsTarget": null,
  "dailyFatTarget": null,
  "dailyFiberTarget": null,
  "dailyWaterMl": 2250,
  "allergens": null,
  "mealTiming": "16:00",
  "status": "Active",
  "days": [
    {
      "dayNumber": 30,
      "dailyWaterMl": 2250,
      "meals": [
        {
          "mealType": "Snack",
          "description": null,
          "foods": "Avena: 50 g",
          "calories": 176,
          "proteinG": 5,
          "carbsG": 30,
          "fatG": 4,
          "fiberG": 3,
          "waterMl": null,
          "notes": "Indicaciones",
          "sortOrder": 1
        }
      ]
    }
  ],
  "startDate": "2026-10-07",
  "endDate": "2026-11-05",
  "currentDay": 30,
  "revision": 7
}
```

Campos exactos: los del ejemplo, incluidos nulls. `days` agrupa comidas; tipos
históricos `Desayuno`, `Almuerzo`, `Snack` y `Cena`, **no** des/alm/mer/cen.
`foods` es string, no array. Macros/agua pueden ser null. Agua diaria entera en
ml. El status histórico es `Active`, **no** `aprobado`. El adaptador nuevo no
inventa condición/alérgenos (null), usa nombre estable «Plan nutricional»,
cantidades con gramos/unidades y macros Atwater redondeados del motor.

Sin plan vigente devuelve HTTP 404 con este body del controller:

```json
{ "message": "No tienes un plan de alimentación activo asignado." }
```

Sin perfil vinculado devuelve 404 ProblemDetails. Sin identidad, 401.

## 15. Errores y concurrencia para la integración

| Status | Caso real                                                                                                                     |
| ------ | ----------------------------------------------------------------------------------------------------------------------------- |
| 400    | JSON/model binding inválido; no confundir con validación clínica                                                              |
| 401    | JWT ausente/inválido                                                                                                          |
| 403    | Audiencia no ERP/usuario inválido en política ERP, falta M/A o capacidad de la acción                                         |
| 404    | Paciente fuera de alcance, prescripción de otro paciente/no encontrada, verbo no reconocido o ruta ausente                    |
| 409    | Revisión obsoleta, transición inválida, edición de aprobado, menús desactualizados, vigencia/solapamiento                     |
| 422    | Datos mínimos insuficientes, metas/ítems/mes inválidos, críticas no confirmadas, devolución sin comentario, fecha retroactiva |
| 499    | Cancelación de la petición/operación                                                                                          |

Las excepciones de negocio usan ProblemDetails; challenge/forbid de autenticación
pueden tener body vacío. No asumir que todos los errores incluyen `message`.
Ejemplo 404 **HTTP literal** con identidad M propia intentando paciente ajeno,
reutilizando el fixture de `HttpRead_EnforcesJwtAudiencePermissionsAndOwnScope`:

```json
{
  "type": "https://httpstatuses.io/404",
  "title": "Not Found",
  "status": 404,
  "detail": "Paciente no encontrado.",
  "instance": "/api/v1/nutrition/patients/22222222-2222-2222-2222-222222222222/prescriptions/66666666-6666-6666-6666-666666666666",
  "correlationId": null
}
```

Después de cada POST, guardar `id`, `revision`, estado y flags devueltos. En
409 de revisión, recargar y resolver la edición; no reintentar ciegamente con
revision anterior. Las mutaciones no exponen Idempotency-Key: repetir POST
generar puede crear otro ciclo y repetir create-version puede crear otra versión.
No mandar actor/approvedBy/capacidades: no forman parte del input y el test HTTP
demuestra que no conceden autorización. No se asume validación estricta de
campos JSON extra: el binding actual puede ignorarlos.

## 16. Fuentes, captura y alcance de la verificación

- `src/CoppAddresd.Api/Controllers/NutritionController.cs`: rutas, verbo, action request, status.
- `NutritionReadController.cs`, `NutritionReadQueries.cs`, `NutritionFoodCatalogQuery.cs`, `INutritionInboxRepository.cs`, `NutritionInboxRepository.cs`: endpoints y DTO de soporte 4B.
- `src/CoppAddresd.Api/Authorization/NutritionPolicies.cs`: JWT ERP e identidad.
- `src/CoppAddresd.Application/Features/Nutrition/Cases/NutritionCaseContracts.cs`: DTO públicos.
- `NutritionCases.cs`, `NutritionStateMachine.cs`: permisos, scope, estados, revisión, publicación.
- `src/CoppAddresd.Infrastructure/Repositories/NutritionPrescriptionRepository.cs`: paginado real y selección vigente.
- `src/CoppAddresd.Api/Controllers/MyPatientProfileController.cs` y `MyNutritionPlanDtos.cs`: ruta y shape app congelados.
- `tests/CoppAddresd.UnitTests/Nutrition/NutritionAuthorizationTests.cs`: HTTP/JWT/actor, fixture de ejemplos ERP.
- `tests/CoppAddresd.UnitTests/Nutrition/GetMyNutritionPlanQueryHandlerTests.cs`: shape literal de app y coexistencia.
- `tests/CoppAddresd.IntegrationTests/NutritionCaseCommandTests.cs`, `NutritionCycleStateTests.cs`: ejemplos de edición, estados, revisión y fechas.
- `tests/CoppAddresd.IntegrationTests/NutritionPrescriptionPersistenceTests.cs`: catálogo físico y DTO de foods; `NutritionInboxTests.cs`: scope/filtros/fechas/orden/volumen de inbox.
- `tests/CoppAddresd.UnitTests/Nutrition/NutritionInboxQueryTests.cs`, `NutritionFoodCatalogTests.cs`: validación y shapes de soporte; NutritionAuthorizationTests prueba ambas rutas por HTTP/JWT.

Captura adicional de auditoría en `%TEMP%/nutrition-contract-wave4`: un programa
temporal reutiliza por reflexión `NutritionAuthorizationTests.StartAsync` y
su Patient, sin cambiar los tests. Verificó HTTP 200 de ficha, 201 de generar,
200 de lista vacía y 404 fuera de alcance. El ejemplo app pasa por el handler
real con las mismas entradas del test de composición. Se guardaron JSON
literales para documentar casing/nulls y se parsearon los bloques JSON de este
documento. GUID dinámicos de captura no deben copiarse a datos reales.

Verificación histórica de Ola 4, anterior a implementar 4B:
`dotnet test tests/CoppAddresd.UnitTests --no-build --filter FullyQualifiedName~Nutrition --nologo -v quiet`:
**217 aprobados, cero fallos y cero omitidos**. No se repite PostgreSQL ni el
global de Ola 3 porque el código no cambió. El runner temporal compiló los
proyectos referenciados correctamente; no se ejecutó un nuevo build global.

Los cuerpos de acción son traducciones JSON de los inputs/aserciones de tests
CQRS indicados, no capturas de un despliegue. Los fragmentos están identificados;
las respuestas completas siempre tienen el DTO común. El test de generación
usa fallback de prueba; no hubo llamada al LLM, cambios de datos ni despliegue.
La auditoría inicial no implementó los huecos; la Ola 4B agrega endpoints de
soporte sin cambiar los contratos anteriores ni el adaptador móvil.

## 17. Verificación de los endpoints añadidos en Ola 4B

Inbox: build Release 0 errores; filtro unit Nutrition 238/238 y cuatro nuevas
integraciones PostgreSQL de inbox 4/4, sin omisiones. Se prueban permisos y
audiencia por HTTP, contrato, alcance propio/global/revocado, última versión,
filtros/total/página, zona local, empate, búsqueda literal y cancelación.
EXPLAIN del SQL real con 2000 pacientes/4000 prescripciones: 14,704 ms de
ejecución, dos consultas por página, usando índice existente por paciente.
No se modifican rutas anteriores ni se certifica una API desplegada.

Verificación final de ambos endpoints:

```text
dotnet build -c Release --nologo -v quiet
dotnet test tests/CoppAddresd.UnitTests -c Release --no-build --filter FullyQualifiedName~Nutrition --nologo -v quiet
dotnet test tests/CoppAddresd.IntegrationTests -c Release --no-build --filter FullyQualifiedName~Nutrition --nologo -v quiet
```

Build 0 errores (11 avisos preexistentes en rebuild); **254 unitarios y 38
integraciones PostgreSQL aprobados**, cero fallos/omitidos. Integración: 35
Workflow (30 anteriores +4 inbox +1 foods) y 3 FoodAiNutrition existentes.
El filtro amplio requiere COP_TEST_DB_CONNECTION a base de pruebas aislada
con migraciones Auth/API para los tests FoodAI; cada Workflow crea/elimina
su base propia. No se conecta al LLM ni a producción. Nuevos tests unit:
21 inbox y 16 foods, incluidos HTTP/JWT/capacidades/contratos/validación/CT.
Documentación: 18 bloques JSON válidos; las casillas 4B.A/4B.B están marcadas.
Debug local estaba en uso por API/Community; se validó en Release sin detener
esos servicios. La evidencia no implica que sus procesos hayan sido actualizados.

## 18. Ola 6 — generación automática (8.1)

El evento clínico `BatteryCompletedEvent(patientId,batteryAssignmentId)` se emite
después del commit sin gate Assignment.Enabled ni condición de inscripción.
El handler de asignación legacy permanece intacto con su gate propio. El consumidor
Nutrition reserva una batería inicial completa; un worker ejecuta el mismo caso
Generate con perfil medido e identidad técnica `system:nutrition`, nunca JWT de
paciente y nunca aprobación automática. Un reconciliador propio recupera hechos
perdidos consultando baterías completas, sin depender de jobs de asignación.

Persistencia nueva: `app.nutrition_draft_reservations`, unique paciente+batería.
Estados de proceso: `pending`, `generating`, `ready`, `failed`; no se confunden con
estados clínicos de prescripción. Lease 3 min, 5 intentos, demora de retry 1 min.
Un fallo de proveedor conserva borrador determinista y `fallback:true`; un fallo
de datos mínimos deja failed con código de motivo, sin inventar mediciones.
Los IDs de reserva y borrador coinciden: replay/reinicio entre commits no crea
otro ciclo. La reserva ready conserva `prescriptionId`. No hay datos clínicos
ni excepciones completas en el motivo/log del proceso.

Actor técnico: GUID reservado `00000000-0000-0000-0000-000000000061`, sin fila
auth.users ni profesional. createdBy/actorUserId usan ese GUID y el comentario
identifica `system:nutrition`. GUC: SYSTEM, user_id NULL y user_role system:nutrition.
No se incorporan correlationId/batteryAssignmentId al body ERP: el controller
no acepta del cliente los parámetros internos de correlación del caso Generate.

Coexistencia: pacientes con batería inicial activa/autoasignada o reserva nueva
se excluyen de nuevos encolados/procesados legacy, incluso antes de llegar el
evento. No modifica tablas ni planes legacy; otras cohortes conservan su productor.
La publicación actual y el DTO app de §14 permanecen intactos. La exposición
HTTP del proceso y recuperación se documentará en 8.2/8.3; 8.1 no agrega ruta pública.

## 19. Ola 6 — perfil medido completo y workspace (8.2)

Nuevo consolidado único `NutritionMeasuredProfiles.Resolve(NutritionCaseContext,
ClinicalContextDto, AssignmentProfileData?, mediciones reales, hoy[, input,
fallback])` sustituye el mapeo ad hoc de `GenerateAsync`. Procedencia declarada
por campo: `health_test_response` (biométrico de batería con unidad y rangos),
`health_test_result` (categorías clasificadas: movimiento, riesgo nutricional,
sueño con banderas IAP, adherencia IAC-ADRESD, riesgo cardiometabólico, estrés),
`clinical_measurements` (seguimiento ERP), `clinical_context` (consolidado), y
`patient_profile` para nacimiento/sexo. Glicemia y grasa corporal se resuelven
EXCLUSIVAMENTE desde mediciones reales (`clinical_measurements`/`clinical_context`);
sin medición quedan `"missing"` — nunca inferidas ni substituidas por manual.
El IMC medido tiene precedencia y se deriva solo en su ausencia. Al medido le
gana al manual si existe; el manual suple campos ausentes y queda marcado
`manual_or_preserved`. Faltantes explícitos con origen; `bateria_9_tests` se
enuncia si no hay batería completa. `CanGenerateWithoutManual` exige los mínimos
100% medidos (peso/talla/edad/sexo desde perfil).

El caso `Generate` reusa el consolidado y valida lo mínimo, sin daño al contrato:
`MissingDataJson` conserva los códigos anteriores (movement→mov, nutritionRisk→nutRiesgo,
sleepRiskFlags→sleepRF, sleepLevel→sleepNivel, testAdherence→adh,
cardiometabolicRisk→riesgoNum, stress→estres) para estabilidad del front; el
snapshot `ClinicalProfileJson` añade `measuredFields` (valor/unidad/fuente/código/
fecha) y `assessmentResults`. El request interno al ai-service amplía
`NutritionDraftRequest.clinicalProfile` con `engine_version`, `fields`,
`applied_rules` (flag/severidad/mensaje/técnica emitidos por el motor) y
`missing`; la validación de esquema `llm` sigue vigente y el payload POST no
contiene identificadores de paciente ni evaluation_id (test de cable lo
verifica; captura opcional `COP_NUTRITION_DRAFT_CAPTURE_PATH`).

API nueva (permiso Staff [Authorize] + NutritionPolicy): sesión
`GET /api/v1/nutrition/patients/{patientId}/workspace?historyPage&historyPageSize
&prescriptionId` — proyección `NutritionWorkspaceQuery` con permiso M o A del
actor, alcance por `patient_professionals` vía `CanAccessAsync` (404 fuera se
alcance, 403 sin permisos), identidad legible (nombre + documento + timezone),
hoy local del paciente vía `NutritionStateMachine.Today`, perfil medido,
faltantes, estado de generación (reserva: pendiente/generando/listo/fallido +
motivo + reintento + fallback + prescripción vinculada), prescripción específica
a revisar (404 si pertenece a otro paciente), vigente publicado hoy
(`GetCurrentPublishedAsync`) y bitácora paginada (items/total/page/pageSize,
orden OccurredAt/Id descendiente, actor técnico `system:nutrition` legible).
Easy: consultas AsNoTracking sin menús para identidad/proceso; historial con
join a cabecera, dos consultas, sin N+1.

Verificación: `dotnet build` 0 errores (11 avisos preexistentes); unit
`tests/CoppAddresd.UnitTests` 1662/1662, 0 fallos, 105 omitidos (omitidos de
infraestructura existente); integración `tests/CoppAddresd.IntegrationTests`
con `COP_TEST_DB_CONNECTION`: **97 aprobados, 3 fallos, 1 omitido**. Los 3
fallos (`FalloTrasCrearEnrollment_…`, `NueveTests_EventoPostCommit_…`,
`Job_RecuperaInscripcionPendiente…` de `ProgramAssignmentIntegrationTests`)
se reproducen IDÉNTICAMENTE sobre `c81da1b` limpio con el WIP en stash → deuda
preexistente del pipeline de asignación (ajena a esta tarea; se documenta para
misión separada). Tests unit nuevos: `NutritionMeasuredProfileTests` (categorías
publicadas sin inventar, ganancia del medido sobre manual, glicemia/grasa solo
con medición real, faltantes explícitos sin defaults saludables, mínimos con
teclado) y `NutritionDraftClientTests.WireContract_…` (perfil y reglas en el
cable bilingüe sin identificadores). FieldType/Refresh: `AssignmentResult`/
`AssignmentBiometric` reciben `ObservedAt` opcional poblado por
`ProgramAssignmentRepository` desde `CompletedAt` de la evaluación; el registro
sigue binario-comparable sin romper constructores de tests existentes.

## 20. Ola 8 — bandeja, recuperación y misiones

### 20.1 Tarea 8.3 — proceso y recuperación

§4 añade por fila `generationState` (pending/generating/ready/failed o null sin
reserva), `generationFallback` (false sin reserva), `generationReason` (código
técnico nullable, nunca datos clínicos) y `generationUpdatedAt` (UTC nullable).
Se toma la reserva más reciente por created_at DESC, id DESC. Dos consultas por
página, sin comidas; se conservan filtros, alcance y orden.

`POST /api/v1/nutrition/patients/{patientId}/draft-generation`, sin body,
requiere JWT ERP, Nutrition.Manage y alcance Nutrition propio/global.
Reserva por paciente+batería inicial completa, sin invocar IA en la petición.
Pending/generating son no-op, ready devuelve el borrador existente; failed se
reprograma inmediatamente con attempts conservado. Una recuperación manual
habilita un intento adicional aunque se hubieran agotado los cinco automáticos.
No acepta actor del cliente; las escrituras de recuperación usan actor JWT.

202 Accepted (pending; generating conserva ese state):
```json
{"reservationId":"55555555-5555-5555-5555-555555555555","state":"pending","prescriptionId":null}
```
200 OK (ready):
```json
{"reservationId":"55555555-5555-5555-5555-555555555555","state":"ready","prescriptionId":"55555555-5555-5555-5555-555555555555"}
```
Errores con el sobre ProblemDetails existente (§15): 401 sin token, 403 sin M
(o audiencia app), 404 paciente inexistente/fuera de alcance; 422 sin batería
inicial completa correlacionable. Ejemplos del sobre (correlationId y ruta de la solicitud definidos por middleware):
```json
{"type":"https://httpstatuses.io/404","title":"Not Found","status":404,"detail":"Paciente no encontrado.","instance":"/api/v1/nutrition/patients/11111111-1111-1111-1111-111111111111/draft-generation","correlationId":null}
```
```json
{"type":"https://httpstatuses.io/422","title":"Unprocessable Entity","status":422,"detail":"El paciente no tiene una batería inicial completa y correlacionable para generar el borrador.","instance":"/api/v1/nutrition/patients/11111111-1111-1111-1111-111111111111/draft-generation","correlationId":null}
```
