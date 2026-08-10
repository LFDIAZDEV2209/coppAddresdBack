---
name: testing
description: 'Testing en CoppAddresd: xUnit, unit vs integration, qué cubrir (paginación, concurrencia, cancelación, transacciones, errores, volúmenes).'
---

# Testing — Reglas del proyecto

xUnit. Estado actual: los proyectos de test NO referencian proyectos src (stubs de plantilla) — **agregar ProjectReferences al escribir tests reales**.

## Pirámide

| Nivel | Qué | Tecnología |
|---|---|---|
| Unit | Handlers MediatR, validadores, lógica de dominio, servicios (mocks) | xUnit + mocks (NSubstitute/Moq a decidir) |
| Integration | Repositorios + EF + PostgreSQL real, flujos completos | `WebApplicationFactory` (Api) + PostgreSQL real (Testcontainers) |
| API | Endpoints end-to-end (status, contratos, auth) | `WebApplicationFactory` + HttpClient |

- No probar EF Core en unit (mockear interfaces). Probar EF real en integration contra PostgreSQL de Testcontainers (Npgsql), no InMemory (no traduce SQL igual).
- Nombrar: `ArchivoPruebas` → `OrderHandlerTests.cs`; método: `Handle_OrderNotFound_ThrowsNotFoundException` (patrón Arrange/Act/Assert, nombre `Método_Estado_Expectativa`).

## Qué cubrir (obligatorio por área)

- **Paginación**: primera/última página, página vacía, límite `pageSize` (>100 rechazado), orden estable con empates, keyset con inserciones entre páginas.
- **Queries**: filtros, orden, proyección correcta, `AsNoTracking`, SQL esperado (mínimo: resultado correcto contra BD real).
- **Autorización**: endpoint sin token → 401; sin permiso → 403; con permiso → 200.
- **Concurrencia**: dos escrituras simultáneas al mismo recurso (optimista → 409 en una), violación de unique, retry por deadlock/serialización si aplica.
- **Cancelación**: token cancelado → operación aborta (`OperationCanceledException`) y no deja estado a medias; request con token → `ToListAsync(ct)` lo respeta.
- **Transacciones**: fallo en paso 2 deshace paso 1 (rollback), commit solo al final, `await using` limpio.
- **Errores**: middleware traduce excepción de dominio → status correcto + ProblemDetails; desconocida → 500 genérico sin stack.
- **Grandes volúmenes**: seed de miles de filas → consultas paginadas/indexadas cumplen tiempo (test de rendimiento opcional, no en suite normal).
- **Idempotencia**: doble POST del mismo request no duplica estado (si el endpoint lo garantiza).

## Comandos

```bash
dotnet test                                    # todo
dotnet test tests/CoppAddresd.UnitTests        # solo unit
dotnet test tests/CoppAddresd.IntegrationTests # solo integración
dotnet test --filter "FullyQualifiedName~Paginacion"
```

## Organización

- `tests/CoppAddresd.UnitTests/` → unit (referenciar Application + Domain).
- `tests/CoppAddresd.IntegrationTests/` → integración (referenciar Api + Infrastructure; `WebApplicationFactory<Program>` con entorno `Testing`, BD PostgreSQL de Testcontainers).
- Fixtures: `TestDb` (spins container), `TestDataFactory` para seeds.
- Regla de oro: **suite debe pasar en `dotnet test` limpio** (sin estado compartido, sin dependencias externas no contenedores).

## Anti-patrones

- `Thread.Sleep` para esperar (usar `WaitAsync` con timeout/token).
- Tests que dependen del orden de ejecución o estado global.
- Assert sobre SQL generado frágil (strings) — usar resultados.
- Probar implementación (privados) en vez de comportamiento.
- Ignorar tests sin motivo documentado.
