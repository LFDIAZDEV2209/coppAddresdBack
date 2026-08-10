---
name: migrations
description: 'Migraciones EF Core seguras para producción: revisión del SQL, tablas grandes, índices concurrentes, compatibilidad hacia atrás, rollback.'
---

# Migraciones — Reglas del proyecto

Una migración que pasa localmente puede tumbar producción (locks, tiempo, datos). Cada migración se revisa como si fueran millones de filas y tráfico real.

## Flujo

```bash
dotnet ef migrations add <Nombre> --project src/CoppAddresd.Infrastructure --startup-project src/CoppAddresd.Api
```

1. Generar migración.
2. **Revisar el SQL generado** (archivo `Up`/`Down`) — nunca aplicarla a ciegas.
3. Validar contra `docs/database/` y las reglas de abajo.
4. Probar contra PostgreSQL real con volumen representativo (miles+).
5. Desplegar con estrategia de zero-downtime (abajo).

## Reglas para producción

### Índices sobre tablas grandes
- `CREATE INDEX CONCURRENTLY` (no bloquea escritura). No corre dentro de transacción → la migración se hace sin envolver en transaction (quitar el `transaction: false`? EF: `migrationBuilder` — ejecutar vía SQL bruto `migrationBuilder.Sql("CREATE INDEX CONCURRENTLY ...")` y **no** dentro de transacción; documentar).
- Consecuencia: un índice concurrent fallido deja "invalid" → borrarlo y reintentar.

### ALTER TABLE / NOT NULL
- Agregar columna `NOT NULL` sobre tabla con filas → se bloquea: patrón: agregar nullable → backfill por lotes → `SET NOT NULL` (fuera de pico o con `NOT VALID` si aplica).
- `ADD COLUMN` con DEFAULT constante en PG 11+ es rápido (metadata); DEFAULT volátil (función) no.
- Renombrar/borrar columnas → romper compatibilidad: desplegar app + migración en orden (abajo).

### Cambios destructivos
- `DROP TABLE`/`DROP COLUMN`/cambio de tipo: prohibidos en el mismo deploy que cambia código viejo. Orden: v1 app vieja + columna nueva → deploy app nueva → drop en migración siguiente.
- `UPDATE`/`DELETE` masivos dentro de migración: chunking (bucle por bloques de ~5-10k), pausas, fuera de pico, log progreso.
- Unique constraints / FKs sobre tablas grandes: crear con `NOT VALID` + `VALIDATE CONSTRAINT` (sin lock largo) cuando el contexto lo permita.

## Compatibilidad y rollback

- Cada migración reversible (`Down` implementado y probado).
- Backup/restore verificado antes de migrar datos.
- La app y la BD deben coexistir durante el deploy: la migración solo debe romper lo que la nueva versión necesita.
- Nunca borrar migraciones aplicadas en producción (el historial `__EFMigrationsHistory` no debe mentir).

## Auto-migrar vs manual

- Producción: `dotnet ef database update` como paso de deploy explícito (no `MigrateAsync()` automático en startup salvo decisión documentada — evalúa riesgo de corridas concurrentes).
- Dev: puede auto-aplicarse en startup (`MigrateAsync`) solo en Development.

## Checklist por migración

- [ ] SQL revisado línea a línea
- [ ] Sin bloqueos de tabla (CONCURRENTLY / NOT VALID / chunking aplicados)
- [ ] Backward compatible con versión anterior de la app
- [ ] Down probado
- [ ] Probada contra BD con volumen real
- [ ] Índices documentados (skill `database-indexes`)
- [ ] No expone datos (no loguear datos de la tabla en migración)

## Anti-patrones

- `DROP CONSTRAINT` y re-crear en una tabla con tráfico sin ventana.
- `UPDATE` gigante en transacción de migración (lock largo).
- Cambiar tipo de columna con datos (replica + switch o columna nueva + swap).
- Migración que depende de datos de producción que no existen en dev (verificar con seed realista).
