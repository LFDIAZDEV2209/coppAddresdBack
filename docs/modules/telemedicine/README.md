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
- **Fase 8 (adelantada) — Permisos `Telemedicine.*`**: siembra en el Auth Service (`PermissionCodes` + `RoleSeeder`), autorización por claim `permission` en el microservicio (mismo mecanismo que el backend).

Pendiente: Fase 7 (datos de referencia vía backend — parcialmente hecho en Fase 3: faltan endpoints de listado/maestros para UI), Fase 9-10 (frontend), Fase 11-12 (testing/hardening). También: `room-ended` sin sesión → NoShow queda para fase futura; alertas al PACIENTE (requieren `PatientRefDto.UserId` + app móvil) y alerta `UpcomingAppointment` (scheduler) quedan para fase futura.

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

| Tabla | Notas |
|---|---|
| `telemedicine_requests` | Solicitud del paciente (referencias débiles, estado `Pending/Approved/Rejected/Cancelled/Converted`) |
| `telemedicine_appointments` | Cita (agregado raíz). `xmin` como token de concurrencia; **índice único parcial** `ix_appointments_professional_start_active` sobre `(professional_id, scheduled_start)` WHERE status IN activos → anti doble reserva en BD |
| `appointment_cancellations` | Historial append-only de cancelaciones |
| `appointment_reschedules` | Historial append-only de reprogramaciones |
| `virtual_rooms` | Sala en el proveedor (`provider`, `provider_room_sid`, `provider_room_name` único por proveedor → base de la idempotencia) |
| `telemedicine_sessions` | Sesión de video (estado independiente de la cita y de la sala) |
| `clinical_encounters` | Encuentro clínico, `clinical_data` jsonb extensible |
| `telemedicine_alerts` | Bandeja (eventos de dominio materializados; canal de entrega desacoplado) |
| `telemedicine_settings` | Reglas parametrizadas por organización/clínica |

**Estados separados a propósito**: cita ≠ sesión ≠ sala (máquinas de estado independientes).

## IVideoProvider

```csharp
CreateRoomAsync(RoomRequest, ct)          // idempotente por RoomName determinista ("apt-{id}")
GetRoomAsync(sidOrName, ct)               // null si no existe (404 mapeado)
CompleteRoomAsync(sid, ct)                // finaliza sala
GenerateAccessTokenAsync(AccessTokenRequest, ct)  // JWT identity + room, TTL corto
GetParticipantsAsync(sid, ct)
ValidateWebhookSignatureAsync(WebhookValidationRequest, ct)  // X-Twilio-Signature
```

Añadir otro proveedor = nueva clase que implemente `IVideoProvider` + un `else if` en `DependencyInjection.AddVideoProvider` (config `Telemedicine:Provider`).

## Fase 3 — Agendamiento (API)

Controladores bajo `/api/v1/telemedicine/*` (JWT del Auth Service, autorización por claim `permission`):

```
POST   /api/v1/telemedicine/requests                    # Paciente solicita (Pending)      [Telemedicine.RequestsCreate]
GET    /api/v1/telemedicine/requests/{id}               # Detalle de solicitud             [Telemedicine.RequestsView]
GET    /api/v1/telemedicine/requests/mine?patientId=    # Solicitudes del paciente         [Telemedicine.RequestsView]
POST   /api/v1/telemedicine/requests/{id}/confirm       # Confirma → crea cita Confirmed   [Telemedicine.RequestsConfirm]

POST   /api/v1/telemedicine/appointments                # Agendamiento directo del doctor   [Telemedicine.AppointmentsSchedule]
GET    /api/v1/telemedicine/appointments/{id}           # Detalle de cita (nombres resueltos) [Telemedicine.AppointmentsView]
GET    /api/v1/telemedicine/appointments/agenda?professionalId&from&to  # Agenda/calendario [Telemedicine.AgendaView]
POST   /api/v1/telemedicine/appointments/{id}/cancel    # Cancelar (historial append-only)  [Telemedicine.AppointmentsCancel]
POST   /api/v1/telemedicine/appointments/{id}/reschedule# Reprogramación inmediata          [Telemedicine.AppointmentsReschedule]
```

**Reglas de negocio** (todas parametrizadas en `tele.telemedicine_settings`): duración (default 30 min, máx 240), anticipación mínima, ventana máxima, límite de reprogramaciones (default 2). La reprogramación es inmediata y registra `appointment_reschedules` (historial append-only); la cita vuelve a `Confirmed` con la nueva hora.

**Concurrencia (anti doble reserva)** — tres capas:
1. Verificación de solapamiento en aplicación (`IAppointmentRepository.HasActiveOverlapAsync`) → error amigable 409.
2. Índice único parcial `ix_appointments_professional_start_active` (mismo inicio exacto).
3. **Constraint de exclusión GiST** `ex_appointments_professional_no_overlap` (migración `AddAppointmentOverlapExclusion`, requiere extensión `btree_gist`) → garantía real ante dos reservas simultáneas (exclusion violation traducida a 409). Además índice único parcial `ix_appointments_request_id`: una solicitud → una sola cita (anti doble confirmación).

**Datos de referencia**: el microservicio NO posee los datos maestros. Valida existencia y resuelve nombres contra internal endpoints del backend (`X-Internal-Key`):

```
GET /api/v1/internal/telemedicine/professionals/{id}    # id = erp.professionals (no employee)
GET /api/v1/internal/telemedicine/patients/{id}
GET /api/v1/internal/telemedicine/specialties/{id}
GET /api/v1/internal/telemedicine/locations/{id}
```

La lectura en listados deduplica por entidad única (sin N+1). Si el backend no responde → 503 (`UpstreamUnavailableException`).

## Fase 4 — Salas y sesiones (join-token, ventana, webhooks, finalización)

Ciclo de video por cita (estados separados a propósito: cita ≠ sesión ≠ sala):

```text
Cita Confirmed → (join-token dentro de la ventana) → sala creada en Twilio (lazy)
Cita Confirmed/InProgress → session/start → sesión Active + cita InProgress
sesión Active → session/end | webhook room-ended → sesión Ended + sala Ended + cita Completed
```

### API

```text
POST /api/v1/telemedicine/appointments/{id}/join-token     # token de acceso (crea la sala si no existe) — participante o supervisor
GET  /api/v1/telemedicine/appointments/{id}/room           # sala + participantes en vivo — participante o supervisor
POST /api/v1/telemedicine/appointments/{id}/session/start  # inicia sesión → cita InProgress — profesional o supervisor
POST /api/v1/telemedicine/appointments/{id}/session/end    # finaliza → cita Completed (idempotente) — profesional o supervisor
POST /api/v1/telemedicine/webhooks/twilio                  # eventos de Twilio (firma validada, idempotente) — anónimo
```

La autorización de los 4 primeros se resuelve en el handler a partir del JWT: el participante se deriva de la identidad (user → profesional/paciente de la cita) o del permiso de supervisión `Telemedicine.SessionsManage`. Por eso los endpoints NO llevan `[RequirePermission]`: el paciente (app móvil) no tiene roles ERP y se autoriza por identidad.

### Reglas de negocio

- **Ventana de acceso**: abre `RoomOpenBeforeMinutes` (default 10) antes del inicio y cierra `RoomCloseAfterMinutes` (default 15) después (settings por org/clínica). `join-token`/`session/start` fuera de la ventana → 409.
- **Sala lazy e idempotente**: se crea en el primer `join-token`/`start` dentro de la ventana. Nombre determinista `apt-{appointmentId}` → idempotencia por índice único `(provider, provider_room_name)` + `UniqueName` de Twilio (una carrera entre dos join-token devuelve la misma sala).
- **Sesión**: una activa a la vez por cita (`start` doble → 409, protegido además por el token de concurrencia xmin de la cita). `end` es idempotente: sin sesión activa → no-op 200.
- **Webhooks**: firma `X-Twilio-Signature` validada (deshabilitada en dev, `Twilio:ValidateWebhookSignature`). Clave de idempotencia `(event_type, room_sid, participant_sid)` en `tele.telemedicine_webhook_events` (índice único): los duplicados concurrentes se serializan y el perdedor recibe `Duplicate` con rollback de sus mutaciones. El procesamiento es atómico (reserva de la clave + mutaciones en una transacción).
  - `room-ended` → sala `Ended`, sesión activa `Ended`, y la cita `InProgress` pasa a `Completed`. Si nunca hubo sesión activa, la cita NO se completa automáticamente (NoShow es una decisión de negocio aparte, fase futura/admin).
  - `participant-connected` → sala `Active` (si estaba Created/Waiting) + timestamp del evento en la sesión. `participant-disconnected` → solo timestamp.
- **Completar la sala en Twilio es best-effort** en `session/end`: si Twilio no responde, la sesión/cita se finalizan igual (la sala termina sola o vía webhook).

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

| Evento de dominio | Alerta | Destinatario |
|---|---|---|
| Nueva solicitud (`CreateTelemedicineRequest`) | `NewRequest` | Profesional elegido por el paciente |
| Cita creada/confirmada (`Schedule`/`Confirm`) | `NewAppointment` | Profesional asignado |
| Reprogramación (`Reschedule`) | `AppointmentRescheduled` | Profesional asignado |
| Cancelación (`Cancel`) | `AppointmentCancelled` | Profesional asignado |
| `participant-connected` (webhook) — el participante es el **paciente** | `PatientWaiting` | Profesional |
| `participant-connected` (webhook) — el participante es el **profesional** | `PatientJoined` | Profesional |
| `participant-disconnected` (webhook) — el participante es el **paciente** | `ParticipantLeft` | Profesional |
| `room-ended` (webhook) con sesión | `SessionEnded` | Profesional |

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
GET  /api/v1/telemedicine/appointments/{id}/encounter        # registro clínico (404 si no existe aún)
PUT  /api/v1/telemedicine/appointments/{id}/encounter        # guarda borrador (crea si no existe; crea/actualiza)
POST /api/v1/telemedicine/appointments/{id}/encounter/complete  # finaliza Draft→Completed (acepta datos finales)
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

## Comandos

```bash
dotnet build src/Services/CoppAddresd.Telemedicine/CoppAddresd.Telemedicine.csproj
dotnet run --project src/Services/CoppAddresd.Telemedicine        # http://localhost:5130
dotnet ef migrations add <Nombre> --project src/Services/CoppAddresd.Telemedicine --startup-project src/Services/CoppAddresd.Telemedicine --output-dir Infrastructure/Migrations
dotnet ef database update --project src/Services/CoppAddresd.Telemedicine --startup-project src/Services/CoppAddresd.Telemedicine
```

> Usar siempre `--output-dir Infrastructure/Migrations`: `MigrationsDirectory` del csproj no se honró en este setup.
