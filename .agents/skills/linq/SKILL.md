---
name: linq
description: 'LINQ orientado a producción: IQueryable vs IEnumerable, ejecución diferida, dónde ocurre la ejecución, reglas para no romper la traducción SQL.'
---

# LINQ — Reglas del proyecto

El objetivo: que la consulta se ejecute en PostgreSQL, no en memoria del servidor.

## IQueryable vs IEnumerable vs List — cuándo es SQL y cuándo memoria

| Tipo | Origen | Ejecución | Uso |
|---|---|---|---|
| `IQueryable<T>` | EF Core | **En SQL**, diferida | Componer consultas: filtros, orden, paginación, proyección |
| `IEnumerable<T>` | Colecciones en memoria | En memoria | Datos ya materializados; streams |
| `List<T>` | `ToList()` | En memoria, materializado | Punto final de datos para retornar/cachear |

- `IQueryable` NO ejecuta nada hasta que se materializa (`.ToListAsync`, `.FirstAsync`, `.AnyAsync`, etc.) o se enumera.
- **Una vez materializado, todo lo que se encadene corre en memoria.** Prohibido: `ToList()` temprano y luego `.Where(...).Select(...)`.

## Composición correcta

```csharp
IQueryable<Entity> query = db.Entities
    .AsNoTracking();

query = ApplyFilters(query);          // filtros
query = ApplySorting(query);          // orden
query = ApplyPagination(query);       // page/pageSize

var result = await query
    .Select(...)                      // proyección a DTO
    .ToListAsync(cancellationToken);  // materializar al final
```

## Reglas obligatorias

- Filtros/orden/paginación **antes** de materializar.
- Proyectar a DTO con `Select` en vez de traer entidades completas.
- `AsNoTracking()` en toda lectura sin mutación.
- Nunca enumerar el mismo `IQueryable` dos veces (reejecución); materializar una vez.
- `FirstOrDefaultAsync`/`SingleOrDefaultAsync` en vez de `ToList`+indexación.
- `CountAsync`/`AnyAsync` para conteos/existencia — nunca `.Count()` sobre una lista completa.
- Evitar métodos que no traducen a SQL en `Where`/`OrderBy` (funciones propias, indexadores, lógica cliente): usan LINQ-to-Objects y cargan todo.

## Operadores y traducción (EF Core + Npgsql)

- Bien: `Where`, `OrderBy`, `Skip`/`Take`, `Select`, `Join`, `GroupBy` (con límites), `Any`, `All`, `Contains` (→ `IN`), `Distinct`, `First`/`FirstOrDefault`, `Count`/`CountAsync`, `Sum`/`Min`/`Max`/`Average`, `String.StartsWith/Contains` (→ `LIKE`), `EF.Functions.ILike` (case-insensitive).
- Cuidado: `GroupBy` compuestos, `Take` en grupos (se requiere ventana), conversiones de tipos, `DateTime` vs `timestamptz` (usar `DateTimeOffset`/UTC).
- No usar funciones CLR arbitrarias dentro de proyecciones/filtros salvo que EF las traduzca.

## Evaluación previa a escribir la consulta

Complejidad → volúmenes → índices existentes → SQL resultante → memoria → latencia → concurrencia → escalabilidad. Si una consulta no escala a millones de filas, rediseñarla antes de escribirla.

## Detección de problemas (dev)

- Logging de SQL para ver las sentencias emitidas (ver skill `query-performance`).
- Buscar `ToList()` prematuros, `.Where` después de `.ToList`, loops con queries adentro.
