# Asignación del programa post-tests — paquete 3

Implementa 2.6, 2.7, 2.8 y 2.10, más el endpoint de desestimación solicitado
para la ficha. No incluye UI ERP ni regeneración. Reutiliza catálogo, motor, snapshot y narrativa de
los paquetes previos.

## Contrato REST ERP

Los tres endpoints exigen JWT y `Program.Enroll`. El actor y el alcance se
resuelven por `IProgramActorContext`, basado en ICurrentContext y
patient_professionals: mismos scopedPatientIds del listado de inscripciones.
La identidad del JWT nunca se recibe en el body. El alcance se comprueba antes
de devolver datos y forma parte del filtro SQL. Sin pacientes visibles la cola
responde 403; una ficha o apply ajenos/inexistentes responde 404.

### GET /api/v1/program/assignments

Parámetros: `status?`, `page=1`, `pageSize=20` (máximo 100), `search?` (máximo 200
caracteres; nombre completo o documento). Status acepta proposed,
pending-narrative, applied, edited, dismissed, fallback y all. Si se omite,
selecciona proposed y pending-narrative. Un estado no reconocido devuelve 400.
Page inferior a 1 se normaliza a 1; pageSize se limita a 1..100.

Respuesta 200:

```json
{
  "items": [{
    "id": "uuid", "enrollmentId": "uuid", "patientId": "uuid",
    "patientName": "Nombre Apellido", "documentNumber": "documento",
    "state": "proposed", "createdAt": "2026-11-04T15:00:00Z",
    "waitingDays": 2, "maxSeverity": 3, "narrativePending": true
  }],
  "total": 1, "page": 1, "pageSize": 20, "totalPages": 1
}
```

JOIN con patient_profiles (excluye soft delete), conteo y paginación en SQL.
Orden: días completos de espera DESC, severidad máxima del snapshot DESC,
nombre ASC, patientId, recommendationId. Severidad ordinal: 0 sin clasificación,
1 baja/leve, 2 moderada/estadio 1, 3 alta/estadio 2, 4 crítica/severa. Los días
se calculan desde created_at, sin contadores persistidos. No se cuenta como
KPI de dashboard: se evaluó CQRS y no se agrega otro rollup para este paginado.

Caché vía ICacheService/Valkey, TTL exacto 30 s, fail-open. Clave efectiva:
`erp:stats:program-assignments:{hash}:v1`. El hash incluye identidad del actor,
ids visibles ordenados, estado, página, tamaño, búsqueda y revisión. Crear o
aplicar una recomendación renueva `erp:program-assignment-revision:v1`, de modo
que las páginas anteriores expiran sin enumerar claves. Una revocación de
asignación cambia el scope antes de consultar la caché. La ficha no se cachea.

### GET /api/v1/program/assignments/{id}

Respuesta 200: `id`, `patientId`, `patientName`, `documentNumber`, `enrollment`
(`id,status,currentWeekNumber,startLocalDate,startedAt,timezone`),
`profileSnapshot` y `proposal` como objetos JSON completos, `narrativeEs`,
`narrativeEn`, `narrativePending`, `state`, `engineVersion`, `modelName`,
`appliedBy`, `dismissedBy`, `createdAt`, `updatedAt`, `appliedAt`, `dismissedAt`.
NarrativePending indica que falta al menos uno de los idiomas; no se fabrica
narrativa si el proveedor falló. Cruce de pacientes: 404 sin distinguir existencia.

### POST /api/v1/program/assignments/{id}/apply

Body: `{ "overrideNutritionPlanName"?: "nombre", "overrideRoutineName"?: "nombre",
"apply": true }`. No admite seleccionar actor, alcance ni fallback desde HTTP.
False o nombres de más de 200 caracteres: 400. Nombre que no resuelve a contenido
Active disponible para el paciente: conflicto de negocio; no se activa a medias.

Respuesta 200: `{recommendationId,enrollmentId,enrollment,patientId,state,startLocalDate,
currentWeekNumber:1,appliedContent,replay}`. AppliedContent contiene nutritionPlan
y routine resueltos (id/nombre), indicador templateDefaults y tasksSnapshot.
En defaults por día, los IDs concretos se conservan dentro de tasksSnapshot.

La transacción usa estrategia Npgsql y locks en orden perfil → enrollment →
recommendation. Verifica PendingAssignment y estado proposed/pending-narrative.
Lee plantilla activa y tareas en una consulta consistente, hornea el mismo
shape BuildSnapshot existente, conserva defaults y reemplaza nut/ejercicio por
lo seleccionado. Semana 1 queda Active, sin mover el arranque al lunes. Todas
las semanas se reubican desde hoy local del perfil; StartedAt/ActivatedAt son
UTC. Se fija applied_by con el JWT y state applied o edited cuando se recibió
un override. La propuesta original y matchedRules se conservan; appliedContent
registra la decisión final. Se inserta auditoría semántica en la transacción.

El replay de applied/edited/fallback devuelve la decisión ya confirmada con
replay=true, sin avisos extra. Dismissed no se reactiva. El PUT de contenido
semanal se mantiene: puede configurar las siguientes semanas del mismo programa.

### POST /api/v1/program/assignments/{id}/dismiss

Sin body. Requiere `Program.Enroll`; actor y scopedPatientIds provienen del JWT,
igual que apply/ficha. Responde 200 con `AssignmentDetailDto` actualizado. Un
identificador inexistente o fuera del alcance responde 404 sin revelar existencia.
Solo proposed/pending-narrative cambian a dismissed, con dismissed_by,
dismissed_at y updated_at; otros estados terminales distintos de dismissed
producen conflicto de negocio. La escritura condicional y la auditoría semántica
se confirman en una transacción; concurrencia con apply no desestima una decisión
ya aplicada. Un replay de dismissed conserva actor/timestamps y no vuelve a
invalidar la caché ni agregar auditoría. La inscripción y sus semanas no se
modifican: siguen PendingAssignment/Locked. Se mantiene el fallback existente,
que excluye dismissed; desestimar no programa activación automática ni envíos.

## Reconciliación y fallback

ProgramAssignmentHostedService ejecuta ProgramAssignmentJob cada
`Program:Assignment:JobTickMinutes` (default 5). Con Enabled=false no procesa
hooks ni fallbacks. TimeProvider inyectable permite pruebas deterministas.

Recuperación: deriva baterías iniciales completas usando la misma división
relacional del submit (required + evaluación completed). Incluye ausencia de
inscripción y PendingAssignment sin recommendation. Reutiliza el evento,
transacción y snapshot, sin duplicar la inscripción preexistente. Claims en
app.program_assignment_recoveries registran Attempts/NextAttemptAt; máximo
RecoveryMaxAttempts=5 y backoff RecoveryBaseDelaySeconds=60 × 2^(intento-1).
El claim es INSERT ON CONFLICT condicionado: dos instancias no toman el mismo
intento. Al agotarse, el registro conserva la evidencia; un operador puede
reprogramarlo por DML tras corregir la causa.

Fallback: state proposed/pending-narrative y created_at <= ahora UTC - 7 días.
El job y la transacción vuelven a comprobar el plazo. Usa el mismo
ApplyAssignmentCommand con UseTemplateDefaults interno, ignora targets y
hornea defaults de plantilla, fija fallback y actor System. Los locks y el
estado terminal hacen idempotentes pasadas concurrentes; nunca reactiva una
desestimada ni un programa ya aplicado.

## Avisos bilingües y entrega

La migración aditiva AddProgramAssignmentRecoveryAndNotices crea recoveries y
app.program_assignment_notices. El único por (recommendation,user,kind) se
persiste en la misma transacción que la recomendación/activación.

Profesional: destinatarios con relación paciente-profesional Active y empleado
Active con usuario. Copy ES: «El paciente X completó la evaluación inicial y
espera asignación de programa»; EN: «Patient X completed the initial assessment
and is waiting for program assignment». Enlace `/program/asignacion/{id}`.
La campana previa del ERP solo tenía push web, sin inbox persistente: se añade
`GET /api/v1/notifications/inbox?page=1&pageSize=20`, autenticado y con
Program.Enroll, filtrado por usuario JWT y relación vigente. Devuelve
`{items:[id,recommendationId,kind,titleEs,titleEn,messageEs,messageEn,link,createdAt,readAt],total,page,pageSize}`.
El frontend no se modifica en este paquete; debe consumir este inbox.

Paciente: copy ES «Tu plan personalizado está listo - empieza tu semana 1»;
EN «Your personalized plan is ready - start your week 1». Dispatch post-commit
reutiliza SendPushNotificationCommand de UC-001, tokens FCM y chat proactivo,
sin LLM. Se guarda progreso independiente de push/chat para no repetir el canal
ya confirmado. Metadata del push incluye route, recommendation_id y ambos copies.
Idioma de entrega Program:Assignment:NotificationLanguage (default es) hasta que
exista una preferencia de idioma por usuario; el registro siempre conserva ES/EN.

Claims de entrega con lease de 15 minutos y hasta 5 intentos; fallo parcial se
reintenta a los 5 minutos desde el job. La activación no depende del proveedor.
Apply/fallback y replays no crean avisos duplicados. La entrega externa es al
menos una vez: una caída después de enviar y antes de confirmar puede repetir
un canal, porque FCM/chat no ofrecen aquí una clave transaccional compartida.
El inbox y la decisión clínica permanecen durables.

## Evidencia de pruebas

Unit: cola/controlador con fakes, permiso de los endpoints, separación de caché,
override/actor/reloj, día 6 sin activación, día 7 una sola vez y reintento por canal.
Integración PostgreSQL: profesional B recibe 404 al consultar paciente de A;
apply en miércoles fija fechas y semana 1, permite check-in del día y PUT de la
semana 2; día 8 activa defaults una sola vez; recuperación de un pending huérfano;
claims concurrentes de entrega y límites/backoff persistidos. Se ejecutan además
las regresiones del submit del paquete 2. No se envían push ni mensajes reales
durante estos tests: los proveedores se sustituyen por fakes.

Validación local del 2026-11-04: build de solución Release verde; unit tests
1297 aprobados, 103 omitidos, 0 fallos; integración del módulo 7/7, incluida
migración Down/reaplicación en BD aislada. Migración
`20261104161744_AddProgramAssignmentRecoveryAndNotices` aplicada en dev y
verificada con psql; EF no detecta cambios de modelo pendientes. Sin commits.

Verificación del ajuste dismiss (2026-11-04): build Release verde; unit tests
1300 aprobados, 103 omitidos, 0 fallos; nueva integración indirecta 1/1
(controlador → MediatR → PostgreSQL, anti-IDOR, replay, auditoría tipada y
ninguna activación ni aviso al paciente). Sin commits ni migración adicional.

## Recuperación y regeneración de narrativa (QA, 2026-11-04)

`POST /api/v1/program/assignments/{id}/regenerate-narrative` no recibe body.
Exige `Program.Enroll` y obtiene el alcance de pacientes desde el actor del JWT,
igual que la ficha. Devuelve 202 `{queued:true}` al encolar o si el mismo ID ya
está pendiente/en procesamiento; devuelve 202 `{queued:false}` si el canal está
lleno (best-effort, permite reintentar). Inexistente/fuera de alcance: 404;
dismissed visible: 409 `ASSIGNMENT_DISMISSED` con mensaje en español. No elimina
narrativas anteriores: solo las sustituye al obtener ambos idiomas válidos del
parser existente, en la misma fila. No cambia snapshot, propuesta ni inscripción.

El worker ahora barre al arrancar y cada `JobTickMinutes` (default 5), además de
consumir el canal. La versión anterior no tenía SELECT de recuperación. El nuevo
debounce `NarrativeDebounceSeconds` (default 60) exige antigüedad de creación y del
último intento; el lote usa `JobBatchSize` limitado a 1–500. El predicado SQL
equivalente, compartido con las pruebas mediante una expresión traducible, es:

```sql
SELECT id FROM app.program_assignment_recommendations
WHERE (
    (state IN ('proposed','pending-narrative')
      AND (narrative_es IS NULL OR narrative_en IS NULL))
    OR (state IN ('applied','edited','fallback')
      AND narrative_es IS NULL AND narrative_en IS NULL)
  ) AND created_at <= @cutoff
    AND (updated_at IS NULL OR updated_at <= @cutoff)
ORDER BY created_at, id LIMIT @batch;
```

Excluir propuestas con ambos idiomas completos evita que ocupen el lote e impidan
recuperar deuda antigua. Applied/edited/fallback conservan su estado al iniciar y
guardar; solo pending-narrative vuelve a proposed. Dismissed queda fuera del
SELECT, del inicio y del guardado, incluso si se desestima durante la llamada IA.
La regeneración manual permite sustituir narrativas ya existentes en cualquier
estado salvo dismissed. Deduplicación del canal por ID hasta terminar todos los
intentos; no se prometen exclusión distribuida entre réplicas ni entrega durable
de regeneraciones explícitas con narrativa existente. La deuda sin narrativa se
recupera de PostgreSQL tras reiniciar. No se introducen KPIs ni nuevos rollups.

Verificado: solución Release verde; unit 1319 aprobados, 103 omitidos y 0 fallos;
integración PostgreSQL aislada del módulo 9/9, incluyendo selección por estado,
guardado conservando applied/edited/fallback, regeneración con narrativa previa,
replay y anti-IDOR. Sin migración, llamadas a IA reales ni commits.

## Contrato real con ai-service (QA, 2026-11-04)

Corregido el 422 por omisión de `source_refs`. La fuente de verdad es
`../ai-service/app/schemas/program_narrative.py`: request con `profile_snapshot`,
`proposal`, `language` y `source_refs`; respuesta exclusivamente
`{"narratives":{"es":"...","en":"..."}}`. El processor deriva fuentes de
`conditions[].sourceRef` solo con `active:true`, recorta espacios, omite textos
vacíos/de más de 500 caracteres, deduplica con comparación ordinal y toma hasta
100. Sin fuentes activas envía `[]`; no fabrica ni recorta citas clínicas.

Infrastructure adapta el wrapper a `ProgramNarrativeResponse` para conservar
la persistencia ES/EN existente. Rechaza traducciones ausentes/vacías o mayores
de 2000 caracteres; normaliza espacios, como Pydantic. ModelName queda null:
el servicio no lo publica. El formato plano anterior se rechaza.

Simulación reproducible y validación del body bajo
`postman/simulations/program-assignment-narrative.sim.yaml`; instrucciones y
límites del ensayo en su archivo `.context.md`. Build Release verde; unit 1329
aprobados, 103 omitidos, 0 fallos; cliente .NET contra simulación HTTP local 1/1
(200, fuentes vacías, 503 y apertura de circuito). Es evidencia simulada, sin
llamar al LLM ni certificar el despliegue del ai-service real. Sin commits.
