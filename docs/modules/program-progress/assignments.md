# Asignación del programa: insumos y recuperación conservados

La Ola 3 retira toda la API ERP `/api/v1/program/assignments*`: cola, ficha,
aplicar, desestimar y el disparador HTTP de narrativa. No hay rutas sustitutas
para ese flujo. El módulo Nutrition gestiona las nuevas prescripciones.
El registro, el perfil medido, snapshot y narrativa permanecen intactos.
`Program:Assignment:Enabled` sigue bajo control del owner en ECS; no se cambia
ningún valor ni se añade un flag alternativo.

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
ApplyAssignmentFallbackCommand interno, sin overrides ni actor ERP, ignora targets y
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
and is waiting for program assignment». Enlace `/program/gestion?view=perfil-360&patient={patientId}`.
Los avisos existentes se proyectan hacia ese destino al leer el inbox, sin
reescritura de datos ni cambios de entrega.
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
Fallback y replays no crean avisos duplicados. La entrega externa es al
menos una vez: una caída después de enviar y antes de confirmar puede repetir
un canal, porque FCM/chat no ofrecen aquí una clave transaccional compartida.
El inbox y la decisión clínica permanecen durables.

## Recuperación y regeneración interna de narrativa

`RegenerateNarrativeCommand` permanece como caso interno; su endpoint ERP fue
retirado junto con la ficha. Consulta solo estado y alcance con
`GetNarrativeStateAsync`, sin reconstruir el DTO eliminado. Conserva la
narrativa vigente hasta obtener ambos idiomas válidos; no cambia snapshot,
propuesta ni inscripción. Fuera del alcance devuelve null antes del conflicto
de dismissed. El canal admite reintentos y deduplica solicitudes pendientes.

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
