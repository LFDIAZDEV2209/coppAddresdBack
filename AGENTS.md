# CoppAddresd Backend — Agent Guide

.NET 10 / C# 13 backend, Clean Architecture. **Estado actual**: Auth Service completo (OTP por identificación con Twilio Verify SMS + protección OtpSecurity), integración AI Chat, sistema de auditoría PostgreSQL.

## Commands

```bash
dotnet build                                  # build solution (usa CoppAddresd.slnx)
dotnet test                                   # all tests (xUnit)
dotnet test tests/CoppAddresd.UnitTests       # unit tests only
dotnet test --filter "FullyQualifiedName~X"   # single test
dotnet run --project src/CoppAddresd.Api      # main API (http://localhost:5122, https 7258)
dotnet run --project src/Services/CoppAddresd.Auth  # auth service (http://localhost:5123, https 7230)
dotnet run --project src/Services/CoppAddresd.Telemedicine  # telemedicine service (http://localhost:5130, https 7130)
dotnet --version                              # necesita 10.0+
```

No hay script de lint/format. Build debe pasar antes de considerar trabajo terminado.

## Project layout & architecture

Flujo de dependencias hacia adentro, enforceado solo por referencias csproj:

- `src/CoppAddresd.Domain` — entidades/ValueObjects/enums/exceptions. Sin deps. **Contenido real**: `ActivityLog`, `AuditAction`, `AuditActorType`.
- `src/CoppAddresd.Application` — handlers MediatR en `Features/`, FluentValidation, DTOs, interfaces. Deps: Domain. **Contenido real**: `ChatCommand/Handler`, `StreamChatCommand/Handler`, `IAiServiceClient`, `AiServiceSettings`.
- `src/CoppAddresd.Infrastructure` — EF Core, PostgreSQL (Npgsql), Identity, JWT. Deps: Domain, Application. **Contenido real**: `AppDbContext` (audit), `AiServiceClient` con Polly resilience, `AuditTriggerInterceptor` (GUC-based).
- `src/CoppAddresd.Api` — minimal API host. Deps: Application, Infrastructure. **Contenido real**: `ChatController` (sync + SSE streaming), JWT auth, CORS, Swagger.
- `src/Services/CoppAddresd.Auth` — **servicio web standalone**, no referencia otros proyectos (solo NuGet: JwtBearer, Identity EF, Npgsql, Twilio). **Contenido real**: Identity completo, JWT + refresh tokens, permisos granulares, roles, usuarios, seeders, rate limiting, health checks, **OTP por identificación** (PHONE → Twilio Verify SMS vía `ITwilioOtpService`/`TwilioOtpService`; EMAIL → flujo local con `auth.otp_codes`) y **motor de protección OTP** (`IOtpProtectionService`/`OtpProtectionService`, en memoria, límites por IP/teléfono/documento + cooldown + lockout).
- `src/Services/CoppAddresd.Telemedicine` — **servicio web standalone** (Clean Architecture por carpetas, precedente: Auth Service), no referencia otros proyectos. **Contenido real**: telemedicina (solicitudes, citas, agenda, calendario, salas virtuales, encuentros, alertas, listados admin globales), schema `tele.` propio, JWT del Auth Service, video con Twilio (`IVideoProvider` desacoplado), anti doble reserva con exclusión GiST, **auditoría clínica** (trigger `audit.*` en `clinical_encounters` + propagación del actor del JWT vía `AuditTriggerInterceptor`/`HttpAuditActorContext`, guardado del encuentro en transacción explícita). **Tests propios (Fase 11-12)**: `tests/CoppAddresd.Telemedicine.UnitTests` (144, fakes en memoria) e `tests/CoppAddresd.Telemedicine.IntegrationTests` (25, BD aislada `coppaddresd_tele_test_*` vía `COP_TEST_DB_CONNECTION`; anti doble reserva concurrente + idempotencia webhook/sala/encuentro). Endpoints de la UI: `GET /api/v1/telemedicine/me` (contexto del JWT), `GET /me/*` (`appointments`/`requests`/`summary`/`analytics` — alcance del profesional por identidad del JWT, nunca por id del cliente; 403 sin perfil clínico) y `GET /admin/*` (listados globales, permiso `Telemedicine.AdminView`). El catálogo de profesionales para la UI vive en el backend (`GET /api/v1/professionals-catalog`). Doc: `docs/modules/telemedicine/README.md`.

`CoppAddresd.slnx` es el nuevo formato XML de soluciones — `.sln` plano no existe. Herramientas esperando `.sln` fallarán.

## Database schema organization

```sql
Schema public:  __EFMigrationsHistory (solo)
Schema auth:    20 tablas (Users, Roles, Permissions, Applications, UserApplications, RefreshTokens, ScopedRoleAssignments, ScopedPermissionAssignments, Invitations, OtpCodes, etc.)
Schema app:     ~69 tablas: núcleo de pacientes (patient_profiles → auth.users, patient_professionals
                = asignación paciente↔profesional, base del alcance "propios"), catálogos clínicos
                (allergens, icd10_codes, medications, insurers, unit_of_measures, measurement_metrics,
                measurement_reference_ranges), diagnósticos/medicamentos/alergias/signos vitales,
                documentos + media, wellness (nutrition_plans, exercise_routines, assignments,
                plan_safety_rules), program progress (program_*, xp_*, streak_*, daily_checkins,
                device_tokens), encounters (canónico) + clinical_measurements y 16 tablas del módulo
                Tests de Salud (health_test_*: instrumentos/versiones/preguntas/opciones/rangos,
                baterías + ítems + asignaciones, evaluaciones/respuestas/resultados, indicadores,
                reglas de alerta, alertas, comentarios). Docs: docs/modules/health-tests/README.md,
                docs/modules/program-progress/README.md, docs/modules/clinical-measurements/README.md
Schema erp:     21 tablas (organizations → clinics → locations; employees como
                núcleo HR con extensión clínica 1:0..1 professionals; catálogos
                professional_types/specialties + puentes N:N + professional_licenses;
                erp.professional_schedules (id uuid PK gen_random_uuid(), professional_id uuid FK→erp.professionals cascade, weekday int 1–7 ISO lunes=1, start_time/end_time time, created_at/updated_at timestamptz; índices uq_professional_schedules_professional_weekday [professional_id+weekday] + ix_professional_schedules_professional_id);
                inventory_*/products/store_items; legal_documents*).
                Plan de evolución del módulo: docs/modules/patients/PLAN.md
Schema audit:   1 tabla (activity_logs)
Schema tele:   10 tablas (telemedicine_requests, appointments, appointment_cancellations/reschedules, virtual_rooms, telemedicine_sessions, clinical_encounters, telemedicine_alerts, telemedicine_settings, telemedicine_webhook_events). Historial de migraciones propio en tele.__ef_migrations_history (aislado del public.__EFMigrationsHistory).

# Historial de migraciones por microservicio (NO compartir public):
#   - Backend (AppDbContext): public.__EFMigrationsHistory (47 migraciones)
#   - Auth (AuthDbContext):   auth.__ef_migrations_history (11 migraciones, aislada)
#   - Telemedicina:           tele.__ef_migrations_history (7 migraciones, aislada)
#   - Community:              community.__ef_migrations_history (5 migraciones, aislada)
# EF no namespacia las IDs por contexto: compartir la tabla public mezclaba las
# migraciones de Auth y del backend (errores de 'migrations remove' del contexto
# equivocado, auditoría ambigua). Cada DbContext configura su historial con
# npgsql.MigrationsHistoryTable("__ef_migrations_history", "<schema>").
```

**Regla**: Cada módulo tiene su schema. Auth usa `auth.`, auditoría usa `audit.`, la app móvil `app.`, el ERP `erp.`; `public` se mantiene mínimo.

**Aplicaciones**: `auth.applications` (códigos `erp`/`app`) + `auth.user_applications` determinan a qué aplicación accede cada usuario (nunca se asume rol→app). El `aud` del JWT es el código de la aplicación del login; ambos servicios validan con `Jwt:ValidAudiences` (fallback `["erp","app"]`). El login requiere el campo `application`; el refresh conserva la aplicación ligada al refresh token.

## Auth Service — Endpoints y permisos

```
POST   /api/auth/login              # Login (email + password + application + rememberMe) → access token en body,
                                    #   refresh en cookie HttpOnly copp_refresh_token. application = código
                                    #   ("erp"/"app") → aud del JWT; requiere UserApplication para esa app.
POST   /api/auth/refresh            # Refresh con la cookie (sin body) — rotación + Set-Cookie nuevo token.
                                    #   401 limpia la cookie corrupta. Header X-Refresh-Status:
                                    #   "missing" (nunca hubo cookie) | "invalid" (token inválido)
POST   /api/auth/logout             # Revoca todos los refresh del usuario + limpia cookie. Sin [Authorize]
POST   /api/auth/change-password    # Cambiar password [Authorize]

GET    /api/auth/me                 # Info del usuario actual + roles + permisos [Authorize]

GET    /api/auth/users               # Listar usuarios [RequirePermission("Users.View")]
POST   /api/auth/users               # Crear usuario [AllowAnonymous]
POST   /api/auth/users/bulk           # Creación masiva [AllowAnonymous] (máx 500 filas; cada fila independiente; contraseñas temporales generadas server-side, retornadas una sola vez)
GET    /api/auth/users/{id}          # Obtener usuario [RequirePermission("Users.View")]
PUT    /api/auth/users/{id}          # Actualizar usuario [RequirePermission("Users.Update")]
DELETE /api/auth/users/{id}          # Eliminar usuario [RequirePermission("Users.Delete")]

GET    /api/auth/roles               # Listar roles [RequirePermission("Roles.View")]
POST   /api/auth/roles               # Crear rol [RequirePermission("Roles.Create")]
PUT    /api/auth/roles/{id}          # Actualizar rol [RequirePermission("Roles.Update")]
DELETE /api/auth/roles/{id}          # Eliminar rol [RequirePermission("Roles.Delete")]
POST   /api/auth/roles/{id}/assign   # Asignar rol a usuario [RequirePermission("Roles.Assign")]
DELETE /api/auth/roles/{id}/assign   # Remover rol de usuario [RequirePermission("Roles.Assign")]

GET    /api/auth/permissions         # Listar permisos [RequirePermission("Permissions.View")]
POST   /api/auth/permissions         # Crear permiso [RequirePermission("Permissions.View")]
PUT    /api/auth/permissions/{id}    # Actualizar permiso [RequirePermission("Permissions.View")]
DELETE /api/auth/permissions/{id}    # Eliminar permiso [RequirePermission("Permissions.View")]
POST   /api/auth/permissions/{id}/assign-to-role    # Asignar a rol [RequirePermission("Permissions.Assign")]
DELETE /api/auth/permissions/{id}/assign-to-role    # Remover de rol [RequirePermission("Permissions.Assign")]
POST   /api/auth/permissions/{id}/assign-to-user   # Asignar a usuario [RequirePermission("Permissions.Assign")]
DELETE /api/auth/permissions/{id}/assign-to-user   # Remover de usuario [RequirePermission("Permissions.Assign")]

# Asignaciones con scope (permisos por contexto: clínica/organización)
POST   /api/auth/users/{id}/scoped/roles           # Rol scoped [RequirePermission("Roles.Assign")]
DELETE /api/auth/users/{id}/scoped/roles           # Remover rol scoped [RequirePermission("Roles.Assign")]
POST   /api/auth/users/{id}/scoped/permissions     # Override Grant/Deny scoped [RequirePermission("Permissions.Assign")]
DELETE /api/auth/users/{id}/scoped/permissions     # Remover override scoped [RequirePermission("Permissions.Assign")]

# Internos (ERP → Auth, header X-Internal-Key)
GET    /api/auth/internal/authorize           # ¿Permiso en cadena de scopes? (?userId&permissionCode&scopes=Clinic:id|Organization:id|Global)
GET    /api/auth/internal/scoped-permissions  # Permisos efectivos para una cadena de scopes (?userId&scopes=...)
POST   /api/auth/internal/invitations         # Crear usuario sin password + acceso ERP + invitación + email (body: email, firstName, lastName)

# Invitaciones de primer acceso (onboarding del profesional)
GET    /api/auth/invitations/validate?token=       # Validar token (público, no consume)
POST   /api/auth/invitations/accept                # Establecer password y marcar usada (público: token, password)
POST   /api/auth/invitations/{id}/resend           # Reenviar (revoca la pendiente) [RequirePermission("Users.Update")]
POST   /api/auth/invitations/{id}/revoke           # Revocar [RequirePermission("Users.Update")]
```

**Permisos seedeados** (84 total): `Users.*`, `Roles.*`, `Permissions.*`, `Agents.*`, `Organizations.*`, `Clinics.*`, `Locations.*`, `Employees.*`, `Professionals.*`, `Patients.*`, `Documents.*`, `ClinicalRecords.*`, `Telemedicine.*` (incluye `Telemedicine.AdminView` para listados admin globales), `Appointments.*`, `Finance.*` (`Finance.View`/`Finance.Manage`), `Reports.View`, `Inventory.*`, `Store.*`, `Media.*`, `Audit.*`, `System.AdminSettings`, `Community.*`. Roles: `Admin` (global, todos los permisos) + `OrganizationAdmin`, `ClinicAdmin`, `ClinicalDirector`, `Professional` (rol clínico consolidado; **los roles Physician/Nutritionist/Psychologist son aliases legado desactivados — IsActive = false**; no se asignan a usuarios nuevos y los existentes conservan sus permisos ya que la cadena de permisos no filtra IsActive), `Nurse`, `Receptionist`, `CareCoordinator`, `Coordinator` (alias de CareCoordinator), `Finance` (sin acceso clínico), `Auditor` (solo lectura) — asignables con scope de clínica/org. **Convención de escalabilidad**: roles FUNCIONALES por capacidad, no por profesión; la especialidad nunca determina permisos. Los roles de sistema llevan `IsSystem = true` (no renombrables/eliminables sin `System.AdminSettings`); las mutaciones de roles/permisos/usuarios exigen `System.AdminSettings` (Admin la tiene vía AdminSeeder).

**Credenciales admin**: `admin@coppaddresd.com` / `Test@1234` (configurable en `appsettings.json` → `Auth` section).

**Cookie de refresh**: `copp_refresh_token` — HttpOnly, `SameSite=Lax`, `Secure` solo fuera de Development, `Path=/api/auth`, `rememberMe=true` → 7 días / `false` → 8 h. El access token (15 min) va en header Bearer, nunca en cookie.

**CORS**: whitelist configurable en `Cors:Origins` (default `http://localhost:3000`) con `AllowCredentials` y expone `X-Refresh-Status`. La API principal usa la misma whitelist pero sin credentials (solo Bearer).

## Auth Service — OTP por identificación y protección

```
POST   /api/auth/id-lookup  # Buscar paciente por documento → contactos enmascarados
POST   /api/auth/send-otp   # Enviar OTP: PHONE → Twilio Verify SMS; EMAIL → local (auth.otp_codes)
POST   /api/auth/verify-otp # Verificar OTP: PHONE → Twilio Check (approved); EMAIL → hash local
```

**Canal PHONE** (Twilio Verify V2, SMS): `OtpService` → `ITwilioOtpService` →
`TwilioOtpService` (único que conoce el SDK). Twilio genera/almacena/verifica el
código; PHONE **no genera OTP local, no usa `auth.otp_codes` ni devuelve
devCode**. Teléfono en E.164 (ej. Colombia `3053924819` → `+573053924819`).

**Canal EMAIL**: flujo local intacto (hash SHA-256 + salt, expiración 5 min,
máx. 5 intentos, `auth.otp_codes`, devCode solo en Development). No usa Twilio.

**Protección `OtpSecurity`** (adicional al rate limiter global): SEND por IP
5/min + 30/h, teléfono 3/min + 10/h + 20/día, cooldown 60 s, documento 5/h;
VERIFY por IP 30/min, teléfono 10/5 min, máx. 5 fallos, lockout 300 s.
`CheckCanSend`/`CheckCanVerify` no consumen cuota; `RegisterSend` solo tras
aceptación de Twilio; bloqueos locales (cooldown/límites/lockout) → **429** con
el shape del rate limiter + `Retry-After`, **antes** de llamar a Twilio.

**Limitación actual**: `OtpProtectionService` es **en memoria** (Singleton) —
los contadores se reinician al reiniciar y NO se comparten entre réplicas;
si el Auth Service escala horizontalmente, migrar a Redis/`IDistributedCache`.

**Errores**: `TwilioOtpException` (400 inválido, 429 rate limit Twilio 60203,
503 proveedor/deshabilitado, 502 resto) y `OtpProtectionException` (429 local),
mapeadas en `GlobalExceptionHandlerMiddleware`; nunca se exponen credenciales,
stack traces ni razones internas de bloqueo.

**Validación real realizada** (Fases 5A–6B-04C): SMS real, cooldown 429,
verificación `approved` + JWT/refresh/cookie, 5 fallos → 401, 6º intento →
429 lockout + Retry-After sin llamar a Twilio.

## Chat/AI Integration — Endpoints

```
POST   /api/v1/chat                 # Chat síncrono (vía MediatR → AI Service) [Authorize]
POST   /api/v1/chat/stream          # Chat streaming SSE (text/event-stream) [Authorize]
GET    /api/v1/agents/executions    # Ejecuciones de agentes (monitoreo, proxy del AI Service)
GET    /api/v1/agents/executions/{id}  # Detalle de una ejecución (12 preguntas del monitoreo)
```

## Food AI (análisis de alimentos) — Endpoints

```
GET    /api/v1/foodai/health        # Probe backend → food-ai-service (8010) [AllowAnonymous]
POST   /api/v1/foodai/analyze       # Ingesta multipart (image) → {analysisId, status:"received"}
                                    #   [AllowAnonymous por ahora; auth cuando haya endpoints de negocio]
```

**Flujo**: `AnalyzeFoodImageCommand` (MediatR) → `ImageFileValidator` (extensión/MIME/10 MB/firma mágica) → `IImageStorage`/`LocalImageStorage` (delega en `IObjectStorageService` existente, clave `foodai/<analysisId>.<ext>` → `.local-storage/foodai/` en dev) → `IFoodAiClient`/`FoodAiClient.SendImageAsync` (multipart `image`+`analysis_id` a `/analyze`, snake_case, errores → `FoodAiException` 502).

**Configuración**: `appsettings.json` → `FoodAi` section (BaseUrl `http://localhost:8010`, TimeoutSeconds 10, MaxImageSizeBytes 10 MB, AllowedContentTypes/Extensions). El food-ai-service NO comparte puerto ni código con el `ai-service` (LangGraph, 8000).

**Chat multi-agente**: `ChatRequestDto` acepta `agentTypeId` y `userId` (aislamiento de
memoria por usuario en el AI Service); `ChatResult` incluye `executionId` (feedback).
El `AiServiceClient` mapea el contrato del AI Service (`answer`/`thread_id`/`execution_id`)
con `JsonPropertyName` y nunca envía `agent: null` (schema del AI Service lo rechaza con 422).

**Configuración**: `appsettings.json` → `AiService` section (BaseUrl, ApiPrefix, TimeoutSeconds, InternalApiKey).

**Resilience**: Polly retry (3 intentos, backoff exponencial) + circuit breaker (5 fallos, 30s break).

**Monitoreo admin**: el frontend NUNCA llama al AI Service directo — `AgentsController` proxya
`GET /api/v1/agents/executions` hacia `/admin/executions` del AI Service vía
`AgentExecutionsQueryService` (resiliente: fallo → lista vacía, nunca 500 al cliente).
La primera versión de un agente se inserta y activa en UNA transacción
(`AddFirstVersionAndActivateAsync`, envuelta en `CreateExecutionStrategy` porque
NpgsqlRetryingExecutionStrategy no soporta transacciones manuales). La activación de una
versión usa `SetActiveVersionAsync` (ExecuteUpdate directo) — el tracking de la navegación
`ActiveVersion` (cargada con Include) reescribía `active_version_id` al guardar.

## Gestión de personas (ERP) — Endpoints

```
GET    /api/v1/employees                      # Listar empleados con filtros y paginación [Employees.View]
GET    /api/v1/employees/{id}                 # Obtener empleado por id [Employees.View]
POST   /api/v1/employees                      # Crear empleado [Employees.Create]
PUT    /api/v1/employees/{id}                 # Actualizar empleado [Employees.Update]
POST   /api/v1/employees/{id}/invite          # Invitar empleado (crea usuario Auth + envía enlace) [Employees.Create]
POST   /api/v1/employees/bulk                 # Creación masiva desde CSV [Employees.Create] (body: { organizationId, rows: [{ firstName, lastName, email, professionalTypeName?, status activo|invitado|inactivo }] }; cada fila independiente; se envía invitación de acceso automáticamente — si falla la invitación, la fila se compensa (no queda empleado))
POST   /api/v1/patients/bulk                  # Creación masiva desde CSV [Patients.Create] (body: { clinicId?, rows: [{ firstName, lastName, documentNumber?, email?, status? }] }; status ∈ activo|inactivo (null→Activo); duplicado documentNumber detecta batch+BD; MRN auto-generado; 500 filas max)
GET    /api/v1/professionals/stats            # Estadísticas del directorio (totales + desglose por tipo) [Professionals.View]
POST   /api/v1/professionals                  # Crear profesional orquestado (empleado + extensión clínica + clínicas + invitación + scopes) [Professionals.Create]
GET    /api/v1/professionals/{id}/scopes      # Asignaciones scoped del profesional (roles + overrides por clínica) [Professionals.View]
PUT    /api/v1/professionals/{id}/scopes      # Reemplazar asignaciones scoped [Professionals.Update]
GET    /api/v1/professionals/{id}/schedules   # Horarios semanales de atención [Professionals.View] → [{ weekday 1–7 ISO, startTime HH:mm, endTime HH:mm }]
PUT    /api/v1/professionals/{id}/schedules   # Reemplazar horarios semanales [Professionals.Update] (body: { schedules: [{ weekday 1–7, startTime, endTime }] }; máx. 7 filas, weekday único, endTime > startTime)
```

Horarios (`erp.professional_schedules`) viven por profesional: hasta 7 filas (una por día); días sin atención no tienen fila. El PUT reemplaza el set completo de forma transaccional.

## Audit System

**Trigger-based**: PostgreSQL trigger automático en INSERT/UPDATE/DELETE.

**Actor propagation**: EF Core interceptor usa GUC variables (`audit.actor_type`, `audit.user_id`, etc.) con `set_config(..., true)` (transactional). Previene leaks en connection pooling.

**Tests**: 7 integration tests passing (requiere PostgreSQL real con variable `COP_TEST_DB_CONNECTION`).

## Gotchas

- **`appsettings.json` / `appsettings.*.json` están gitignoreados** (`src/CoppAddresd.Api`, `src/Services/CoppAddresd.Auth` y `src/Services/CoppAddresd.Telemedicine`). Deben crearse localmente antes de correr. Hay `appsettings.Example.json` solo en Auth.
- **Caché distribuida (Valkey)**: `Cache:Provider` elige `Valkey` (default; local 127.0.0.1:6379 AUTH del compose, prod ElastiCache), `Memory` (tests) o `None` (rollback sin redeploy). Contrato **fail-open por operación**: un fallo de caché nunca rompe la request (log Warning + fuente de datos); `/health` reporta el componente `valkey` como Degraded (HTTP 200), no Unhealthy. Claves namespaced por servicio (`erp:`/`auth:`/`tele:`) + sufijo de versión (`:v1`) — agregar un caso = `GetOrCreateAsync` en el handler (nunca en controllers) + `RemoveAsync` en la mutación. Los tres servicios llevan su propia copia de la abstracción (standalone). Usar `127.0.0.1` (no `localhost`: resuelve primero a IPv6 y falla). Detalle completo: `docs/modules/cache/README.md`.
- **Storage de objetos**: `Storage:Provider` elige `Local` (filesystem, dev) o `S3` (AWS, prod). Con S3 el `upload-intent`/`download` devuelven presigned URLs reales del bucket `cooppadresd-storage-prod` (región `us-east-2`); las credenciales salen de la cadena por defecto del SDK (IAM role), nunca de Access Keys. Config en `appsettings` + fallback a variables `AWS_REGION`/`AWS_S3_*`. Detalle en `docs/modules/storage/README.md`. Si `Storage:Provider=S3` sin credenciales AWS configuradas, la primera operación de storage fallará con error de credenciales del SDK (fail fast en uso).
- **JWT debe ser idéntico** entre API y Auth Service (mismo Secret, Issuer, Audience) para que los tokens funcionen.
- **Auth Service corre migraciones + seeders automáticamente** al iniciar (Program.cs).
- **Telemedicine NO corre migraciones al iniciar** (a diferencia de Auth): localmente aplicar con `dotnet ef database update --project src/Services/CoppAddresd.Telemedicine --startup-project src/Services/CoppAddresd.Telemedicine`; en el servidor las aplica el job `migrate` del pipeline (`deploy-backend.yml`) vía el modo `--migrate` del `Program.cs` (one-off task ECS en la VPC; la API principal usa el mismo mecanismo con su historial `public.__EFMigrationsHistory`). Para nuevas migraciones usar siempre `--output-dir Infrastructure/Migrations` (`MigrationsDirectory` del csproj no se honra). Gotchas: la exclusión GiST requiere extensión `btree_gist` (la crea la migración); `TwilioClient.Init(apiKeySid, apiKeySecret, accountSid)` — el orden es (username, password, accountSid); NO usar `SetRegion` con Twilio Video. Detalle completo: `docs/modules/telemedicine/README.md`.
- **`HttpAuditActorContext`** actualmente retorna `ActorType=System`, `UserId=null` — no hay integración con Identity todavía.
- **Tests de integración** requieren PostgreSQL real (no InMemory). Configurar variable `COP_TEST_DB_CONNECTION`.
- **`OtpProtectionService` es en memoria** (Singleton): contadores se reinician al reiniciar el Auth Service y no se comparten entre réplicas — si se escala horizontalmente, migrar a Redis/`IDistributedCache`.
- **Twilio Verify puede devolver 429/60203** (rate limit por número) aunque la protección local permita el envío: el error se propaga como `TwilioOtpException` (RateLimited → 429) y `RegisterSend` NO se ejecuta (no consume cuota local).
- **`AiServiceClient` mapea el contrato del AI Service** (`answer`/`thread_id`/`execution_id`) con `JsonPropertyName` — si el AI Service cambia el schema, ajustar `ChatResponseJson`.
- **Comentarios/docs en español** por convención del README.

## Project skills & docs (MANDATORIO antes de trabajo sustancial)

- **Skills de estándares** viven en `.agents/skills/` (architecture, entity-framework, linq, pagination, query-performance, database-indexes, database-normalization, database, concurrency, cancellation-token, transactions, n-plus-one, production-performance, aws-production, api-design, repository-pattern, service-layer, error-handling, logging, caching, testing, migrations, documentation, security, dotnet). Cargar `architecture` PRIMERO, luego los que apliquen al módulo — codifican reglas de producción/PostgreSQL/concurrencia (N+1, CancellationToken propagation, keyset pagination, index documentation, transaction hygiene, secrets policy).
- **Analítica de dashboards (pre-agregación)**: si el cambio agrega un dato nuevo a contar en algún dashboard (KPI, serie temporal, contador, distribución por tipo/hora) o modifica agregaciones existentes, **EVALUAR si corresponde al modelo de pre-agregación CQRS (Channel Pattern)** y, si aplica, implementar el pipeline completo (evento → cola → processor → rollup) para que el dato se guarde **automáticamente** en las tablas de pre-agregación. Cargar la skill `cqrs-preaggregation` (`.agents/skills/cqrs-preaggregation/SKILL.md`) ANTES de escribir código. Referencias: `docs/architecture/analytics-cqrs-preaggregation.md` y `docs/modules/*/analytics.md` (6 módulos ya implementados: Patients, ProgramProgress, Telemedicine, HealthTests, Community, Inventory).
- `docs/` — docs por módulo + database/performance/AWS en español; actualizar el doc del módulo en la misma tarea que cambia código. Docs y skills están en español (convención del repo).
- Skills estándar faltantes (csharp-_, dotnet-_, aspnet-core) también existen en `.agents/skills/`.

## Sources of truth

- `README.md` — arquitectura completa, versiones del stack (MediatR 14.2.0, FluentValidation 12.1.1, EF 10.0.10/Npgsql 10.0.3), puertos. Mayormente preciso para el estado actual.

<!-- CODEGRAPH_START -->

## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code:

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.

If there is no `.codegraph/` directory, skip CodeGraph entirely — indexing is the user's decision.
<!-- CODEGRAPH_END -->
