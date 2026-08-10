# Arquitectura — CoppAddresd Backend

Estado: **esqueleto inicial** (solo plantilla ASP.NET, módulos sin implementar). Este documento define el target arquitectónico.

## Mapa completo

```
┌────────────────────────────────────────────────┐
│  Presentation                                  │
│  CoppAddresd.Api (Minimal APIs, middleware)    │
│  CoppAddresd.Auth (standalone: Identity + JWT) │
└──────────────────────┬─────────────────────────┘
┌──────────────────────▼─────────────────────────┐
│  Infrastructure                                │
│  EF Core + Npgsql, repositorios, servicios AWS │
└──────────────────────┬─────────────────────────┘
┌──────────────────────▼─────────────────────────┐
│  Application                                   │
│  MediatR (Features/), FluentValidation, DTOs,  │
│  interfaces (DIP)                              │
└──────────────────────┬─────────────────────────┘
┌──────────────────────▼─────────────────────────┐
│  Domain                                        │
│  Entities, ValueObjects, Enums, excepciones    │
└────────────────────────────────────────────────┘
```

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

_(Registro incremental: cada decisión relevante se añade aquí con contexto, decisión y consecuencias.)_

| Fecha | Decisión | Contexto |
|---|---|---|
| — | Pending | Esqueleto: sin decisiones de negocio aún |

## Deuda técnica conocida / pendientes del esqueleto

- `Program.cs` de Api y Auth: código de plantilla (`/weatherforecast`) a reemplazar por endpoints reales + DI + middleware.
- Sin DbContext, sin migraciones, sin excepciones propias, sin middleware de errores ni correlation id.
- Tests sin referencias a proyectos src (stubs).
- Sin plantilla `appsettings.Example.json` versionada (el patrón `appsettings.*.json` del .gitignore la ignoraría — requeriría negación).
- README menciona config keys (ConnectionStrings, Jwt) que ningún código lee todavía.
- Detalle concreto por problema priorizado: ver entregable de análisis (ordenar por severidad).
