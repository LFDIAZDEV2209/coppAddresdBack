# Concurrencia — Reglas y casos

Skill: `concurrency`.

## Evaluación obligatoria por operación crítica

```text
¿Duración de la transacción? ¿Qué registros bloquea? ¿Qué índices usa?
¿Puede ejecutarse concurrentemente? ¿Qué ocurre si falla?
¿Qué ocurre si se cancela? ¿Qué ocurre si se ejecuta dos veces (idempotencia)?
```

## Estándares

- **Optimista** por defecto: columna de versión (`IsRowVersion()` o `UseXminAsConcurrencyToken()` de Npgsql); `DbUpdateConcurrencyException` → reintento o 409.
- **Pesimista** (`FOR UPDATE`) solo cuando la actualización depende de lectura-escritura inseparable (saldo/stock) y la contención lo justifica; siempre dentro de transacción corta.
- Order de actualización consistente entre tablas (evita deadlocks).
- Índices en columnas de filtro de UPDATE/DELETE (locks amplios sin índice).
- Retry `40P01`/`40001` con backoff y datos frescos.
- Check-then-act: constraint único + captura `23505`, o `ON CONFLICT`.
- Contadores: `UPDATE ... SET n = n + 1` en una operación, no leer-calcular-escribir.
- Jobs background: idempotencia o lock de líder (múltiples instancias ECS).
- Cancelación no debe dejar estados a medias (transacción corta → abort limpio).

## Casos registrados

| Fecha | Ubicación | Riesgo | Solución | Estado |
|---|---|---|---|---|
| — | — | — | — | — |

_(Vacío por esqueleto.)_
