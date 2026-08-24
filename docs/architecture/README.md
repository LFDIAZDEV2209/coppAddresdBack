# Arquitectura — CoppAddresd Backend

Estado: **implementación activa**. Auth Service completo, integración AI Chat, sistema de auditoría PostgreSQL.

## Mapa completo

```
┌────────────────────────────────────────────────────────────────┐
│  Presentation                                                  │
│  CoppAddresd.Api (Controllers, JWT auth, SSE streaming)        │
│  CoppAddresd.Auth (Identity + JWT + permisos granulares)       │
└──────────────────────────────┬─────────────────────────────────┘
┌──────────────────────────────▼─────────────────────────────────┐
│  Infrastructure                                                │
│  EF Core + Npgsql, AppDbContext (audit), AiServiceClient,      │
│  AuditTriggerInterceptor, Polly resilience                     │
└──────────────────────────────┬─────────────────────────────────┘
┌──────────────────────────────▼─────────────────────────────────┐
│  Application                                                   │
│  MediatR (Features/Chat), FluentValidation, DTOs,              │
│  interfaces (IAiServiceClient, IAuditActorContext)             │
└──────────────────────────────┬─────────────────────────────────┘
┌──────────────────────────────▼─────────────────────────────────┐
│  Domain                                                        │
│  ActivityLog, AuditAction, AuditActorType                      │
└────────────────────────────────────────────────────────────────┘
```

## Database schema organization

```sql
Schema public:  __EFMigrationsHistory (solo)
Schema auth:    12 tablas — Users, Roles, Permissions, RefreshTokens, etc.
Schema audit:   1 tabla — activity_logs (trigger-based)
```

**Regla**: Cada módulo tiene su propio schema. Auth usa `auth.`, auditoría usa `audit.`, negocio irá en `public.`.

## Dependencias (verificadas en csproj)

| Proyecto | Referencia a |
|---|---|
| `CoppAddresd.Domain` | — |
| `CoppAddresd.Application` | Domain |
| `CoppAddresd.Infrastructure` | Domain, Application |
| `CoppAddresd.Api` | Application, Infrastructure |
| `CoppAddresd.Auth` | — (solo NuGet: JwtBearer, Identity EF, Npgsql, OpenApi, EF Design) |

Regla: la dependencia fluye hacia adentro. No agregar referencias hacia afuera.

## Responsabilidades

- **Domain**: entidades con reglas de negocio, value objects, enums, excepciones de dominio. Sin dependencias externas.
- **Application**: casos de uso CQRS (Commands/Queries/Handlers), validación FluentValidation, DTOs, interfaces de infraestructura (contratos). Orquesta, no implementa infraestructura.
- **Infrastructure**: EF Core + PostgreSQL, implementaciones de repositorios, Identity/JWT (también en Auth), servicios externos (S3, SQS, Secrets).
- **Api**: endpoints HTTP delgados, middleware (excepciones, correlation id, auth), DI, OpenAPI.
- **Auth**: servicio web independiente para identidad y emisión/validación de tokens.

## Flujo de una petición (target)

```
GET /api/v1/orders?page=1&pageSize=20
→ Middleware (correlation id, excepciones, auth JWT)
→ Endpoint: valida input (FluentValidation vía pipeline MediatR)
→ Query → Handler (Application)
→ Interfaz repositorio (Application/Interfaces)
→ Implementación EF (Infrastructure) → PostgreSQL
→ DTO ← proyección SQL
→ ProblemDetails si error; PagedResult si lista
```

## Ubicación estándar de artefactos

| Artefacto | Carpeta |
|---|---|
| Entities / ValueObjects / Enums / Excepciones | `Domain/Entities|ValueObjects|Enums|Exceptions` |
| Commands / Queries / Handlers / Validators | `Application/Features/<Modulo>/...` |
| Behaviors (pipeline MediatR) | `Application/Behaviors/` |
| DTOs | `Application/DTOs/` |
| Interfaces | `Application/Interfaces/` |
| Mappings | `Application/Mappings/` |
| DbContext | `Infrastructure/Persistence/` |
| Configuraciones EF | `Infrastructure/Configurations/` |
| Repositorios (impl) | `Infrastructure/Persistence/Repositories/` |
| Middleware | `Api/Middleware/` |

## Decisiones de arquitectura (ADR)

| Fecha | Decisión | Contexto |
|---|---|---|
| 2026-08-10 | Schema `auth.` separado | Auth usa schema propio en PostgreSQL para consistencia con `audit.` y evitar contaminación de `public.` cuando crezca el dominio. |
| 2026-08-10 | Permisos granulares (no solo roles) | Sistema de permisos tipo `Users.View`, `Users.Create` etc. Usuario puede tener permisos directos + via rol. Más flexible que solo RBAC. |
| 2026-08-10 | Auth como servicio standalone | Auth no referencia otros proyectos del solution. Puede desplegarse independientemente. |
| 2026-08-10 | SSE para chat streaming | Server-Sent Events sobre HTTP. Compatible con navegadores, simple, unidireccional (suficiente para streaming de LLM). |
| 2026-08-10 | Polly resilience para AI Service | Retry (3 intentos, backoff exponencial) + circuit breaker (5 fallos, 30s). Tolerancia a fallos sin código complejo. |
| 2026-08-10 | Auditoría trigger-based + GUC | Trigger PostgreSQL automático + EF interceptor con `set_config(..., true)` para propagar actor. Zero código en handlers. |
| 2026-08-24 | Gateway YARP standalone (puerto 5080) + plan AWS | Único punto de entrada pública (web/móvil/webhooks); CORS centralizado, internals con `X-Internal-Key`, split SSE en AWS. Detalle completo: `docs/architecture/gateway.md`. |

## Deuda técnica / pendientes

- `HttpAuditActorContext` retorna `ActorType=System`, `UserId=null` — no hay integración con Identity todavía. Los audit logs no capturan quién hizo la acción.
- Tests unitarios (`UnitTest1.cs`) son stubs sin referencias a proyectos src.
- Tests de integración requieren PostgreSQL real (variable `COP_TEST_DB_CONNECTION`).
- `.http` files aún apuntan a `/weatherforecast` — actualizar con endpoints reales.
- CORS configurado como `AllowAll` — restringir en producción.
- `RequireHttpsMetadata = false` — activar en producción.
- Secret de JWT en `appsettings.json` es placeholder — usar secrets manager en producción.
- Sin health check específico para AI Service (solo Polly resilience).
