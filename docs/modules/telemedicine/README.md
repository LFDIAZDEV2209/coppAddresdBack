# Módulo de Telemedicina — Microservicio

## Estado

Fases implementadas:

- **Fase 0 — Scaffold**: microservicio `src/Services/CoppAddresd.Telemedicine` (puerto **5130** http / 7130 https), Clean Architecture por carpetas (precedente: Auth Service), JWT del Auth Service (mismo secret/issuer/audiences), Swagger, `/health`.
- **Fase 1 — Dominio + persistencia**: schema `tele.` con 9 tablas, migración `AddTelemedicineSchema` aplicada.
- **Fase 2 — IVideoProvider + Twilio**: contrato agnóstico en Application, `TwilioVideoProvider` en Infrastructure (SDK oficial validado contra la cuenta real), validación de firma de webhook, DI config-driven.
- **Fase 3 — Requests + Appointments + agendamiento**: repositorios del agregado (cita + solicitud), casos de uso MediatR (crear solicitud, confirmar → cita, agendar directo, cancelar, reprogramar), agenda del profesional, validadores, controladores `/api/v1/telemedicine/*`. **Anti doble reserva real en BD**: constraint de exclusión GiST sobre solapamiento de citas activas + índice único parcial de inicio + índice único sobre `request_id` (migración `AddAppointmentOverlapExclusion`). Datos de referencia (profesionales/pacientes/especialidades/sedes) validados contra el backend vía internal endpoints.
- **Fase 4 — Salas y sesiones**: join-token (creación idempotente de sala Twilio dentro de la ventana), consulta de sala con participantes en vivo, `session/start` y `session/end` (cita `Confirmed→InProgress→Completed`), webhooks del proveedor (firma validada, **idempotentes** por clave única en `tele.telemedicine_webhook_events`, procesamiento atómico), resolución de identidad user→profesional/paciente (internal endpoints `by-user`), permiso `Telemedicine.SessionsManage`. Detalle en la sección Fase 4.
- **Fase 5 — Encuentro clínico (espacio clínico durante la consulta)**: consulta/guardado/finalización del registro clínico de la cita. `clinical_data` jsonb tipado y extensible + `notes`. Creación perezosa idempotente (1:1 cita→encuentro). Autorización por identidad del profesional o supervisor (`SessionsManage`); el paciente NO accede (PHI). Estados `Draft→Completed` (inmutable) y `Cancelled` (al cancelar una cita con borrador). Detalle en la sección Fase 5.
- **Fase 6 — Bandeja de alertas y notificaciones**: materialización de eventos de dominio en `tele.telemedicine_alerts` (tabla y enums listos desde Fase 1) y endpoints de bandeja del profesional + vista administrativa. Alerta emitida en: nueva solicitud → al profesional elegido; cita creada/confirmada → al profesional asignado; reprogramación → al profesional; cancelación → al profesional; eventos de sesión vía webhook (`participant-connected`/`disconnected`/`room-ended`) → al profesional de la cita. Permiso `Telemedicine.AlertsView` (vista global) vs identidad (bandeja propia del profesional). Detalle en la sección Fase 6.
- **Fase 7 — Datos de referencia / maestros para la UI**: catálogo de **profesionales clínicos** en el backend (`GET /api/v1/professionals-catalog`, paginado, filtros por especialidad/sede/búsqueda, sin PHI) + endpoints del microservicio para la UI: `GET /api/v1/telemedicine/me` (resuelve profesional/paciente del JWT) y listados admin (`/admin/summary`, `/admin/appointments`, `/admin/requests`, `/admin/sessions`) bajo el nuevo permiso `Telemedicine.AdminView`. Detalle en la sección Fase 7.
- **Fase 8 (adelantada) — Permisos `Telemedicine.*`**: siembra en el Auth Service (`PermissionCodes` + `RoleSeeder`), autorización por claim `permission` en el microservicio (mismo mecanismo que el backend).
- **Fase 9-10 — Frontend (`coppaddresd-front`)**: módulo `features/telemedicine/*` (types espejo de los DTOs, services del microservicio 5130 y de catálogos del backend 5122, hooks, componentes) + rutas `/telemedicine/*` (profesional: dashboard, agenda, calendario, solicitudes, alertas, detalle de cita con sala virtual y encuentro clínico; admin: dashboard con KPIs, citas, solicitudes, profesionales, sesiones) protegidas con `PermissionGate` (`Telemedicine.AdminView`). Detalle en la sección Fase 9-10.
- **Fase 11 — Testing formal**: proyectos `tests/CoppAddresd.Telemedicine.UnitTests` (134 tests: reglas puras de agendamiento/sala/encuentro, materializador de alertas, guard de referencias, mappers, validadores y handlers con fakes en memoria) e `tests/CoppAddresd.Telemedicine.IntegrationTests` (25 tests contra PostgreSQL real en BD aislada `coppaddresd_tele_test_*` — creada, migrada y eliminada por corrida vía `COP_TEST_DB_CONNECTION`): anti doble reserva concurrente (exclusión GiST + índice único parcial), idempotencia del webhook (clave única + rollback), persistencia del agregado, idempotencia de sala/encuentro y fallback de settings.
- **F3 — Experiencia de llamada (backend)**: capacidad de sala por defecto **3** (validada 2–10) con elevación perezosa de las salas creadas antes de F3 (`UpdateRoomMaxParticipantsAsync`, REST de Twilio, best-effort) en `join-token`/`session/start` y corrección de la reapertura para que la sala nueva herede el settings vigente; **chat clínico persistido** `tele.chat_messages` + `GET/POST /api/v1/appointments/{id}/chat/messages` (REST + polling incremental con cursor, misma autorización que la sala). Migración `AddRoomChatAndCapacity`. Detalle en la sección F3.
- **Fase 12 — Hardening**: auditoría clínica del encuentro — `tele.clinical_encounters` adjunta el trigger `audit.audit_trigger_function` vía migración condicional (`AttachClinicalEncounterAudit`, 4ª migración de `tele.`), y el actor del JWT + correlation id se propagan a los GUC `audit.*` por `AuditTriggerInterceptor`/`HttpAuditActorContext` (el guardado del encuentro usa transacción explícita corta para que el interceptor dispare). Bugs reales corregidos (descubiertos en el E2E de la fase): idempotencia del proveedor ante "Room exists" de Twilio (409/20429 → recupera la sala existente) y tracking `Added` de sesiones nuevas en el agregado (el fixup de EF las marcaba `Modified` → 409 de concurrencia al iniciar sesión tras un join previo). Detalle en la sección Fase 11-12.
- **F4 — Clínica y cumplimiento (backend)**: **pre-consulta del paciente** `tele.pre_visit_intakes` (1:1 con la cita, `GET/PUT /api/v1/appointments/{id}/pre-visit-intake`, edición solo con la cita `Confirmed`, autoría del paciente) y **adendas del encuentro** `tele.encounter_addenda` (append-only, `GET/POST .../encounter/addenda`, solo con el encuentro `Completed`); **auditoría del ciclo de vida** con triggers condicionales en `appointments`/`telemedicine_sessions`/`virtual_rooms` (`AttachAppointmentLifecycleAudit`) y transacción explícita en `AppointmentRepository` para atribuir el actor del JWT. Grabación + consentimiento (F4.1–F4.4) **no** se implementan en esta fase (sub-proyecto separado). Migraciones `AddPreVisitIntakeAndAddenda` y `AttachAppointmentLifecycleAudit`. Detalle en la sección F4.
- **F5 — Operación y calidad (backend)**: **métricas de llamada** en la pre-agregación CQRS (`rooms_opened`, `sessions_started`, `sessions_ended`, `session_duration_seconds`, `reopens`, `chat_messages_sent` por rol) como claves nuevas de la tabla EAV `tele.appointment_daily_metrics` (**sin migración de esquema**), **gracia de reapertura configurable** `telemedicine_settings.reopen_grace_minutes` (default 60, rango 5–1440, migración `AddReopenGraceMinutes`), bloque aditivo `calls` en los endpoints de analytics existentes y **corrección de la deriva** del barrido/webhook (ahora emiten al pipeline). Detalle en la sección F5.

Pendiente: integración con el SDK de video del navegador (Twilio) en la sala virtual (el `join-token` ya se genera y se muestra); alerta `UpcomingAppointment` en la bandeja (los recordatorios push/SMS de F2 ya se disparan por scheduler); `room-ended` sin sesión → NoShow (decisión de negocio aparte); exponer el filtro `clinicId` en la UI admin (el backend ya lo acepta); propagación de actor en el BACKEND (su `HttpAuditActorContext` sigue devolviendo System; el microservicio ya la tiene resuelta vía JWT).

## Arquitectura

El microservicio es independiente del proveedor de video. El dominio de telemedicina depende de `IVideoProvider` (Application), nunca de Twilio.

```text
Controllers → MediatR (Application) → Domain
                        │
                        └→ IVideoProvider (Application)
                                │
                        TwilioVideoProvider (Infrastructure)
```

- **BD**: misma instancia PostgreSQL compartida, **solo schema `tele.`**. El historial de migraciones vive en `tele.__ef_migrations_history` (aislado del `public.__EFMigrationsHistory` del backend).
- **Datos maestros** (pacientes, profesionales, sedes, especialidades): referencias **débiles por Id** (columnas UUID indexadas, **sin FK cross-schema**). El microservicio NO posee esos datos; los leerá del backend vía internal endpoints (Fase 7).
- **Convención snake_case** vía `UseSnakeCaseNamingConvention()` (paquete `EFCore.NamingConventions`). NO usar `SetRegion` con el SDK Twilio: el host de Video es `video.twilio.com` (sin sufijo); `SetRegion("us1")` genera `video.us1.twilio.com` que no resuelve.

## Schema `tele.`

| Tabla                       | Notas                                                                                                                                                                                                                                                                                                                                 |
| --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `telemedicine_requests`     | Solicitud del paciente (referencias débiles, estado `Pending/Approved/Rejected/Cancelled/Converted`)                                                                                                                                                                                                                                  |
| `appointments`              | Cita (agregado raíz, ex `telemedicine_appointments` renombrada en la migración `RenameTelemedicineAppointmentsToAppointments`). `xmin` como token de concurrencia; **índice único parcial** `ix_appointments_professional_start_active` sobre `(professional_id, scheduled_start)` WHERE status IN activos → anti doble reserva en BD |
| `appointment_cancellations` | Historial append-only de cancelaciones                                                                                                                                                                                                                                                                                                |
| `appointment_reschedules`   | Historial append-only de reprogramaciones                                                                                                                                                                                                                                                                                             |
| `virtual_rooms`             | Sala en el proveedor (`provider`, `provider_room_sid`, `provider_room_name` único por proveedor → base de la idempotencia)                                                                                                                                                                                                            |
| `telemedicine_sessions`     | Sesión de video (estado independiente de la cita y de la sala)                                                                                                                                                                                                                                                                        |
| `clinical_encounters`       | Encuentro clínico, `clinical_data` jsonb extensible                                                                                                                                                                                                                                                                                   |
| `telemedicine_alerts`       | Bandeja (eventos de dominio materializados; canal de entrega desacoplado)                                                                                                                                                                                                                                                             |
| `notification_dispatch`     | Despachos de notificación F2: dedupe por `(appointment_id, kind)` (único) — un recordatorio nunca se envía dos veces                                                                                                                                                                                                                  |
| `chat_messages`             | Chat clínico F3: mensaje de texto plano por cita (`sender_user_id`/`sender_role` derivados del JWT), índice `(appointment_id, created_at)` para la lectura incremental por cursor; sin edición/borrado                                                                                                                                 |
| `pre_visit_intakes`         | Pre-consulta del paciente F4: 1:1 con la cita (único `appointment_id`), motivo obligatorio + síntomas/alergias/medicación opcionales, autoría del paciente (JWT); editable solo con la cita `Confirmed`                                                                                                                               |
| `encounter_addenda`         | Adendas del encuentro F4: append-only (encuentro, autor + snapshot de nombre, texto 1–2000, fecha), índice `(encounter_id, created_at)`; el registro clínico original no se modifica                                                                                                                                                    |
| `telemedicine_settings`     | Reglas parametrizadas por organización/clínica (`max_participants` default 3 desde F3, rango 2–10; `reopen_grace_minutes` default 60 desde F5, rango 5–1440)                                                                                                                                                                        |

**Estados separados a propósito**: cita ≠ sesión ≠ sala (máquinas de estado independientes).

## IVideoProvider

```csharp
CreateRoomAsync(RoomRequest, ct)          // idempotente por RoomName determinista ("apt-{id}")
GetRoomAsync(sidOrName, ct)               // null si no existe (404 mapeado)
CompleteRoomAsync(sid, ct)                // finaliza sala
GenerateAccessTokenAsync(AccessTokenRequest, ct)  // JWT identity + room, TTL corto
GetParticipantsAsync(sid, ct)
UpdateRoomMaxParticipantsAsync(sid, maxParticipants, ct)  // F3: POST /v1/Rooms/{Sid} (REST del SDK)
ValidateWebhookSignatureAsync(WebhookValidationRequest, ct)  // X-Twilio-Signature
```

Añadir otro proveedor = nueva clase que implemente `IVideoProvider` + un `else if` en `DependencyInjection.AddVideoProvider` (config `Telemedicine:Provider`).

## Fase 3 — Agendamiento (API)

Controladores bajo `/api/v1/*` (JWT del Auth Service, autorización por claim `permission`).

> **Transición de rutas (zero-downtime)**: los endpoints de CITA usan el path nuevo `/api/v1/appointments/*`. El path viejo `/api/v1/telemedicine/appointments/*` sigue funcionando como **alias** en el gateway (transform `PathPattern` → `/api/v1/appointments/*`) y se retirará cuando el frontend migre. Los demás (`requests`, `alerts`, `admin`, `me`, `webhooks`) siguen bajo `/api/v1/telemedicine/*`.

```
POST   /api/v1/telemedicine/requests                    # Paciente solicita (Pending)      [Telemedicine.RequestsCreate]
GET    /api/v1/telemedicine/requests/{id}               # Detalle de solicitud             [Telemedicine.RequestsView]
GET    /api/v1/telemedicine/requests/mine?patientId=    # Solicitudes del paciente         [Telemedicine.RequestsView]
POST   /api/v1/telemedicine/requests/{id}/confirm       # Confirma → crea cita Confirmed   [Telemedicine.RequestsConfirm]
POST   /api/v1/telemedicine/requests/{id}/approve       # Aprueba (Pending → Approved)     alcance dual: AdminView o profesional asignado
POST   /api/v1/telemedicine/requests/{id}/reject        # Rechaza con motivo (máx 500)     alcance dual: AdminView o profesional asignado
```

**Ciclo de revisión de solicitudes (2 pasos)**: aprobar (`Pending → Approved`, sin crear cita; alerta `RequestApproved` al profesional cuando la aprobación la hace un admin) → confirmar con fecha (crea la cita y la solicitud pasa a `Converted`). Rechazo (`Pending|Approved → Rejected`): motivo obligatorio persistido en `telemedicine_requests.rejection_reason` (migración `AddRequestRejectionReason`), alerta `RequestRejected` al profesional asignado si es resoluble. Los endpoints `approve`/`reject` **no llevan `[RequirePermission]`**: el handler resuelve el alcance dual (claim `Appointments.AdminView` o profesional asignado por identidad del JWT, patrón de la bandeja de alertas) y devuelve 403 sin alcance.

POST /api/v1/appointments # Agendamiento directo del doctor [Telemedicine.AppointmentsSchedule]
GET /api/v1/appointments/{id} # Detalle de cita (nombres resueltos) [Telemedicine.AppointmentsView]
GET /api/v1/appointments/agenda?professionalId&from&to # Agenda/calendario [Telemedicine.AgendaView]
POST /api/v1/appointments/{id}/cancel # Cancelar (historial append-only) [Telemedicine.AppointmentsCancel]
POST /api/v1/appointments/{id}/reschedule# Reprogramación inmediata [Telemedicine.AppointmentsReschedule]

```

**Reglas de negocio** (todas parametrizadas en `tele.telemedicine_settings`): duración (default 30 min, máx 240), anticipación mínima, ventana máxima, límite de reprogramaciones (default 2). La reprogramación es inmediata y registra `appointment_reschedules` (historial append-only); la cita vuelve a `Confirmed` con la nueva hora.

**Concurrencia (anti doble reserva)** — tres capas:

1. Verificación de solapamiento en aplicación (`IAppointmentRepository.HasActiveOverlapAsync`) → error amigable 409.
2. Índice único parcial `ix_appointments_professional_start_active` (mismo inicio exacto).
3. **Constraint de exclusión GiST** `ex_appointments_professional_no_overlap` (migración `AddAppointmentOverlapExclusion`, requiere extensión `btree_gist`) → garantía real ante dos reservas simultáneas (exclusion violation traducida a 409). Además índice único parcial `ix_appointments_request_id`: una solicitud → una sola cita (anti doble confirmación).

**Datos de referencia**: el microservicio NO posee los datos maestros. Valida existencia y resuelve nombres contra internal endpoints del backend (`X-Internal-Key`):

```

GET /api/v1/internal/telemedicine/professionals/{id} # id = erp.professionals (no employee)
GET /api/v1/internal/telemedicine/patients/{id}
GET /api/v1/internal/telemedicine/specialties/{id}
GET /api/v1/internal/telemedicine/locations/{id}

````

La lectura en listados deduplica por entidad única (sin N+1). Si el backend no responde → 503 (`UpstreamUnavailableException`).

## Fase 4 — Salas y sesiones (join-token, ventana, webhooks, finalización)

Ciclo de video por cita (estados separados a propósito: cita ≠ sesión ≠ sala):

```text
Cita Confirmed → (join-token dentro de la ventana) → sala creada en Twilio (lazy)
Cita Confirmed/InProgress → session/start → sesión Active + cita InProgress
sesión Active → session/end | webhook room-ended → sesión Ended + sala Ended + cita Completed
````

### API

```text
POST /api/v1/appointments/{id}/join-token     # token de acceso (crea la sala si no existe) — participante o supervisor
GET  /api/v1/appointments/{id}/room           # sala + participantes en vivo — participante o supervisor
POST /api/v1/appointments/{id}/session/start  # inicia sesión → cita InProgress — profesional o supervisor
POST /api/v1/appointments/{id}/session/end    # finaliza → cita Completed (idempotente) — profesional o supervisor
POST /api/v1/telemedicine/webhooks/twilio                  # eventos de Twilio (firma validada, idempotente) — anónimo
```

La autorización de los 4 primeros se resuelve en el handler a partir del JWT: el participante se deriva de la identidad (user → profesional/paciente de la cita) o del permiso de supervisión `Telemedicine.SessionsManage`. Por eso los endpoints NO llevan `[RequirePermission]`: el paciente (app móvil) no tiene roles ERP y se autoriza por identidad.

### Reglas de negocio

- **Ventana de acceso**: abre `RoomOpenBeforeMinutes` (default 10) antes del inicio y cierra `RoomCloseAfterMinutes` (default 15) después del fin (settings por org/clínica); una cita reabierta extiende la ventana desde `reopened_at` (ver reapertura). `join-token`/`session/start` fuera de la ventana → 409.
- **Sala lazy e idempotente**: se crea en el primer `join-token`/`start` dentro de la ventana. Nombre determinista `apt-{appointmentId}` → idempotencia por índice único `(provider, provider_room_name)` + `UniqueName` de Twilio (una carrera entre dos join-token devuelve la misma sala).
- **Capacidad (F3)**: `settings.MaxParticipants` (default 3, rango 2–10) se copia al crear la sala; si la sala ya existe con un límite menor, `join-token`/`session/start` la elevan de forma perezosa (proveedor best-effort + persistencia local) y la reapertura crea la sala nueva con el settings vigente. La autorización de participantes NO cambia: la cita sigue siendo 1:1 (profesional/paciente/supervisor) y el cupo solo protege el ingreso de supervisores.
- **Sesión**: una activa a la vez por cita (`start` doble → 409, protegido además por el token de concurrencia xmin de la cita). `end` es idempotente: sin sesión activa → no-op 200.
- **Webhooks**: firma `X-Twilio-Signature` validada (deshabilitada en dev, `Twilio:ValidateWebhookSignature`). Clave de idempotencia `(event_type, room_sid, participant_sid)` en `tele.telemedicine_webhook_events` (índice único): los duplicados concurrentes se serializan y el perdedor recibe `Duplicate` con rollback de sus mutaciones. El procesamiento es atómico (reserva de la clave + mutaciones en una transacción).
  - `room-ended` → sala `Ended`, sesión activa `Ended`, y la cita `InProgress` pasa a `Completed`. Si nunca hubo sesión activa, la cita NO se completa automáticamente (el barrido de sesiones estancadas resuelve esos casos).
  - `participant-connected` → sala `Active` (si estaba Created/Waiting) + timestamp del evento en la sesión. Si la identidad del participante es distinta a la del profesional, marca `tele.virtual_rooms.patient_joined_at` (primera vez): insumo del barrido para distinguir NoShow de Completed. `participant-disconnected` → solo timestamp.
- **Barrido de citas vencidas**: `StaleSessionSweepHostedService` (cada 5 min, delay inicial 1 min) cierra citas vencidas más la gracia de `RoomCloseAfterMinutes` efectiva (settings por org/clínica): `InProgress` → `NoShow` si el paciente nunca ingresó (`patient_joined_at` null), `Completed` si ingresó; `Confirmed` sin sesión iniciada → `NoShow`. La sala del proveedor se completa best-effort y la sesión activa se cierra con `end_reason = stale-sweep`.
- **Ventana en el detalle**: `GET /appointments/{id}` incluye `RoomOpensAt`/`RoomClosesAt` calculados con los settings efectivos; `GET /appointments/mine` (app móvil del paciente) también los expone para el pre-join — sala persistida si ya existe; si no, `SessionSupport.Window` (reapertura incluida). Las listas admin/ERP los omiten — la UI decide el estado de la sala sin esperar la creación lazy.
- **Completar la sala en Twilio es best-effort** en `session/end`: si Twilio no responde, la sesión/cita se finalizan igual (la sala termina sola o vía webhook).
- **Reapertura (gracia configurable, F5)**: `POST /api/v1/appointments/{id}/session/reopen` reabre una cita `Completed` dentro de la gracia efectiva `settings.ReopenGraceMinutes` (default 60) desde `completed_at`, por el profesional asignado o un supervisor con `Appointments.SessionsManage` (pacientes: sin acceso). El settings se carga **antes** del check y se valida el rango 5–1440 (`SessionSupport.EnsureValidReopenGraceMinutes`; fuera de rango → 409 sin efectos). Efectos: la cita vuelve a `InProgress` (`reopened_at`, `reopen_count++`), se crea una sala nueva en el proveedor `apt-{id}-r{n}` (completar una sala Twilio es irreversible) y la ventana efectiva abre `reopened_at − RoomOpenBeforeMinutes` y cierra `max(ScheduledEnd, reopened_at + duración) + RoomCloseAfterMinutes`; el barrido de citas vencidas usa esa misma ventana. Un encuentro clínico completado sigue inmutable.

### Identidad (backend)

El participante se resuelve desde el usuario del JWT, nunca de un id del cliente. Nuevos internal endpoints (X-Internal-Key):

```text
GET /api/v1/internal/telemedicine/professionals/by-user/{userId}   # user → profesional (erp.professionals) o 404
GET /api/v1/internal/telemedicine/patients/by-user/{userId}        # user → paciente (app.patient_profiles) o 404
```

`IEmployeeRepository.GetByUserIdAsync` ya existía; se agregó `IPatientRepository.GetByUserIdAsync` (filtra soft-deleted).

### Datos

- Migración `AddWebhookEventsTable`: `tele.telemedicine_webhook_events` (`event_type`, `room_sid`, `participant_sid` no nulo, `payload_json`, `processed_at`) + índice único `(event_type, room_sid, participant_sid)`.
- Permiso `Telemedicine.SessionsManage` (Auth, seeder idempotente): Admin/OrgAdmin/ClinicAdmin/ClinicalDirector. Los profesionales de línea NO lo tienen: acceden a su sala por identidad (least privilege).

### Decisiones de Fase 4

- **Un solo permiso de sesión** (`SessionsManage`) en lugar de JoinSession+ManageSession del plan: la identidad ya autoriza al profesional/paciente; un permiso de join adicional sería código muerto.
- **Historial de migraciones por microservicio**: Auth pasa a `auth.__ef_migrations_history` (aislado de `public.__EFMigrationsHistory` del backend; patrón ya usado por telemedicine con `tele.__ef_migrations_history`). EF no namespacia las IDs por contexto: compartir `public` era fuente de errores (p. ej. `migrations remove` del contexto equivocado).
- **Horarios en UTC**: Npgsql exige `DateTimeOffset` con offset 0 para `timestamptz`; el horario del cliente (con su offset) se normaliza a UTC en la frontera de aplicación (`SchedulingRules.ResolveSlot`, `PreferredStart`, agenda). La UI convierte a local para mostrar. (Bug latente de Fase 3 corregido en Fase 4.)
- **Traducción de conflictos EF**: `SaveChangesAsync` envuelve la `PostgresException` en `DbUpdateException`; los repositorios (citas y salas) traducen 23505/23P01 y `DbUpdateConcurrencyException` a 409 vía el helper `DbUpdateExceptionExtensions`. (Latente de Fase 3 corregido.)
- **Transacciones manuales**: con `EnableRetryOnFailure` (NpgsqlRetryingExecutionStrategy) deben ejecutarse vía `CreateExecutionStrategy()` (`TelemedicineUnitOfWork`), patrón del proyecto.

## Fase 6 — Bandeja de alertas (materialización de eventos + API)

Las alertas son la **materialización de eventos de dominio** en `tele.telemedicine_alerts`
(tabla + `AlertType`/`AlertSeverity`/`AlertRecipientType` listos desde Fase 1). El canal de
entrega (email/push/SMS) es responsabilidad futura y **desacoplada** de este agregado.

### Eventos materializados (quién recibe)

| Evento de dominio                                                         | Alerta                   | Destinatario                        |
| ------------------------------------------------------------------------- | ------------------------ | ----------------------------------- |
| Nueva solicitud (`CreateTelemedicineRequest`)                             | `NewRequest`             | Profesional elegido por el paciente |
| Solicitud aprobada por un admin (`ReviewTelemedicineRequest`)             | `RequestApproved`        | Profesional asignado                |
| Solicitud rechazada (`ReviewTelemedicineRequest`)                         | `RequestRejected`        | Profesional asignado (si resoluble) |
| Cita creada/confirmada (`Schedule`/`Confirm`)                             | `NewAppointment`         | Profesional asignado                |
| Reprogramación (`Reschedule`)                                             | `AppointmentRescheduled` | Profesional asignado                |
| Cancelación (`Cancel`)                                                    | `AppointmentCancelled`   | Profesional asignado                |
| `participant-connected` (webhook) — el participante es el **paciente**    | `PatientWaiting`         | Profesional                         |
| `participant-connected` (webhook) — el participante es el **profesional** | `PatientJoined`          | Profesional                         |
| `participant-disconnected` (webhook) — el participante es el **paciente** | `ParticipantLeft`        | Profesional                         |
| `room-ended` (webhook) con sesión                                         | `SessionEnded`           | Profesional                         |

El destinatario se resuelve por el **usuario del JWT** (`ProfessionalRefDto.UserId`), nunca
por un id del cliente. Si el destinatario no es resoluble (p. ej. profesional sin usuario del
ERP), la alerta se omite (`AlertMaterializer` devuelve `null`). Las alertas al **paciente**
quedan para una fase futura: requieren `PatientRefDto.UserId` y la app móvil.

### API

```text
GET  /api/v1/telemedicine/alerts                    # Bandeja paginada (no leídas primero) ?unreadOnly&page&pageSize
GET  /api/v1/telemedicine/alerts/summary            # Recuento de no leídas (badge)
POST /api/v1/telemedicine/alerts/{id}/read          # Marcar una como leída (204) / 404 si no existe para el usuario
POST /api/v1/telemedicine/alerts/read-all           # Marcar todas como leídas → nº marcadas
```

**Autorización (en el handler, como las sesiones)**: el profesional (sin
`Telemedicine.AlertsView`) ve **solo sus propias alertas** (resuelto por identidad del JWT);
un usuario con `Telemedicine.AlertsView` (roles admin) ve la **bandeja global** y puede marcar
cualquier alerta. Los endpoints NO llevan `[RequirePermission]`.

### Reglas de negocio

- **Orden estable de la bandeja**: no leídas primero, luego por fecha descendente (cubre el
  índice `ix_telemedicine_alerts_recipient_user_id_read_at`).
- **`MarkReadAsync` solo marca si la alerta pertenece al destinatario** (update dirigido con
  `ExecuteUpdateAsync`; un usuario no puede marcar alertas ajenas).
- **Best-effort en el webhook**: un fallo al materializar la alerta de sesión NO tumba el
  procesamiento del webhook (la idempotencia/atomicidad ya las garantiza la clave de evento).
- **Los profesionales ven su bandeja por identidad; el permiso `AlertsView` es solo la vista
  administrativa global** (filtrar por clínica/organización es evolución futura, Fase 7/11).

### Decisiones (Fase 6)

- **Un solo permiso de alertas** (`AlertsView`) en lugar de View+Manage: la identidad ya
  autoriza la bandeja propia del profesional; `AlertsView` añade solo la vista global admin.
  Se siembra en roles admin (`AllTelemedicinePermissions`) y en los perfiles clínicos
  (`ProfessionalTelemedicinePermissions`) — el claim habilita la vista global, no es requisito
  para la bandeja propia.
- **`AlertMaterializer` como fábrica pura** (Application, sin infraestructura): el handler
  resuelve destinatario y nombres; la fábrica construye la alerta u omite si no hay destinatario.
- **Sin migración**: `telemedicine_alerts` y sus índices existen desde Fase 1.
- **Pendiente transversal**: `UpcomingAppointment` (cita próxima) necesita un scheduler/job;
  queda fuera de esta fase junto con el canal de entrega externo.

## Fase 5 — Encuentro clínico (espacio clínico durante la consulta)

El registro clínico de la cita. **Separación de responsabilidades**: la información
operativa vive en la cita/sesión/sala; la información clínica vive SOLO en el
encuentro (`tele.clinical_encounters`, creada en Fase 1). La cita tiene a lo sumo
un encuentro (índice único en `appointment_id`).

### API

```text
GET  /api/v1/appointments/{id}/encounter        # registro clínico (404 si no existe aún)
PUT  /api/v1/appointments/{id}/encounter        # guarda borrador (crea si no existe; crea/actualiza)
POST /api/v1/appointments/{id}/encounter/complete  # finaliza Draft→Completed (acepta datos finales)
```

La autorización se resuelve en el handler a partir del JWT (igual que las sesiones):
solo el **profesional de la cita** (identidad) o un **supervisor** con
`Telemedicine.SessionsManage`. **El paciente NO accede** a datos clínicos (PHI).
Por eso los endpoints no llevan `[RequirePermission]`.

### Reglas de negocio

- **Creación perezosa e idempotente**: el primer `PUT` crea el encuentro `Draft`;
  los siguientes lo actualizan. La carrera entre dos guardados concurrentes se
  resuelve con el índice único (el perdedor devuelve el existente y aplica sus
  cambios sobre él — `EncounterRepository.AddAsync`).
- **Documentar solo durante la consulta**: la cita debe estar `InProgress` o
  `Completed` (se puede completar la nota después de finalizar). Antes de iniciar
  o con la cita cancelada/no-show → 409.
- **Máquina de estados** `Draft → Completed` (final): `PUT` sobre un encuentro
  `Completed` → 409 (registro clínico inmutable). `POST complete` es idempotente
  (sobre un `Completed` devuelve el existente, 200).
- **Completar exige contenido mínimo**: al menos una nota o un campo clínico
  estructurado → 409 si está vacío.
- **Integridad al cancelar la cita**: un borrador (`Draft`) se cancela; un
  registro `Completed` se preserva (la consulta ocurrió).
- **Datos extensibles**: `clinical_data` jsonb con núcleo TIPADO
  (`ClinicalDataDto`: MotivoConsulta, Evaluacion, Diagnostico, Plan, Indicaciones,
  Observaciones, Seguimiento) serializado en camelCase. El ERP puede ampliar el
  esquema agregando campos al record sin romper (el jsonb es tolerante a campos
  desconocidos). `notes` = nota libre (varchar 4000).
- **Vínculo a sesión**: si hay una sesión activa al guardar, se asigna `session_id`.

### Decisiones (Fase 5)

- **Acceso de supervisión vía `SessionsManage`** (no un permiso dedicado
  `Telemedicine.ClinicalRecords*`): consistente con Fase 4 y mínimo privilegio
  (solo el profesional + supervisores clínicos). Si el dominio exige separar el
  acceso clínico del operativo, se agrega un permiso dedicado en una fase futura
  sin romper (la identidad sigue siendo la vía principal).
- **Encuentro accesible solo al profesional/supervisor** — el paciente (que sí
  participa de la sala) NO ve el registro clínico.
- **Sin `xmin` en el encuentro** (a diferencia de la cita): escritor único (el
  profesional), riesgo bajo. El `UpdatedAt` da trazabilidad de cambios.
- **Sin auditoría de cambios clínicos en `tele.`**: el sistema de auditoría por
  triggers es del backend (schema `audit`); pendiente para hardening (Fase 11-12).

- `Twilio` (gitignoreado): `AccountSid`, `ApiKeySid`, `ApiKeySecret` (key **región US1**), `AuthToken` (para firma de webhooks), `ValidateWebhookSignature` (`false` solo dev).
- Gotcha SDK: `TwilioClient.Init(apiKeySid, apiKeySecret, accountSid)` — el orden es (username=ApiKeySid, password=ApiKeySecret, accountSid), NO (accountSid, apiKey, secret).
- `Telemedicine:Provider` = `twilio` (default).
- `Backend` (clave compartida con el backend del ERP): `BaseUrl` (`http://localhost:5122`), `InternalApiKey` (gitignoreado; mismo valor que `Telemedicine:InternalApiKey` del backend), `TimeoutSeconds`. El backend valida el header `X-Internal-Key` con `RequireInternalKeyAttribute`.

## Fase 7 — Datos de referencia / maestros para la UI

La Fase 7 cierra la brecha de **listados/maestros para la UI** (los internal
endpoints por-Id ya existían desde Fase 3):

### Backend (`CoppAddresd.Api`)

- **`GET /api/v1/professionals-catalog`** — catálogo de profesionales clínicos
  (empleados con extensión clínica) con sus especialidades y sedes, paginado y
  filtrable (búsqueda, estado, especialidad, sede, organización, clínica).
  Accesible para **cualquier usuario autenticado** (como `/specialties` y
  `/professional-types`): la UI lo usa para elegir profesional al crear/confirmar
  solicitudes y para el directorio admin. Sin PHI: solo identidad, profesión,
  especialidades, sedes y estado.
  - Query: `ListProfessionalsCatalogQuery` (Application/Features/Professionals).
  - Repositorio: `IEmployeeRepository.ListProfessionalsAsync` (filtros + includes
    de especialidades/sedes, orden estable por nombre).
  - Endpoint en `ProfessionalCatalogsController`.

### Microservicio (`CoppAddresd.Telemedicine`)

- **`GET /api/v1/telemedicine/me`** — contexto del usuario autenticado:
  profesional y/o paciente resueltos **por el JWT** (`GetCurrentUserContextQuery`).
  Es el "me" que el frontend usa para saber si el usuario es profesional (y su
  `professionalId`) sin adivinar ids. Accesible a cualquier usuario autenticado.
- **Analytics del dashboard** (`GetDashboardAnalyticsQuery`): payload completo
  para las gráficas del dashboard — KPIs, serie temporal diaria, distribución
  por estado y por hora, actividad por profesional, próximas citas, conteo por
  estado USA del paciente (`States`, vía `StateCode` de la referencia ERP con
  dedup + caché) y el bloque aditivo **`Calls`** de F5 (salas, sesiones,
  `AverageDurationSeconds` = suma ÷ terminadas, reaperturas, chat por rol y
  claves P2 en 0; rollup-first con fallback a conteos vivos). Sin
  migración: solo agrupaciones de lectura (proyección ligera + agrupación en
  memoria, porque Npgsql no traduce `DateTimeOffset.Date`/enums-string en
  GroupBy; rango acotado del dashboard). Consultas **secuenciales**: EF Core no
  permite operaciones concurrentes sobre el mismo DbContext scoped.
  - `GET /api/v1/telemedicine/admin/analytics?from&to` — vista **global**
    (`Telemedicine.AdminView`): incluye actividad por profesional y
    profesionales activos. `from`/`to` opcionales (default: últimos 30 días).
  - `GET /api/v1/telemedicine/me/analytics?from&to` — solo las citas del
    profesional resuelto **por el JWT** (identidad, nunca un id del cliente):
    sin actividad de otros profesionales; 403 si el usuario no es profesional.
  - La serie temporal se entrega **por día completa** (días sin citas en 0) y el
    frontend agrupa por semana/mes client-side.
- **Listados admin** (requieren el permiso `Telemedicine.AdminView`, solo roles
  administrativos; los profesionales usan su agenda por identidad):
  - `GET /api/v1/telemedicine/admin/summary` — KPIs (citas hoy, pendientes,
    completadas, solicitudes pendientes, sesiones activas, alertas no leídas).
  - `GET /api/v1/telemedicine/admin/appointments` — citas paginadas con filtros
    (profesional, paciente, clínica, sede, estado, rango).
  - `GET /api/v1/telemedicine/admin/requests` — solicitudes paginadas con filtros
    (estado, profesional, paciente, rango). Requiere `Telemedicine.AdminView`:
    los profesionales usan su bandeja por identidad en `/me/requests` (abajo).
  - `GET /api/v1/telemedicine/admin/sessions` — sesiones de video con cita,
    paciente y profesional resueltos.
- **"Mis datos" del profesional** (`/me/*`, alcance **por identidad del JWT** —
  nunca por un id enviado por el cliente; 403 si el usuario no tiene perfil
  clínico):
  - `GET /api/v1/telemedicine/me/appointments` — citas del profesional con los
    mismos filtros y shape que el listado admin (`ListMyAppointmentsQuery`).
  - `GET /api/v1/telemedicine/me/requests` — bandeja de solicitudes del
    profesional (las que los pacientes enviaron a su agenda), con filtros
    (estado, paciente, rango) y el mismo shape que el listado admin
    (`ListMyRequestsQuery`): reutiliza `ListAdminAsync` con el id del JWT.
  - `GET /api/v1/telemedicine/me/summary` — KPIs acotados al profesional
    (`GetMySummaryQuery`), mismo shape que el resumen admin.
- **Permiso `Telemedicine.AdminView`** nuevo, sembrado en Auth
  (`PermissionCodes.cs` + `RoleSeeder.AllTelemedicinePermissions`) y declarado en
  el microservicio (`TelemedicinePermissionCodes`). Asignado a Admin,
  OrganizationAdmin y ClinicAdmin.

### Decisiones (Fase 7)

- Los **maestros viven en el backend** (dueño de los datos) y la UI los consume
  por `apiUrl`; el microservicio solo expone lo propio de telemedicina (`me` y
  admin). Exponer los catálogos también vía el microservicio sería duplicación.
- **Un solo permiso admin** (`AdminView`) para todos los listados globales
  (mismo criterio que `AlertsView` en Fase 6): identidad cubre el resto.
- El catálogo de profesionales es **público-autenticado** (sin PHI) para que el
  paciente (app móvil futura) pueda elegir profesional al crear una solicitud.
- **Sin migración**: no se agregó ninguna tabla; todo fueron lecturas y un permiso.

## Fase 9-10 — Frontend (`coppaddresd-front`)

Módulo **`features/telemedicine/`** + rutas bajo **`/telemedicine`**:

```
features/telemedicine/
├── types/index.ts            # Espejo de DTOs y enums del microservicio
├── services/
│   ├── telemedicine-service.ts   # Cliente del microservicio (5130, /api/v1/telemedicine/*)
│   └── reference-service.ts      # Catálogos del backend (5122: professionals-catalog, specialties, tree, patients)
├── hooks/                    # use-current-user, use-agenda, use-alerts, use-admin (listados + summary),
│                             #   use-dashboard-analytics (analytics admin/me)
├── utils/format.ts           # Etiquetas de estados en español + colores + formato de fechas
└── components/               # ProfessionalDashboard, ProfessionalAgenda, ProfessionalCalendar,
                              #   ProfessionalRequests, AlertsPage, AppointmentDetail (sala + encuentro),
                              #   AdminDashboard, AdminAppointments, AdminRequests, AdminSessions,
                              #   AdminProfessionals, PermissionGate,
                              #   dashboard/ (DashboardChartCard, AppointmentsTrendChart,
                              #     StatusDistributionChart, ProfessionalActivityChart,
                              #     HourlyDistributionChart, UpcomingAppointments, QuickActions)
```

Rutas:

| Ruta                                | Vista                                                                           | Acceso                                    |
| ----------------------------------- | ------------------------------------------------------------------------------- | ----------------------------------------- |
| `/telemedicine`                     | Dashboard: admin global (KPIs, gráficas, próximas) o profesional (sus métricas) | Cualquier autenticado                     |
| `/telemedicine/agenda`              | Mi agenda (día/semana/mes + cancelar/reprogramar)                               | Cualquier autenticado (si es profesional) |
| `/telemedicine/calendario`          | Calendario mensual con citas                                                    | Cualquier autenticado (si es profesional) |
| `/telemedicine/solicitudes`         | Bandeja del profesional (confirmar solicitudes)                                 | Cualquier autenticado (si es profesional) |
| `/telemedicine/alertas`             | Bandeja de alertas (mark-read / read-all)                                       | Cualquier autenticado                     |
| `/telemedicine/citas/[id]`          | Detalle: sala virtual (join-token, start/end) + encuentro clínico               | Profesional de la cita o supervisor       |
| `/telemedicine/admin`               | Dashboard admin (KPIs, gráficas, próximas citas)                                | `Telemedicine.AdminView`                  |
| `/telemedicine/admin/citas`         | Todas las citas                                                                 | `Telemedicine.AdminView`                  |
| `/telemedicine/admin/solicitudes`   | Todas las solicitudes                                                           | `Telemedicine.AdminView`                  |
| `/telemedicine/admin/profesionales` | Catálogo de profesionales                                                       | `Telemedicine.AdminView`                  |
| `/telemedicine/admin/sesiones`      | Sesiones de video                                                               | `Telemedicine.AdminView`                  |

### Notas de la UI

- La URL del microservicio se configura con `NEXT_PUBLIC_TELEMEDICINE_API_URL`
  (default `http://localhost:5130`) en `lib/config/env.ts` (ver `.env.example`).
- `PermissionGate` oculta la navegación sin el permiso, pero **la autorización
  real siempre la aplica el microservicio** (mínimo privilegio en la UI).
- La sala virtual genera el **join-token** y muestra el estado de la sala y los
  participantes; la integración con el SDK de video del navegador (Twilio) queda
  para una fase posterior (el endpoint ya devuelve el token de acceso).
- El encuentro clínico usa los campos `ClinicalDataDto` (jsonb tipado) y respeta
  la inmutabilidad del registro `Completed`.

## Fase 11-12 — Testing formal y hardening

### Tests

```bash
dotnet test tests/CoppAddresd.Telemedicine.UnitTests        # 134 unit (sin BD)
$env:COP_TEST_DB_CONNECTION="Host=localhost;..."; dotnet test tests/CoppAddresd.Telemedicine.IntegrationTests  # 25 int
```

- **Unit** (`CoppAddresd.Telemedicine.UnitTests`, fakes en memoria estilo proyecto):
  reglas de agendamiento (anticipación/ventana/duración/UTC), ventana de sala y
  autorización por identidad, máquina de estados del encuentro, materializador
  de alertas, guard de referencias, mappers (sin N+1), validadores y todos los
  handlers (incluido el webhook: firma, duplicado, room-ended).
- **Integración** (`CoppAddresd.Telemedicine.IntegrationTests`): BD aislada
  `coppaddresd_tele_test_<guid>` creada con `TEMPLATE template0` (el template1 del
  contenedor tiene mismatch de collation), migrada y eliminada por corrida. Cubre
  la garantía real de anti doble reserva (dos INSERTs concurrentes solapados →
  uno falla con exclusión GiST), idempotencia del webhook con transacción real
  (duplicado → rollback sin doble mutación), persistencia del agregado
  (historial append-only, agenda, listados admin), idempotencia de sala/encuentro
  (índices únicos) y fallback clínica → organización → defaults.
- Convención: los tests de una corrida comparten la BD (colección xUnit), por eso
  cada seed usa profesionales/ids únicos y los conteos se filtran por clave propia.

### Datos de prueba (seed)

`coppAddresdBack/scripts/seed_telemedicine.py` genera un volumen realista y bien
relacionado para probar el dashboard y las vistas en el frontend: solicitudes en
estados variados, ~900 citas distribuidas en -60..+14 días (estados coherentes
con la fecha: pasadas → Completed/NoShow/Cancelled, hoy → mixto, futuras →
Confirmed), salas + sesiones + encuentros clínicos para completadas recientes e
InProgress de hoy, y alertas por profesional. Usa los profesionales reales de
`erp.professionals` (con su `user_id`) y pacientes reales de `app.patient_profiles`.

```bash
# Desde ai-service (entorno uv con psycopg):
uv run --with psycopg[binary] python ../coppAddresdBack/scripts/seed_telemedicine.py
```

Idempotente: borra y recrea solo lo marcado con su `created_by` (SEED_USER_ID),
nunca toca datos de otros orígenes. Respeta las reglas de la BD: anti doble
reserva (no solapa citas ACTIVAS del mismo profesional ni repite inicios
exactos), una solicitud Converted → una sola cita (`request_id` único).

### Auditoría clínica (PHI)

- Migración `AttachClinicalEncounterAudit` (4ª de `tele.`): adjunta
  `audit.audit_trigger_function` a `tele.clinical_encounters` de forma
  **condicional** (solo si el schema `audit` del backend existe en la instancia)
  e idempotente. `Down` elimina el trigger.
- `HttpAuditActorContext` (scoped) resuelve el actor desde el JWT del microservicio
  (NameIdentifier/Email/Role + correlation id del middleware); `AuditTriggerInterceptor`
  (patrón del backend) propaga los GUC `audit.*` al iniciar cada transacción.
- El guardado del encuentro (`EncounterRepository.Add/Update`) usa transacción
  explícita corta: un `SaveChanges` de una sola sentencia no abre transacción y el
  interceptor nunca dispararía (actor quedaría `SYSTEM`).
- Verificado E2E: `PUT encounter` de un usuario real → fila en
  `audit.activity_logs` con `actor_type=USER`, `user_id` del JWT y correlation id.

### Correcciones de bugs (descubiertas en el E2E de hardening)

1. **`TwilioVideoProvider.CreateRoomAsync`**: el SDK lanza `ApiException` "Room
   exists" (HTTP 409/código 20429) cuando el nombre determinista `apt-{id}` ya
   está tomado (sala huérfana de un intento previo) en lugar de devolverla. Ahora
   se recupera la sala existente (`GetRoomAsync` por nombre) → idempotencia real
   del proveedor.
2. **Sesiones nuevas en el agregado**: el fixup de EF (RoomId → sala cargada con
   `ThenInclude Sessions`) marca una sesión agregada a `appointment.Sessions` como
   `Modified` (clave Guid no generada por BD → asume existente) → `UPDATE` de 0
   filas → `DbUpdateConcurrencyException` → 409 al iniciar sesión cuando la sala ya
   existía. Fix: `AppointmentRepository.UpdateAsync` re-trackea con
   `dbContext.Sessions.Add(session)` las sesiones no cargadas de BD
   (`_loadedSessionIds`, poblado en `GetForUpdateAsync`); las cargadas conservan su
   transición Active → Ended. `Entry.State = Added` no sirve (el fixup lo re-marca;
   `DbSet.Add` sobrevive a DetectChanges — verificado con test de integración).

### Decisiones

- Los tests de integración **no** usan WebApplicationFactory: el valor está en la
  capa de datos/concurrencia/idempotencia contra PostgreSQL real; la autorización
  se cubre a nivel de handler (identidad/supervisión).
- Auditoría SOLO del encuentro clínico en esta fase (PHI); extender a cita/sesión
  si el dominio lo exige (mismo mecanismo).
- El actor del BACKEND sigue sin propagarse (pendiente transversal del proyecto).

## F2 — Recordatorios y notificaciones (schedule + entrega desacoplada)

F2 separa responsabilidades: **Telemedicina es dueña de la agenda y de los
eventos** (decide a quién, qué y cuándo notificar); **el backend es dueño de la
entrega** (push/SMS). Contrato interno (mismo `Backend:BaseUrl` +
`X-Internal-Key` que los datos de referencia):

```text
POST {Backend:BaseUrl}/api/v1/internal/telemedicine/notifications
{ "userId", "title", "body", "channels": ["Push","Sms"],
  "data": { "appointmentId", "screen" }, "dedupeKey" }
→ 200 con estado por canal
```

El emisor es `ITelemedicineNotifier` (HttpClient resiliente, `TelemedicineNotifier`,
misma config `Backend` que `AppointmentReferenceDataService`). Es **best-effort**:
un fallo/timeout se registra y nunca rompe el flujo de negocio (patrón de las
alertas del webhook).

### Recordatorios (scheduler)

`AppointmentReminderSweepHostedService` (cada **10 min**, delay inicial **1 min**,
patrón `StaleSessionSweepHostedService`) ejecuta `AppointmentReminderSweeper`
sobre las citas `Confirmed` de las próximas 24 h:

| Banda (settings)                                              | Destinatario | Canales                                              | Kind                     |
| ------------------------------------------------------------- | ------------ | ---------------------------------------------------- | ------------------------ |
| `(ReminderSecondHoursBefore, ReminderFirstHoursBefore]` (24 h) | Paciente     | Push                                                 | `Reminder24h`            |
| `(0, ReminderSecondHoursBefore]` (1 h)                        | Paciente     | Push + SMS (`≤ SmsReminderHoursBefore`)              | `Reminder1h`             |
| `(0, ReminderSecondHoursBefore]` (1 h)                        | Profesional  | Push                                                 | `ProfessionalReminder1h` |

- Solo se envía mientras la cita siga `Confirmed` (revalidación inmediata al
  envío): `Cancelled/Completed/NoShow` se omiten y `InProgress` no recibe
  recordatorios. Una cita que ya entró a la banda corta no recibe además el
  aviso de 24 h.
- **Dedupe en `tele.notification_dispatch`** (`appointment_id`, `kind`,
  `sent_at`; único `(appointment_id, kind)`): si el backend acepta el envío se
  registra la fila; si lo rechaza NO se registra y el tick siguiente reintenta.
  La carrera entre réplicas la resuelve el índice único (el perdedor no reenvía).
- Sin usuario de Auth del destinatario (`PatientRefDto.UserId` /
  `ProfessionalRefDto.UserId`) se omite el envío (log Debug). La referencia de
  paciente expone `userId` cuando el backend lo provee (trabajo paralelo del
  dueño de delivery).

### Hooks de eventos (best-effort)

| Evento                                        | Destinatario                                                                  | Mensaje                                |
| --------------------------------------------- | ----------------------------------------------------------------------------- | -------------------------------------- |
| `CreateTelemedicineRequestCommand`            | Profesional elegido                                                           | "Nueva solicitud de {paciente}" (push) |
| `CancelAppointmentCommand`                    | Profesional cancela → paciente; paciente cancela → profesional; admin/sistema → paciente | "Cita cancelada" + motivo (push) |
| `StaleSessionSweeper` (cierre por `NoShow`)   | Paciente                                                                      | "No asististe a tu cita…" (push + SMS) |

### Settings (`tele.telemedicine_settings`)

| Columna                        | Default | Efecto                                                        |
| ------------------------------ | ------- | ------------------------------------------------------------- |
| `NotificationsEnabled`         | `true`  | Interruptor maestro de notificaciones (barridos y hooks)      |
| `ReminderFirstHoursBefore`     | `24`    | Anticipación del primer recordatorio (paciente, push); 0 = off |
| `ReminderSecondHoursBefore`    | `1`     | Anticipación del segundo recordatorio (paciente push+SMS y profesional push); 0 = off |
| `SmsReminderHoursBefore`       | `1`     | Ventana en la que el recordatorio del paciente incluye SMS     |

### Datos

Migración `AddTelemedicineNotifications`: columnas de settings (defaults BD
`true`/`24`/`1`/`1` para que las filas existentes queden con recordatorios
activos) + tabla `tele.notification_dispatch` con índice único
`(appointment_id, kind)`.

### Tests (F2)

18 tests nuevos en `CoppAddresd.Telemedicine.UnitTests` (notificador fake +
contrato HTTP del emisor): recordatorios por ventana/canal con dedupe,
revalidación de estado, settings deshabilitados, reintento con backend caído,
shape del contrato de entrega, hooks de solicitud/cancelación/no-show y elección
de destinatario según quién cancela. Total de la suite: **224**.

## F3 — Experiencia de llamada (backend: capacidad + chat)

Alcance backend de F3 (P2). Los bloques de reconexión/preflight/pantalla
compartida y las UIs de chat son de los clientes (ERP/app) y no cambian el
contrato del microservicio.

### Capacidad de participantes

- **Default 3** (`tele.telemedicine_settings.max_participants`): profesional +
  paciente + 1 supervisor. Rango validado **2–10**
  (`SessionSupport.EnsureValidMaxParticipants`, fuera de rango → 409 sin
  tocar la sala). La autorización de participante no cambia (cita 1:1).
- **Salas nuevas** (primer join-token, `session/start`, reapertura): Twilio
  recibe `max_participants = settings.MaxParticipants` y `virtual_rooms` lo
  persiste. La **reapertura** crea la sala nueva con el settings vigente y
  actualiza `virtual_rooms.max_participants` (antes quedaba con el valor viejo).
- **Salas creadas antes de F3** (límite 2): en el siguiente `join-token` o
  `session/start`, si `room.MaxParticipants < settings.MaxParticipants`, se
  actualiza la sala `in-progress` en Twilio
  (`IVideoProvider.UpdateRoomMaxParticipantsAsync` → `POST /v1/Rooms/{Sid}` con
  el cliente REST del SDK, porque Twilio 7.14.9 no expone `MaxParticipants` en
  `UpdateRoomOptions`) y se persiste el valor local. Es **best-effort**: si
  Twilio falla, el join continúa y queda un log Warning.
- **Sala llena (53105)**: el proveedor traduce el código a
  `BusinessRuleViolationException` («La sala alcanzó el máximo de
  participantes», 409) en lugar de un error crudo; los clientes mapean el
  error homónimo del SDK JS a un copy accionable.
- La migración `AddRoomChatAndCapacity` fija el default de BD en 3 y hace
  **backfill** de las filas existentes que valen 2 (respeta valores manuales).

### Chat de la consulta

```text
GET  /api/v1/appointments/{id}/chat/messages?after={ISO}&afterId={uuid}&limit={1..100}
     → 200 ChatMessageDto[] ordenado por (created_at, id); default limit 50
POST /api/v1/appointments/{id}/chat/messages   { "body": "…" }
     → 201 ChatMessageDto (persistido en tele.chat_messages)
```

- **Autorización**: misma que la sala (`SessionSupport.RequireParticipantAsync`):
  profesional/paciente de la cita o supervisor con `Telemedicine.SessionsManage`.
  Usuario ajeno → 403; cita inexistente → 404. `sender_user_id` y `sender_role`
  se derivan del JWT, nunca del cuerpo.
- **Estado**: disponible con la cita **Confirmed, InProgress o Completed** (los
  participantes pueden esperar en la sala con la cita aún confirmada y se
  conserva el acceso posterior a la consulta); `Requested` y terminales sin
  atención (`Cancelled`/`NoShow`) → 409. El historial persiste en BD; no hay
  purga en v1.
- **Validación**: `body` con trim, 1–2000 caracteres (400 con errores);
  `limit` 1–100; `after` ISO inválido lo rechaza el model binding (400).
- **Polling**: el cliente (ERP/app) usa REST + polling incremental cada ~4 s con
  el cursor `(after, afterId)`; no hay SignalR/SSE ni push por mensaje en F3.
- **Sin edición, borrado, adjuntos ni recibos**; el DTO no incluye `senderName`
  (los clientes resuelven el nombre con la cita/participantes). Logging sin PHI
  (solo ids, rol y longitud).
- Migración `AddRoomChatAndCapacity`: tabla `tele.chat_messages` + índice
  `ix_chat_messages_appointment_id_created_at` (misma migración que la
  capacidad; **no** se aplica automáticamente al iniciar el microservicio).

### Tests (F3)

49 tests nuevos en `CoppAddresd.Telemedicine.UnitTests`: rango del default de
capacidad y `NewRoom`, elevación perezosa en join/start/reapertura (el fake del
proveedor registra las actualizaciones), fallo del proveedor best-effort,
matriz de autorización del chat (profesional/paciente/supervisor y ajeno),
guarda de estado, validación de body/límite, orden y cursor keyset, traducción
EF del cursor. Total de la suite backend: **273**.

## F4 — Clínica y cumplimiento (backend: pre-consulta, addenda y auditoría)

Alcance backend de F4. La **grabación de la consulta y el consentimiento
versionado** (F4.1–F4.4 del diseño) **no** se implementan en esta fase: son un
sub-proyecto separado (diseño en `openspec/changes/app-f4-clinical-compliance`).

### Pre-consulta del paciente

```text
GET  /api/v1/appointments/{id}/pre-visit-intake
     → 200 PreVisitIntakeDto | 200 sin cuerpo si aún no existe (estado vacío)
PUT  /api/v1/appointments/{id}/pre-visit-intake
     { "reason": "…", "symptoms": "…", "allergies": "…", "medications": "…" }
     → 200 PreVisitIntakeDto (upsert de autoguardado)
```

- Tabla `tele.pre_visit_intakes` **1:1 con la cita** (índice único
  `appointment_id`, FK cascade): `reason` obligatorio (500), `symptoms` (4000),
  `allergies` (2000) y `medications` (2000) opcionales. `patient_id` y
  `created_by` se derivan de la cita y del JWT, **nunca** del cuerpo.
- **Autorización participante** (misma que sala/chat): GET para el paciente,
  el profesional de la cita o un supervisor; PUT **solo el paciente** de la
  cita (profesional/supervisor → 403; usuario ajeno → 403; cita inexistente →
  404).
- **Ciclo de vida**: editable mientras la cita está `Confirmed` (autoguardado
  upsert, sin estado borrador/enviado; `updated_at` en cada guardado). Con la
  cita `InProgress` o en estados terminales la escritura responde 409 y la
  **lectura sigue disponible** para los autorizados.
- **Validación** FluentValidation (trim; `reason` 1–500; textos opcionales con
  sus máximos) → 400 con errores. Logging **sin PHI** (ids y longitudes).
- **Auditoría**: `attach_table_audit` condicional sobre `pre_visit_intakes`
  (PHI) en la migración y transacción explícita corta en el repositorio para
  que el trigger reciba el actor del JWT.
- **Upsert concurrente**: el índice único resuelve la carrera; el perdedor
  recibe la fila existente TRACKEADA y aplica sus cambios (mismo patrón que el
  encuentro).

### Adendas del encuentro

```text
GET  /api/v1/appointments/{id}/encounter/addenda
     → 200 EncounterAddendumDto[] ordenado por (created_at, id); [] si no hay encuentro
POST /api/v1/appointments/{id}/encounter/addenda   { "body": "…" }
     → 201 EncounterAddendumDto (persistido en tele.encounter_addenda)
```

- Tabla `tele.encounter_addenda` **append-only**: encuentro, autor
  (`author_user_id` del JWT + `author_name` snapshot del claim de nombre/email,
  truncado a 200), texto 1–2000 y fecha. **Sin edición, borrado ni límite por
  encuentro**; el registro clínico original sigue inmutable (`PUT encounter`
  sobre `Completed` sigue 409).
- **Autorización** idéntica a la del encuentro (`RequireSessionOwnerAsync`):
  profesional asignado o supervisor; el paciente → 403; cita inexistente → 404.
- **Precondición**: el POST exige encuentro existente y `Completed`; en `Draft`
  o sin encuentro → 409 («la adenda es para registros finalizados»). GET
  devuelve `[]` si el encuentro no existe (la UI no necesita 404).
- **Auditoría**: `attach_table_audit` condicional sobre `encounter_addenda`
  (PHI) en la migración.

### Auditoría del ciclo de vida de la cita

- Migración `AttachAppointmentLifecycleAudit` (condicional e idempotente,
  mismo patrón de `AttachClinicalEncounterAudit`): adjunta
  `audit.audit_trigger_function` a `tele.appointments`,
  `tele.telemedicine_sessions` y `tele.virtual_rooms`. Cubre inicio/fin/
  reapertura/cancelación/reprogramación/no-show y la creación/cambio de estado
  de sala y sesión; la transición se lee de `changed_data` de
  `audit.activity_logs` (**sin doble escritura** de eventos de aplicación).
- **Actor**: `AuditTriggerInterceptor` propaga el actor del JWT, y
  `AppointmentRepository.Add/UpdateAsync` abre transacción explícita corta
  (con `CreateExecutionStrategy`) porque el interceptor solo dispara al iniciar
  la transacción — un `SaveChanges` de una sentencia dejaba el actor en
  `SYSTEM`. El barrido de sesiones estancadas y los webhooks no tienen usuario
  y quedan como `SYSTEM` (correcto y documentado).
- **No** se auditan por trigger: `chat_messages`, `notification_dispatch`,
  `telemedicine_webhook_events` ni los historiales de cancelación/
  reprogramación (clasificación en `docs/modules/activity-log/database.md`).
- **Sin API de lectura** de `audit.activity_logs` en F4 (decisión abierta;
  consulta por SQL en el backend).

### Migraciones (generadas, las aplica el padre)

- `AddPreVisitIntakeAndAddenda`: tablas `tele.pre_visit_intakes` y
  `tele.encounter_addenda` + triggers de auditoría PHI condicionales.
- `AttachAppointmentLifecycleAudit`: triggers del ciclo de vida de la cita.
- El microservicio **no** migra al iniciar (ver «Comandos»).

### Tests (F4)

34 tests nuevos en `CoppAddresd.Telemedicine.UnitTests`: matriz de autorización
del intake (paciente escribe; profesional/supervisor/ajeno 403), edición
permitida/bloqueada por estado, lectura post-inicio, upsert sin duplicar,
identidad derivada del JWT y validaciones de longitud; matriz de adendas
(profesional/supervisor OK; paciente/ajeno 403), precondición `Completed`
(Draft/sin encuentro → 409), orden `(created_at, id)`, snapshot/truncado del
autor y validación 1–2000. Total de la suite backend al cerrar F4: **310**
(F5 la lleva a 355 unit + 41 de integración — ver sección F5). La presencia y
condicionalidad de las migraciones de auditoría **no** se cubre por unit tests
(requiere PostgreSQL real y schema `audit`): queda para integración/QA manual.

## F5 — Operación y calidad (métricas de llamada + gracia configurable)

### Métricas de llamada (pipeline CQRS, sin migración de esquema)

Nuevas claves EAV en `tele.appointment_daily_metrics` (contrato compartido
processor/backfill/lector en `Application/Constants/TelemedicineMetricKeys.cs`):

| Clave | Dimensión | Incremento | Emisor |
|---|---|---|---|
| `rooms_opened` | `general` | +1 | primera apertura de sala en `join-token`/`session/start` (la reapertura NO la cuenta) |
| `sessions_started` | `general` | +1 | `session/start` |
| `sessions_ended` | `general` | +1 | fin manual, webhook `room-ended` o barrido |
| `session_duration_seconds` | `general` | +duración | ídem (base del promedio: suma ÷ `sessions_ended`; 0 → null) |
| `reopens` | `general` | +1 | transición `Completed → InProgress` derivada del `AppointmentStatusChangedMetricEvent` existente (sin evento nuevo) |
| `chat_messages_sent` | `Professional｜Patient｜Supervisor` | +1 | `SendRoomChatMessageCommand` con el rol del JWT |

- **Eventos**: `RoomOpenedMetricEvent`, `SessionStartedMetricEvent`,
  `SessionEndedMetricEvent` (con `DurationSeconds`), `ChatMessageSentMetricEvent`
  (rol derivado del JWT, nunca del cuerpo; sin PHI en logs). Se encolan al final
  de cada handler con el patrón `ITelemedicineMetricsQueue? metricsQueue = null`
  (param opcional al final del constructor primario).
- **Processor** (`TelemedicineMetricsProcessorHostedService`): casos nuevos en el
  `switch`; contadores con upsert doble (profesional + espejo global
  `Guid.Empty`) y `session_duration_seconds` sumando el incremento en lugar de
  +1. El mapeo evento→clave/dimensión es testeable (`MapCounters`); `reopens`
  se deriva dentro de `ProcessStatusChangedAsync` (`IsReopen`).
- **Deriva corregida (bug operativo)**: el barrido (`StaleSessionSweeper`) y el
  webhook (`ProcessTwilioWebhookCommand`) ahora emiten
  `AppointmentStatusChangedMetricEvent` (InProgress→NoShow/Completed y
  Confirmed→NoShow) y `SessionEndedMetricEvent`, de modo que `status_count` y
  las claves de llamada no se descuadran sin backfill. El webhook emite
  **después del commit** (un duplicado con rollback no cuenta) y el barrido una
  sola vez por cita cerrada.
- **Backfill** (`MetricsBackfillService`): reconstruye las claves nuevas desde
  `tele.virtual_rooms`, `tele.telemedicine_sessions`, `tele.chat_messages` y
  `tele.appointments.reopen_count`, con la **fecha de agenda** de la cita
  (misma semántica que los eventos). Las claves P2 `join_tokens_issued` y
  `participant_connections` no se emiten en v1 y no son reconstruibles (el
  lector las expone en 0/empty).
- **Lectura**: `IAppointmentRepository.GetCallMetricsAsync` (rollup-first con
  fallback a conteos vivos) alimenta el bloque `Calls` de
  `DashboardAnalyticsDto`. Los endpoints existentes
  (`GET /api/v1/telemedicine/admin/analytics` y `/me/analytics`) no cambian de
  ruta ni de permisos.

### Gracia de reapertura configurable

- `TelemedicineSettings.ReopenGraceMinutes` (default de dominio **60**;
  `AlertConfiguration` fija el default de BD 60) + migración
  **`AddReopenGraceMinutes`** (`tele.telemedicine_settings.reopen_grace_minutes`,
  `integer NOT NULL DEFAULT 60`; las filas existentes toman el default sin
  backfill).
- `SessionSupport.EnsureValidReopenGraceMinutes` valida 5–1440
  (`BusinessRuleViolationException` → 409). `ReopenSessionCommand` carga el
  settings **antes** del check y usa el valor efectivo (también en el mensaje de
  error).
- `AppointmentDto.ReopenGraceMinutes` (campo trailing opcional) se enriquece en
  `GetAppointmentQuery` (detalle ERP) y `GetMyAppointmentsQuery` (app móvil);
  las listas admin lo dejan null. El ERP calcula el botón «Reabrir consulta» con
  el valor del DTO (fail-closed si viene null).

### Datos y tests (F5)

- Migración `AddReopenGraceMinutes` (8.ª de `tele.`): columna
  `reopen_grace_minutes` con default 60. El microservicio **no** migra al
  iniciar (ver «Comandos»).
- Unit `CoppAddresd.Telemedicine.UnitTests`: **355** (45 nuevos: emisores con
  cola fake, mapeo de claves/dimensiones del processor, deriva del
  barrido/webhook, rango de gracia y comando con valor configurado, exposición
  del DTO y bloque `Calls`).
- Integración `CoppAddresd.Telemedicine.IntegrationTests`: **41** (3 nuevos:
  backfill de las claves de llamada con espejo global y lectura rollup-first /
  fallback vivo), corridos contra PostgreSQL real con `COP_TEST_DB_CONNECTION`.

## Comandos

```bash
dotnet build src/Services/CoppAddresd.Telemedicine/CoppAddresd.Telemedicine.csproj
dotnet run --project src/Services/CoppAddresd.Telemedicine        # http://localhost:5130
dotnet ef migrations add <Nombre> --project src/Services/CoppAddresd.Telemedicine --startup-project src/Services/CoppAddresd.Telemedicine --output-dir Infrastructure/Migrations
dotnet ef database update --project src/Services/CoppAddresd.Telemedicine --startup-project src/Services/CoppAddresd.Telemedicine
```

> Usar siempre `--output-dir Infrastructure/Migrations`: `MigrationsDirectory` del csproj no se honró en este setup.

## Pacientes demo (app móvil ANTARES)

El script `scripts/seed_patients_demo.py` crea 6 pacientes de prueba para el
flujo móvil de telemedicina: perfil en `app.patient_profiles` + cuenta en el
Auth Service con password conocida + acceso a la aplicación `app` + vínculo
`patient_profiles.user_id` (idempotente; la 2ª corrida reutiliza las cuentas).

```bash
# Desde ai-service (entorno uv con psycopg):
uv run --with psycopg[binary] python ../coppAddresdBack/scripts/seed_patients_demo.py
```

Requisitos: Postgres local (`docker compose up -d`) y el Auth Service corriendo
en `:5123` (el script lee la clave interna de `appsettings.json` del Auth o de
`AUTH_INTERNAL_KEY`).

### Credenciales (password común `Demo1234!`)

| Email                              | Documento  | Nombre           |
| ---------------------------------- | ---------- | ---------------- |
| `juan.perez@coppaddresd.com`       | 1000000001 | Juan Pérez       |
| `maria.gomez@coppaddresd.com`      | 1000000002 | María Gómez      |
| `carlos.rodriguez@coppaddresd.com` | 1000000003 | Carlos Rodríguez |
| `ana.martinez@coppaddresd.com`     | 1000000004 | Ana Martínez     |
| `luis.fernandez@coppaddresd.com`   | 1000000005 | Luis Fernández   |
| `laura.sanchez@coppaddresd.com`    | 1000000006 | Laura Sánchez    |

Login desde ANTARES: `email` + password con `application: "app"`, o el flujo
OTP por identificación (canal email — en Development el código llega en la
respuesta `devCode`). Los pacientes solo acceden por identidad (sin permisos
ERP): pueden crear solicitudes, listar/cancelar sus citas y entrar a la sala
de sus citas.

### Profesionales demo (ERP)

Los profesionales del seed (`scripts/seed_professionals_demo.py`) se crean sin
password. `scripts/seed_professional_credentials.py` fija la password
`Demo1234!` a todos (idempotente) vía el endpoint interno del Auth
(`POST /api/auth/internal/seed-demo-password`, solo Development).

```bash
# Desde ai-service:
uv run python ../coppAddresdBack/scripts/seed_professional_credentials.py
```

Login en el ERP (`coppaddresd-front` :3000) con `email` + `Demo1234!`:

| Email                           | Profesión (catálogo)   |
| ------------------------------- | ---------------------- |
| `ana.torres@coppaddresd.com`    | Registered Dietitian   |
| `carlos.ruiz@coppaddresd.com`   | Clinical Psychologist  |
| `lucia.mendez@coppaddresd.com`  | Physician (MD/DO)      |
| `pedro.salas@coppaddresd.com`   | Physical Therapist     |
| `rosa.pineda@coppaddresd.com`   | Health Coach           |
| `felipe.castro@coppaddresd.com` | Registered Nurse       |
| `jorge.vega@coppaddresd.com`    | (Finance — no clínico) |

Los profesionales tienen el rol `Professional` **scoped por clínica**: el
microservicio los autoriza por introspección al Auth cuando la petición lleva
el header `X-Clinic-Id` (lo envía el ERP). Pueden ver su bandeja de
solicitudes, confirmar/rechazar y operar agenda, salas y encuentros de sus
pacientes.
