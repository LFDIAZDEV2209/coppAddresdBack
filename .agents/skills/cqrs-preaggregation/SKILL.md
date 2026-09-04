---
name: cqrs-preaggregation
description: "Trigger: dashboard analytics, nueva métrica a contar, KPI, pre-agregación, rollup, contadores. Evaluate new dashboard metrics against the CQRS pre-aggregation model and implement the pipeline so data lands in rollup tables automatically."
license: Apache-2.0
metadata:
  author: "coppaddresd"
  version: "1.0"
---

# CQRS Pre-aggregation (Channel Pattern) — coppAddresdBack

Contrato para que cualquier métrica nueva de dashboard se guarde automáticamente en las tablas rollup (pre-agregadas por día, alimentadas en background), en lugar de contar OLTP en cada request. El patrón ya está implementado en 6 módulos (Patients, ProgramProgress, Telemedicine, HealthTests, Community, Inventory); esta skill garantiza que las métricas nuevas sigan el mismo pipeline.

## Activation Contract

Cargar cuando una tarea en `coppAddresdBack` **agregue un dato nuevo a contar** en algún dashboard/analítica (KPI, serie temporal, contador, distribución por tipo/hora) o modifique agregaciones existentes. Antes de escribir código, evaluar si el dato corresponde al modelo de pre-agregación.

## Decision Gates

| Situación | Acción |
|---|---|
| Conteo/agregación en el tiempo: posts, likes, entradas, unidades, costos, actividad por día/hora/tipo | Pre-agregar (rollup) |
| Estado puntual actual: stock, productos activos, low/out-of-stock, por expirar | Consultar OLTP directo (NUNCA rollup) |
| Detalle por entidad o historial crudo | OLTP (el rollup solo guarda agregados) |
| El módulo ya pre-agrega el mismo write path | Extender, no duplicar |

## Hard Rules

1. **Enqueue SOLO después** de `SaveChangesAsync`/`CreateXAsync` commiteado. Nunca antes.
2. Inyectar `I<Modulo>MetricsQueue? queue = null` (opcional, al final). Guard explícito `if (queue != null)`, NUNCA `?.`.
3. Upserts **acumulan**: `total_count = <tabla>.total_count + EXCLUDED.total_count`. Nunca reemplazan.
4. Decrementos: `GREATEST(0, total_count - 1)`.
5. Dinero en **centavos** (`long`): por línea `Math.Round(costo*cant*100, MidpointRounding.AwayFromZero)`; total del documento = Σ por línea. Lectura `/100m`.
6. Dimension keys ≤64 chars: truncar nombre de producto/categoría/tipo; vacío → `"general"`.
7. Channel **bounded**: `CreateBounded(10_000)` + `DropOldest` + `SingleReader` (precedente del repo).
8. Lectura **rollup-first**; fallback OLTP solo si el rollup está vacío en el rango — el fallback debe devolver LA MISMA semántica que el rollup.
9. Migraciones con historial aislado por contexto (`community.`/`tele.`/`auth.`/`public`). Nunca compartir `public` entre contextos.
10. Tests PostgreSQL-gated (`COP_TEST_DB_CONNECTION`, patrón skip). NUNCA InMemory para SQL crudo.
11. Comentarios/docs en español. Sin atribución AI en commits.

## Execution Steps

1. Ubicar el write path (mutación GraphQL o handler MediatR) que crea la fila OLTP.
2. Copiar la implementación existente más cercana como plantilla (References: Community o HealthTest).
3. Crear: entidad + configuración EF + DbSet; evento(s) de métrica; interfaz + cola; HostedService processor con upserts atómicos.
4. Inyectar la cola opcional en el writer; enqueue tras el save.
5. Lectura fast-path en query/repositorio de analítica; fallback OLTP equivalente.
6. Registrar DI: `AddSingleton<I<Modulo>MetricsQueue, <Modulo>MetricsQueue>()` + `AddHostedService<...ProcessorHostedService>()`.
7. Generar migración (`dotnet ef migrations add` con el contexto del módulo e historial aislado).
8. Escribir 5+ tests PostgreSQL-gated (acumulación, mismo día, piso de decremento, dimension keys, centavos).
9. Actualizar `docs/modules/<modulo>/analytics.md` + ADR en `docs/architecture/README.md`.

## Output Contract

Retornar: archivos creados/modificados, nombre de migración, resumen del upsert SQL, conteos de tests, y confirmación de las reglas 1–11.

## References (leer ANTES de escribir código)

- `coppAddresdBack/docs/architecture/analytics-cqrs-preaggregation.md` — arquitectura del patrón.
- `coppAddresdBack/src/Services/CoppAddresd.Community/Metrics/` — ejemplo completo más limpio (entidad→evento→cola→processor→inyección→query).
- `coppAddresdBack/src/CoppAddresd.Infrastructure/Metrics/HealthTestMetricsProcessorHostedService.cs` — processor del stack principal.
- `coppAddresdBack/docs/modules/inventory/analytics.md` — diseño + convención de centavos.
- `coppAddresdBack/tests/CoppAddresd.UnitTests/ProgramProgress/ProgramRepositoryTests.cs` — patrón skip de `COP_TEST_DB_CONNECTION`.