---
name: repository-pattern
description: 'Repositorios en CoppAddresd: cuándo existen, qué contienen, qué está prohibido. Interfaces en Application, implementaciones EF en Infrastructure.'
---

# Repository Pattern — Reglas del proyecto

Los repositorios existen donde aportan (agregados complejos, queries reutilizables, desacople de EF para tests). **No** es obligatorio un repositorio por entidad.

## Estructura

- **Interfaz** en `Application/Interfaces/` (contrato del caso de uso; DIP).
- **Implementación EF** en `Infrastructure/Persistence/Repositories/`.
- DbContext solo visible en Infrastructure; Application conoce interfaces, nunca `IQueryable` del contexto (ver alternativa abajo).

## Qué contienen

- Operaciones de persistencia de agregados: `GetByIdAsync(id, ct)`, `AddAsync`, `Update`, `Delete` (o `Remove`).
- Queries de dominio necesarias por más de un caso de uso (no duplicar SQL en 3 handlers).
- Firma con `CancellationToken` SIEMPRE (skill `cancellation-token`).

```csharp
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Order>> GetOpenByCustomerAsync(Guid customerId, int page, int pageSize, CancellationToken ct);
    void Add(Order order);
    void Update(Order order);
}
```

## Qué está prohibido

- **Lógica de negocio en repositorios**: validaciones, cálculos de dominio, decisiones. Solo persistencia y acceso a datos.
- `IQueryable<T>` devuelto al Application/endpoint (fuga del contexto, composición libre que dificulta testear y auditar); si el proyecto necesita composición de filtros, exponer métodos tipados con parámetros de query (especificación o DTO de filtro), no `IQueryable`.
- Métodos genéricos inútiles (`GetAll` sin paginación, `GetAllAsync` que devuelve todo): prohibidos — ver `pagination`.
- Consultas ad-hoc duplicadas en cada handler cuando la misma query aparece 2+ veces → centralizar en repositorio o query service.
- Síncronos: nunca `.ToList()`, siempre `ToListAsync(ct)`.

## Lecturas vs escrituras

- Leer: `AsNoTracking()`, proyección a DTO, paginación (skills `linq`, `pagination`, `entity-framework`).
- Escribir: unidades de trabajo vía DbContext; repositorio no controla transacciones — el handler decide (skill `transactions`).
- Para reportes/agregaciones complejas: Query Services dedicados (lectura pura) en Infrastructure, expuestos por interfaz en Application — no forzarlos dentro del repositorio del agregado.

## Testing

- Unit: mockear la interfaz (Mock de repositorio).
- Integration: implementación real contra PostgreSQL de test (Testcontainers) — ver skill `testing`.
- Tests de repositorio validan: paginación, orden, filtros, cancelación, transacciones del handler.

## Anti-patrones

- GenericRepository<T> hinchado con `GetAll`/`Find(x => ...)` sin límites.
- Repositorio por cada tabla sin motivo (navegar por EF directamente cuando el caso es simple es válido si se mantiene la regla de capas).
- Devuelve entidades cuando el cliente solo necesita un DTO.
- Repositorio con `SaveChanges` propio (el DbContext/UoW lo gestiona en el handler).
