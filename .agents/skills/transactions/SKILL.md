---
name: transactions
description: 'Transacciones en el proyecto: cuándo, aislamiento, duración, retry, y anti-patrones. Transacciones cortas y predecibles.'
---

# Transacciones — Reglas del proyecto

La transacción es lo más corta posible, predecible, cancelable y aislada correctamente.

## Cuándo usar

- **No transacción** por defecto: una sola escritura atómica (`SaveChangesAsync`) no necesita transacción explícita.
- **Transacción explícita** (`BeginTransactionAsync`) cuando hay 2+ operaciones que deben ser todo-o-nada (varias tablas, múltiples `SaveChangesAsync`, secuencias).

## Uso correcto

```csharp
await using var tx = await db.Database.BeginTransactionAsync(ct);
try
{
    // 1. escrituras (cortas, solo DB)
    await db.Save1Async(ct);
    await db.Save2Async(ct);

    await tx.CommitAsync(ct);
}
catch
{
    await tx.RollbackAsync(ct);   // o dejar que el using deshaga
    throw;
}
```

- `await using` garantiza dispose/rollback si no se commitea.
- Aislamiento: default PostgreSQL `Read Committed` para la mayoría. `Repeatable Read`/`Serializable` SOLO si el caso lo exige (validación de consistencia de lectura + escritura): documentar el porqué y manejar `40001` (serialization failure) con retry.
- Savepoints (`CreateSavepointAsync`) para flujos largos con partes opcionales.

## Lo que NUNCA va dentro de una transacción

- Llamadas HTTP externas, envíos de email, S3, publish de eventos → la transacción se sostiene abierta mientras un tercero responde = locks retenidos. Patrón: persistir + publicar evento de dominio que se procesa después del commit (outbox si se necesita entrega garantizada).
- I/O de usuario (leer archivo, esperar input).
- Tiempos de espera/latencia de red entre escrituras.

## Duración

- Objetivo: transacción < ~100ms en operaciones típicas; nunca minutos.
- Transacciones largas = locks largos = contención y deadlocks (skill `concurrency`).
- Si un flujo necesita validación + escrituras de muchos pasos: reducir a la parte estrictamente atómica.

## Retry

- Reintentar en `40001` (serialization) y `40P01` (deadlock) con backoff exponencial pequeño (p. ej. 3 intentos) y datos re-leídos. Reintentar dentro de una nueva transacción.
- No reintentar errores de validación/dominio ni `23505` (unique violation) si el negocio lo trata como conflicto real.

## Reglas de aislamiento y locking

- `Read Committed` por defecto — no subir sin motivo.
- Para check-then-act: `SELECT FOR UPDATE` dentro de la transacción (ver `concurrency`) o constraint de BD.
- `NOLOCK` no existe en PostgreSQL; las lecturas no bloquean escrituras en MVCC (leer snapshot). Locks reales: escrituras, `FOR UPDATE`, DDL, `VACUUM` agresivo.

## Documentación

Cada flujo con transacción explícita documentado en `docs/database/transactions.md`: qué valida, qué escribe, aislamiento, duración esperada, comportamiento ante error/cancelación/doble ejecución.
