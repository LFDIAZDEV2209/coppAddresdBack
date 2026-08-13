# CoppAddresd Backend — Agent Guide

.NET 10 / C# 13 backend, Clean Architecture. **Estado actual**: Auth Service completo, integración AI Chat, sistema de auditoría PostgreSQL.

## Commands

```bash
dotnet build                                  # build solution (usa CoppAddresd.slnx)
dotnet test                                   # all tests (xUnit)
dotnet test tests/CoppAddresd.UnitTests       # unit tests only
dotnet test --filter "FullyQualifiedName~X"   # single test
dotnet run --project src/CoppAddresd.Api      # main API (http://localhost:5122, https 7258)
dotnet run --project src/Services/CoppAddresd.Auth  # auth service (http://localhost:5058, https 7230)
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

`CoppAddresd.slnx` es el nuevo formato XML de soluciones — `.sln` plano no existe. Herramientas esperando `.sln` fallarán.

## Database schema organization

```sql
Schema public:  __EFMigrationsHistory (solo)
Schema auth:    12 tablas (Users, Roles, Permissions, RefreshTokens, etc.)
Schema audit:   1 tabla (activity_logs)
```

**Regla**: Cada módulo tiene su schema. Auth usa `auth.`, auditoría usa `audit.`, negocio irá en `public.`.

## Auth Service — Endpoints y permisos

```
POST   /api/auth/login              # Login (email + password)
POST   /api/auth/refresh            # Refresh token (rotación automática)
POST   /api/auth/logout             # Logout (revoca refresh tokens) [Authorize]
POST   /api/auth/change-password    # Cambiar password [Authorize]

GET    /api/me                      # Info del usuario actual + permisos [Authorize]

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
```

**Permisos seedeados** (15 total): `Users.View/Create/Update/Delete`, `Roles.View/Create/Update/Delete/Assign`, `Permissions.View/Assign`, `Agents.View/Create/Update/Delete`.

**Credenciales admin**: `admin@coppaddresd.com` / `Test@1234` (configurable en `appsettings.json` → `Auth` section).

## Chat/AI Integration — Endpoints

```
POST   /api/v1/chat                 # Chat síncrono (vía MediatR → AI Service) [Authorize]
POST   /api/v1/chat/stream          # Chat streaming SSE (text/event-stream) [Authorize]
```

**Configuración**: `appsettings.json` → `AiService` section (BaseUrl, ApiPrefix, TimeoutSeconds).

**Resilience**: Polly retry (3 intentos, backoff exponencial) + circuit breaker (5 fallos, 30s break).

## Audit System

**Trigger-based**: PostgreSQL trigger automático en INSERT/UPDATE/DELETE.

**Actor propagation**: EF Core interceptor usa GUC variables (`audit.actor_type`, `audit.user_id`, etc.) con `set_config(..., true)` (transactional). Previene leaks en connection pooling.

**Tests**: 7 integration tests passing (requiere PostgreSQL real con variable `COP_TEST_DB_CONNECTION`).

## Gotchas

- **`appsettings.json` / `appsettings.*.json` están gitignoreados** (`src/CoppAddresd.Api` y `src/Services/CoppAddresd.Auth`). Deben crearse localmente antes de correr. Hay `appsettings.Example.json` solo en Auth.
- **JWT debe ser idéntico** entre API y Auth Service (mismo Secret, Issuer, Audience) para que los tokens funcionen.
- **Auth Service corre migraciones + seeders automáticamente** al iniciar (Program.cs).
- **`HttpAuditActorContext`** actualmente retorna `ActorType=System`, `UserId=null` — no hay integración con Identity todavía.
- **Tests de integración** requieren PostgreSQL real (no InMemory). Configurar variable `COP_TEST_DB_CONNECTION`.
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
