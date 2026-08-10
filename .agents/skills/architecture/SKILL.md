---
name: architecture
description: 'Arquitectura del proyecto CoppAddresd (Clean Architecture, .NET 10). Leer SIEMPRE antes de cualquier trabajo sustancial.'
---

# Arquitectura de CoppAddresd Backend

Clean Architecture, .NET 10, Minimal APIs, CQRS (MediatR). Estado actual: **esqueleto** — solo plantilla ASP.NET, módulos de negocio sin implementar.

## Capas y dependencias

Dependencia estrictamente hacia adentro, forzada por ProjectReferences en csproj (no agregar referencias hacia afuera):

| Capa | Contenido | Depende de |
|---|---|---|
| `src/CoppAddresd.Domain` | Entidades, ValueObjects, enums, excepciones de dominio | — |
| `src/CoppAddresd.Application` | Handlers MediatR (`Features/`), validadores FluentValidation, DTOs, interfaces | Domain |
| `src/CoppAddresd.Infrastructure` | EF Core, Npgsql, Identity, JWT, repositorios, servicios externos | Domain, Application |
| `src/CoppAddresd.Api` | Endpoints HTTP, middleware, DI, OpenAPI | Application, Infrastructure |
| `src/Services/CoppAddresd.Auth` | **Servicio standalone**: sin ProjectReferences. Solo paquetes (JwtBearer, Identity EF, Npgsql). Hogar de Identity + JWT | nada |

## Dependencias prohibidas

- Domain → Application/Infrastructure/Api: nunca.
- Application → Infrastructure: nunca (usa interfaces propias).
- Infrastructure → Api: nunca.
- Auth → cualquier proyecto del repo: nunca (autónomo).

## Flujo correcto de una petición (target)

```
HTTP → Endpoint (Api) → MediatR (Application) → Handler
     → valida (FluentValidation pipeline behavior)
     → usa interfaces → implementaciones (Infrastructure: repos/repositorios EF)
     → Domain para reglas de negocio
     → PostgreSQL
```

## Dónde vive cada cosa

- **Entities**: `Domain/Entities/` — sin dependencias externas, reglas de dominio adentro.
- **ValueObjects**: `Domain/ValueObjects/`.
- **Enums**: `Domain/Enums/`.
- **DTOs**: `Application/DTOs/` (o junto al feature).
- **Commands/Queries/Handlers**: `Application/Features/<Modulo>/` (`Commands/`, `Queries/`, `Handlers/`).
- **Validators**: `Application/Features/<Modulo>/Validators/` o `Behaviors/`.
- **Repositories**: interfaces en `Application/Interfaces/`, implementaciones EF en `Infrastructure/Persistence/`.
- **DbContext**: `Infrastructure/Persistence/`.
- **Configuraciones EF**: `Infrastructure/Configurations/` (IEntityTypeConfiguration).
- **Mappings**: `Application/Mappings/`.
- **Middleware**: `Api/Middleware/`.
- **Servicios de infraestructura**: `Infrastructure/Services/`.

## Reglas anti-acoplamiento

- **Sin lógica de negocio en endpoints**: solo orquestar, mapear, validar input, devolver status codes.
- **Sin lógica de negocio en repositorios**: solo persistencia; composición de queries en Application.
- **Domain nunca toca infraestructura** (no EF, no ILogger, no HttpClient).
- Mappers: manuales o library (AutoMapper si se agrega) — nunca lógica en el mapeo.
- Excepciones de dominio → mapeadas a HTTP en Api, no propagadas crudas.

## Convenciones

- Comentarios y docs en **español**.
- C# moderno: records para DTOs, `Nullable` + `ImplicitUsings` habilitados (ya en csproj).
- Carpetas pre-creadas con `.gitkeep` — usarlas, no crear paralelas.
- Solución en `CoppAddresd.slnx` (no existe `.sln`).

## SOLID / DRY / KISS

- SOLID aplicado vía capas: SRP (cada capa un motivo), DIP (Application define interfaces, Infrastructure implementa).
- DRY para lógica de dominio/validación compartida; no abstraer prematuremente.
- KISS: solución simple que escala > complejidad innecesaria.

## Cómo trabajar en este repo (regla fundamental)

1. Leer esta skill + skills aplicables (`entity-framework`, `linq`, `query-performance`, `cancellation-token`, etc.).
2. Leer docs del módulo en `docs/modules/`.
3. Analizar código existente y dependencias.
4. Evaluar impacto: BD, rendimiento, concurrencia, N+1, CancellationToken, índices, transacciones.
5. Implementar → probar (`dotnet test`) → revisar → actualizar docs del módulo.
6. **Nunca modificar código sin entender el flujo existente.**

Advertencia esqueleto: `Program.cs` de Api y Auth son plantilla (`/weatherforecast`); `Class1.cs` son placeholders; tests no referencian proyectos src — se les debe agregar referencia al escribir tests reales.
