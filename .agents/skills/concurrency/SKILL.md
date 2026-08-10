---
name: concurrency
description: 'Concurrencia en el proyecto: control de concurrencia optimista/pesimista, bloqueos, deadlocks, idempotencia, race conditions.'
---

# Concurrencia — Reglas del proyecto

Cada operación crítica se evalúa bajo concurrencia real antes de implementarse: no solo "funciona", sino que funciona cuando 1.000 requests tocan el mismo dato.

## Evaluación obligatoria de cada operación crítica

```text
¿Cuánto dura la transacción?
¿Qué registros bloquea?
¿Qué índices usa?
¿Puede ejecutarse concurrentemente?
¿Qué ocurre si falla?
¿Qué ocurre si se cancela?
¿Qué ocurre si se ejecuta dos veces?   → idempotencia
```

## Concurrencia optimista (preferida)

- Columna de versión `rowversion` en PostgreSQL: `xmin` nativo o columna `version` (`bigint`) incrementada.
- En EF Core: `Property(x => x.Version).IsRowVersion()` (o `UseXminAsConcurrencyToken()` de Npgsql).
- Capturar `DbUpdateConcurrencyException` → reintento con datos frescos o 409 Conflict al cliente.
- Aplicar en: ediciones de entidades de negocio, configuración compartida, perfiles.

## Concurrencia pesimista

- PostgreSQL: `SELECT ... FOR UPDATE` (`EF: .UseRowNumber` no; Npgsql: `.ForUpdate()` en EF 9+/`UseXmin`…). Uso concreto: `query = db.X.Where(...).ForUpdate()` (extensiones Npgsql) dentro de transacción.
- Solo cuando la actualización dependa de lectura-escritura inseparable (saldo, stock crítico) y la contención justifique el lock.
- Nunca `FOR UPDATE` sin transacción; mantener la transacción lo más corta posible (skill `transactions`).

## Bloqueos / deadlocks

- Causas típicas: transacciones largas, actualizaciones con orden inconsistente entre tablas, queries sin índice que toman locks amplios.
- Prevención: orden de actualización consistente en todo el código (mismo orden de tablas), índices en columnas filtradas por UPDATE/DELETE, transacciones cortas.
- `UPDATE`/`DELETE` masivos: chunking y fuera de horario pico; pueden escalar a `ROW EXCLUSIVE`/lock de tabla (skill `migrations`).
- Retry con backoff ante `PostgresException` 40P01 (deadlock) / 40001 (serialization) — patrón de reintento documentado en `docs/database/transactions.md`.

## Race conditions típicas a vigilar

- Check-then-act sin lock: `if (exists) insert` → usar constraint único + capturar violación, o `INSERT ... ON CONFLICT`.
- Contadores/agregados leídos-calculados-escritos → hacerlo en una sola operación SQL (`UPDATE ... SET n = n + 1`), `ExecuteUpdateAsync` con expresión, o transacción con lock.
- Caches locales con invalidation no atómica (skill `caching`).
- Procesos background duplicados (múltiples instancias) → lock de líder/distribuido (ver `aws-production`) o operación idempotente.

## Reglas

- Idempotencia: los efectos duplicados no deben duplicar estado (clienteId/requestId único o `ON CONFLICT`).
- No asumir atomicidad de varias operaciones sin transacción.
- CancellationToken y concurrencia: no dejar estados a medias al cancelar; transacción corta cancela limpio (skill `cancellation-token`).
- En tests: cubrir actualización simultánea (dos requests al mismo recurso), violación de unique, reintento, cancelación a mitad de transacción (skill `testing`).
