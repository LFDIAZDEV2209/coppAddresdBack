---
name: query-performance
description: 'Rendimiento de consultas: cómo analizar, medir y validar queries SQL emitidas por EF Core antes de pasar a producción.'
---

# Rendimiento de Queries — Reglas del proyecto

Toda consulta importante se valida como se valida en producción: **se mide, no se adivina**.

## Proceso de validación de una consulta

1. Ver el SQL que emite EF Core (ver abajo).
2. Ejecutarlo con `EXPLAIN (ANALYZE, BUFFERS)` en PostgreSQL (pgAdmin / psql).
3. Verificar: ¿usa índice? ¿Seq Scan evitable? ¿filtros por columna indexada? ¿rows estimadas ≈ reales?
4. Medir latencia en dev con volumen de datos representativo (miles → millones).
5. Documentar resultado en `docs/performance/` o en el README del módulo.

## Habilitar logging de SQL en Development (temporal o global dev)

En `Program.cs` / DbContext de dev:

```csharp
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(conn)
     .LogTo(Console.WriteLine, LogLevel.Information, DbContextLoggerCategory.Database.Command)
     .EnableSensitiveDataLogging()); // SOLO Development, nunca producción
```

En producción: logging estructurado + duración de query (no parámetros).

## Anti-patrones de rendimiento (revisar en cada diff)

- N+1: loops con queries adentro (skill `n-plus-one`).
- `ToList()` temprano + filtrado en memoria (skill `linq`).
- Cargar entidades completas cuando solo se necesitan 2 columnas (usar `Select`).
- `Count`/`Any` sobre colecciones ya materializadas.
- `Include` de colecciones sin `AsSplitQuery` cuando el producto cartesiano explote.
- Paginación offset sobre tablas de cientos de miles de filas (skill `pagination`).
- Consultas duplicadas en el mismo request (idéntico SQL repetido → cachear el resultado o reutilizar la query).
- `First()`/`Single()` repetidos dentro del mismo scope — resolver una sola vez.

## Reglas de composición

- Filtros → orden → paginación → proyección → materializar (ver skill `linq`).
- Filtros por columnas indexadas; si el filtro no está indexado, es decisión con índice o diseño de query (skill `database-indexes`).
- Las operaciones que no necesitan traer datos (`UPDATE`/`DELETE`/`Count`) usan `ExecuteUpdateAsync`/`ExecuteDeleteAsync`/`CountAsync` — nunca cargar entidades para mutarlas en memoria.

## Presupuesto de coste por request (orientativo)

- Total de queries por request: lo mínimo posible; lecturas repetibles del mismo dato → 1.
- Latencia objetivo p95 < 100ms en queries simples; complejas < 500ms. Si no cumple, optimizar (índice, proyección, cache — en ese orden de preferencia).

## Escala de evaluación (aplicar a cada consulta importante)

```text
10 usuarios | 1.000 usuarios | 100k filas | millones de filas | 100 req concurrentes | 1.000 req concurrentes
```

Si la consulta falla el escenario de millones de filas, rediseñarla. Si el problema es de caché/estadística, actualizar `ANALYZE`/`VACUUM`; si es de diseño, arreglar el modelo.
