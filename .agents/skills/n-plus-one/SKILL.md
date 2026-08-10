---
name: n-plus-one
description: 'Detección y eliminación de consultas N+1. Patrón prohibido en el proyecto: queries dentro de loops.'
---

# N+1 Queries — Reglas del proyecto

El patrón N+1 es un defecto de rendimiento: 1 query + N queries adicionales. En producción (miles de filas, alta concurrencia) tumba la base de datos.

## Patrones a detectar (prohibidos)

```csharp
// Prohibido: query por cada elemento
foreach (var item in items)
{
    var detalle = await repository.GetDetalleAsync(item.Id);   // N queries
}

// Prohibido: navegación materializada
var users = await GetUsers();
foreach (var user in users)
{
    var orders = await GetOrders(user.Id);                     // N queries
}

// Prohibido: lazy/proxy navigation o referenciar navegaciones no cargadas
```

## Señales de alerta en código

- Bucles (`foreach`/`for`) conteniendo `await` de repositorio/EF/HttpClient.
- Navegaciones de EF referenciadas fuera de la query raíz sin `Include`.
- Repositorio consultado múltiples veces con el mismo id dentro de un request.
- `.First()`/`.FirstOrDefault()` dentro de un `Select` de otra query.

## Alternativas (en orden de preferencia)

1. **Proyección (Select a DTO)** — una sola query, solo columnas necesarias:

```csharp
var result = await db.Users.AsNoTracking()
    .Where(u => u.TenantId == tenant)
    .Select(u => new UserWithOrderCountDto(
        u.Id, u.Name,
        u.Orders.Count()))
    .ToListAsync(ct);
```

2. **Include/ThenInclude** — cuando realmente se necesitan entidades completas relacionadas. Cuidado con productos cartesianos: `AsSplitQuery()` para colecciones múltiples.
3. **Join explícito** con proyección.
4. **Batch query** — cargar los N ids de una vez (`Contains(idList)` → `IN`) en lugar de N queries.

```csharp
var ids = items.Select(i => i.Id).ToList();
var detalle = await db.Detalles.AsNoTracking()
    .Where(d => ids.Contains(d.ItemId))
    .ToListAsync(ct);                                  // 1 query
```

5. **Grouping / agregaciones** — para contadores/sumas por padre.
6. **Bulk operations** — `ExecuteUpdateAsync`/`ExecuteDeleteAsync` para mutaciones masivas (no cargar y loop-escuchar).

## Verificación

- En dev: revisar el logging de SQL — un request debe emitir pocas sentencias, no N+1.
- Regla simple: si un request emite más de ~5-10 queries para un listado paginado, sospechar N+1 (salvo composición justificada).
- Tests de regresión: contar queries por request si la librería lo permite, o assertar contra una BD de pruebas.

## Documentación

Queries agregadas/complejas documentadas en `docs/performance/n-plus-one.md` con el patrón usado y la alternativa evitada.
