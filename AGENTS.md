# CoppAddresd Backend — Agent Guide

.NET 10 / C# 13 backend, Clean Architecture. **Estado actual**: Auth Service completo, integración AI Chat, sistema de auditoría PostgreSQL.

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
- `src/Services/CoppAddresd.Auth` — **servicio web standalone**, no referencia otros proyectos (solo NuGet: JwtBearer, Identity EF, Npgsql). **Contenido real**: Identity completo, JWT + refresh tokens, permisos granulares, roles, usuarios, seeders, rate limiting, health checks.
- `src/Services/CoppAddresd.Telemedicine` — **servicio web standalone** (Clean Architecture por carpetas, precedente: Auth Service), no referencia otros proyectos. **Contenido real**: telemedicina (solicitudes, citas, agenda, calendario, salas virtuales, encuentros, alertas, listados admin globales), schema `tele.` propio, JWT del Auth Service, video con Twilio (`IVideoProvider` desacoplado), anti doble reserva con exclusión GiST, **auditoría clínica** (trigger `audit.*` en `clinical_encounters` + propagación del actor del JWT vía `AuditTriggerInterceptor`/`HttpAuditActorContext`, guardado del encuentro en transacción explícita). **Tests propios (Fase 11-12)**: `tests/CoppAddresd.Telemedicine.UnitTests` (134, fakes en memoria) e `tests/CoppAddresd.Telemedicine.IntegrationTests` (25, BD aislada `coppaddresd_tele_test_*` vía `COP_TEST_DB_CONNECTION`; anti doble reserva concurrente + idempotencia webhook/sala/encuentro). Endpoints de la UI: `GET /api/v1/telemedicine/me` (contexto del JWT) y `GET /admin/*` (listados globales, permiso `Telemedicine.AdminView`). El catálogo de profesionales para la UI vive en el backend (`GET /api/v1/professionals-catalog`). Doc: `docs/modules/telemedicine/README.md`.

`CoppAddresd.slnx` es el nuevo formato XML de soluciones — `.sln` plano no existe. Herramientas esperando `.sln` fallarán.

## Database schema organization

```sql
Schema public:  __EFMigrationsHistory (solo)
Schema auth:    16 tablas (Users, Roles, Permissions, Applications, UserApplications, RefreshTokens, ScopedRoleAssignments, ScopedPermissionAssignments, etc.)
Schema app:     11 tablas (patient_profiles → auth.users, patient_professionals
                (asignación paciente↔profesional, base del alcance "propios"),
                insurers, allergens, icd10_codes, medications, patient_diagnoses,
                patient_medications, patient_allergies, vital_signs)
Schema erp:     12 tablas (organizations → clinics → locations; employees como
                núcleo HR con extensión clínica 1:0..1 professionals; catálogos
                professional_types/specialties + puentes N:N + professional_licenses).
                Plan de evolución del módulo: docs/modules/patients/PLAN.md
Schema audit:   1 tabla (activity_logs)
Schema tele:    10 tablas (telemedicine_requests, telemedicine_appointments, appointment_cancellations/reschedules, virtual_rooms, telemedicine_sessions, clinical_encounters, telemedicine_alerts, telemedicine_settings, telemedicine_webhook_events). Historial de migraciones propio en tele.__ef_migrations_history (aislado del public.__EFMigrationsHistory).

# Historial de migraciones por microservicio (NO compartir public):
#   - Backend (AppDbContext): public.__EFMigrationsHistory (16 migraciones)
#   - Auth (AuthDbContext):   auth.__ef_migrations_history (5 migraciones, aislada)
#   - Telemedicina:           tele.__ef_migrations_history (4 migraciones, aislada)
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

GET    /api/me                      # Info del usuario actual + roles + permisos [Authorize]

GET    /api/users                   # Listar usuarios [RequirePermission("Users.View")]
POST   /api/users                   # Crear usuario [AllowAnonymous]
GET    /api/users/{id}              # Obtener usuario [RequirePermission("Users.View")]
PUT    /api/users/{id}              # Actualizar usuario [RequirePermission("Users.Update")]
DELETE /api/users/{id}              # Eliminar usuario [RequirePermission("Users.Delete")]

GET    /api/roles                   # Listar roles [RequirePermission("Roles.View")]
POST   /api/roles                   # Crear rol [RequirePermission("Roles.Create")]
PUT    /api/roles/{id}              # Actualizar rol [RequirePermission("Roles.Update")]
DELETE /api/roles/{id}              # Eliminar rol [RequirePermission("Roles.Delete")]
POST   /api/roles/{id}/assign       # Asignar rol a usuario [RequirePermission("Roles.Assign")]
DELETE /api/roles/{id}/assign       # Remover rol de usuario [RequirePermission("Roles.Assign")]

GET    /api/permissions             # Listar permisos [RequirePermission("Permissions.View")]
POST   /api/permissions             # Crear permiso [RequirePermission("Permissions.View")]
PUT    /api/permissions/{id}        # Actualizar permiso [RequirePermission("Permissions.View")]
DELETE /api/permissions/{id}        # Eliminar permiso [RequirePermission("Permissions.View")]
POST   /api/permissions/{id}/assign-to-role    # Asignar a rol [RequirePermission("Permissions.Assign")]
DELETE /api/permissions/{id}/assign-to-role    # Remover de rol [RequirePermission("Permissions.Assign")]
POST   /api/permissions/{id}/assign-to-user   # Asignar a usuario [RequirePermission("Permissions.Assign")]
DELETE /api/permissions/{id}/assign-to-user   # Remover de usuario [RequirePermission("Permissions.Assign")]

# Asignaciones con scope (permisos por contexto: clínica/organización)
POST   /api/users/{id}/scoped/roles           # Rol scoped [RequirePermission("Roles.Assign")]
DELETE /api/users/{id}/scoped/roles           # Remover rol scoped [RequirePermission("Roles.Assign")]
POST   /api/users/{id}/scoped/permissions     # Override Grant/Deny scoped [RequirePermission("Permissions.Assign")]
DELETE /api/users/{id}/scoped/permissions     # Remover override scoped [RequirePermission("Permissions.Assign")]

# Internos (ERP → Auth, header X-Internal-Key)
GET    /api/auth/internal/authorize           # ¿Permiso en cadena de scopes? (?userId&permissionCode&scopes=Clinic:id|Organization:id|Global)
GET    /api/auth/internal/scoped-permissions  # Permisos efectivos para una cadena de scopes (?userId&scopes=...)
POST   /api/auth/internal/invitations         # Crear usuario sin password + acceso ERP + invitación + email (body: email, firstName, lastName)

# Invitaciones de primer acceso (onboarding del profesional)
GET    /api/invitations/validate?token=       # Validar token (público, no consume)
POST   /api/invitations/accept                # Establecer password y marcar usada (público: token, password)
POST   /api/invitations/{id}/resend           # Reenviar (revoca la pendiente) [RequirePermission("Users.Update")]
POST   /api/invitations/{id}/revoke           # Revocar [RequirePermission("Users.Update")]
```

**Permisos seedeados** (59 total): `Users.*`, `Roles.*`, `Permissions.*`, `Agents.*`, `Organizations.*`, `Clinics.*`, `Locations.*`, `Employees.*`, `Professionals.*`, `Patients.*`, `Documents.*`, `ClinicalRecords.*`, `Telemedicine.*` (incluye `Telemedicine.AdminView` para listados admin globales). Roles: `Admin` (global, todos los permisos) + `OrganizationAdmin`, `ClinicAdmin`, `ClinicalDirector`, `Physician`, `Nutritionist`, `Psychologist`, `Nurse`, `Receptionist`, `CareCoordinator` (asignables con scope de clínica/org).

**Credenciales admin**: `admin@coppaddresd.com` / `Test@1234` (configurable en `appsettings.json` → `Auth` section).

**Cookie de refresh**: `copp_refresh_token` — HttpOnly, `SameSite=Lax`, `Secure` solo fuera de Development, `Path=/api/auth`, `rememberMe=true` → 7 días / `false` → 8 h. El access token (15 min) va en header Bearer, nunca en cookie.

**CORS**: whitelist configurable en `Cors:Origins` (default `http://localhost:3000`) con `AllowCredentials` y expone `X-Refresh-Status`. La API principal usa la misma whitelist pero sin credentials (solo Bearer).

## Chat/AI Integration — Endpoints

```
POST   /api/v1/chat                 # Chat síncrono (vía MediatR → AI Service) [Authorize]
POST   /api/v1/chat/stream          # Chat streaming SSE (text/event-stream) [Authorize]
GET    /api/v1/agents/executions    # Ejecuciones de agentes (monitoreo, proxy del AI Service)
GET    /api/v1/agents/executions/{id}  # Detalle de una ejecución (12 preguntas del monitoreo)
```

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

## Audit System

**Trigger-based**: PostgreSQL trigger automático en INSERT/UPDATE/DELETE.

**Actor propagation**: EF Core interceptor usa GUC variables (`audit.actor_type`, `audit.user_id`, etc.) con `set_config(..., true)` (transactional). Previene leaks en connection pooling.

**Tests**: 7 integration tests passing (requiere PostgreSQL real con variable `COP_TEST_DB_CONNECTION`).

## Gotchas

- **`appsettings.json` / `appsettings.*.json` están gitignoreados** (`src/CoppAddresd.Api`, `src/Services/CoppAddresd.Auth` y `src/Services/CoppAddresd.Telemedicine`). Deben crearse localmente antes de correr. Hay `appsettings.Example.json` solo en Auth.
- **Storage de objetos**: `Storage:Provider` elige `Local` (filesystem, dev) o `S3` (AWS, prod). Con S3 el `upload-intent`/`download` devuelven presigned URLs reales del bucket `cooppadresd-storage-prod` (región `us-east-2`); las credenciales salen de la cadena por defecto del SDK (IAM role), nunca de Access Keys. Config en `appsettings` + fallback a variables `AWS_REGION`/`AWS_S3_*`. Detalle en `docs/modules/storage/README.md`. Si `Storage:Provider=S3` sin credenciales AWS configuradas, la primera operación de storage fallará con error de credenciales del SDK (fail fast en uso).
- **JWT debe ser idéntico** entre API y Auth Service (mismo Secret, Issuer, Audience) para que los tokens funcionen.
- **Auth Service corre migraciones + seeders automáticamente** al iniciar (Program.cs).
- **Telemedicine NO corre migraciones al iniciar** (a diferencia de Auth): aplicar con `dotnet ef database update --project src/Services/CoppAddresd.Telemedicine --startup-project src/Services/CoppAddresd.Telemedicine`. Para nuevas migraciones usar siempre `--output-dir Infrastructure/Migrations` (`MigrationsDirectory` del csproj no se honra). Gotchas: la exclusión GiST requiere extensión `btree_gist` (la crea la migración); `TwilioClient.Init(apiKeySid, apiKeySecret, accountSid)` — el orden es (username, password, accountSid); NO usar `SetRegion` con Twilio Video. Detalle completo: `docs/modules/telemedicine/README.md`.
- **`HttpAuditActorContext`** actualmente retorna `ActorType=System`, `UserId=null` — no hay integración con Identity todavía.
- **Tests de integración** requieren PostgreSQL real (no InMemory). Configurar variable `COP_TEST_DB_CONNECTION`.
- **`AiServiceClient` mapea el contrato del AI Service** (`answer`/`thread_id`/`execution_id`) con `JsonPropertyName` — si el AI Service cambia el schema, ajustar `ChatResponseJson`.
- **Comentarios/docs en español** por convención del README.

## Project skills & docs (MANDATORIO antes de trabajo sustancial)

- **Skills de estándares** viven en `.agents/skills/` (architecture, entity-framework, linq, pagination, query-performance, database-indexes, database-normalization, database, concurrency, cancellation-token, transactions, n-plus-one, production-performance, aws-production, api-design, repository-pattern, service-layer, error-handling, logging, caching, testing, migrations, documentation, security, dotnet). Cargar `architecture` PRIMERO, luego los que apliquen al módulo — codifican reglas de producción/PostgreSQL/concurrencia (N+1, CancellationToken propagation, keyset pagination, index documentation, transaction hygiene, secrets policy).
- `docs/` — docs por módulo + database/performance/AWS en español; actualizar el doc del módulo en la misma tarea que cambia código. Docs y skills están en español (convención del repo).
- Skills estándar faltantes (csharp-*, dotnet-*, aspnet-core) también existen en `.agents/skills/`.

## Sources of truth

- `README.md` — arquitectura completa, versiones del stack (MediatR 14.2.0, FluentValidation 12.1.1, EF 10.0.10/Npgsql 10.0.3), puertos. Mayormente preciso para el estado actual.

<!-- CODEGRAPH_START -->
## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code:

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.

If there is no `.codegraph/` directory, skip CodeGraph entirely — indexing is the user's decision.
<!-- CODEGRAPH_END -->
