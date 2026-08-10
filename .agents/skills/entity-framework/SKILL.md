---
name: entity-framework
description: 'Uso correcto de EF Core 10 + Npgsql en CoppAddresd: DbContext, configuraciones, consultas, migraciones, tracking.'
---

# Entity Framework Core — Reglas del proyecto

Stack: EF Core 10.0.10 + Npgsql 10.0.3 + PostgreSQL 16. DbContext aún NO creado (esqueleto) — vivirá en `Infrastructure/Persistence/`.

## DbContext y configuraciones

- DbContext en `Infrastructure/Persistence/`; configuraciones por entidad en `Infrastructure/Configurations/` (implementar `IEntityTypeConfiguration<T>`, registrarlas en `OnModelCreating`).
- Scoped lifetime por request. Nunca singleton/shared.
- Npgsql: mapear `Guid` → `uuid`, `DateTimeOffset` → `timestamptz`, `decimal` → `numeric` con precisión/scale explícita (p. ej. `HasPrecision(18, 4)`), `string` → `text` o `varchar(n)` con `HasMaxLength`.
- Enums de dominio: preferir tabla/string legible; `HasConversion<string>()` por defecto salvo razón de rendimiento.
- Nombres de tabla/columna en snake_case (convención PostgreSQL): `ToTable("users")`, `Property(x => x.CreatedAt).HasColumnName("created_at")`.

## Reglas de consulta

- **Siempre async**: `ToListAsync`, `FirstOrDefaultAsync`, `SingleOrDefaultAsync`, `AnyAsync`, `CountAsync`, `ExecuteDeleteAsync`... nunca las variantes síncronas.
- **Siempre con CancellationToken** (ver skill `cancellation-token`).
- Lectura sin mutación → `AsNoTracking()`.
- Leer solo lo necesario → proyectar a DTO (`Select`) en lugar de cargar entidades completas.
- Nada de `.Include` innecesarios; evaluar proyección/join explícito/split query.
- `AsSplitQuery()` cuando un `Include` de colecciones multiplica filas (evaluar N+1 vs join cartesiano).
- Sin evaluación cliente: todo lo posible debe traducirse a SQL. Detectar con logging de sentencias SQL.
- Orden de composición: `AsNoTracking()` → filtros → orden → paginación → proyección → materializar (`.ToListAsync`) **al final**.

## Migraciones

- `dotnet ef migrations add <Nombre> --project src/CoppAddresd.Infrastructure --startup-project src/CoppAddresd.Api`
- Revisar el SQL generado ANTES de aplicarlo (ver skill `migrations`).
- Nunca borrar migraciones aplicadas a producción.

## Anti-patrones prohibidos

- `.ToList()` antes de filtrar/proyectar.
- Consultas dentro de loops (N+1).
- Lazy loading (`ILazyLoader`, proxies) — no habilitar.
- Actualizar entidades cargadas solo para cambiar 1 campo: preferir updates dirigidos o `ExecuteUpdateAsync`.
- Bloquear el hilo: `.Result`/`.Wait()` sobre EF.

## Configuración de logging de SQL (dev)

En Development: `optionsBuilder.LogTo(...)` o `EnableSensitiveDataLogging` solo en dev; en producción usar logging estructurado + métricas de duración de query.
