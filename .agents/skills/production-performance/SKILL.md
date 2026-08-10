---
name: production-performance
description: 'Rendimiento en producción: presupuesto de recursos, conexiones, latencia, memoria, GC, observabilidad. Decisiones pensadas para carga real en AWS.'
---

# Producción y Escalabilidad — Reglas del proyecto

Toda decisión se evalúa para **producción en AWS con carga real**, no para el equipo local. No optimizar "para que funcione en mi máquina".

## Preguntas de escalabilidad (aplicar a cada implementación importante)

```text
¿Qué ocurre con 10 usuarios?       → debe ser trivial
¿Qué ocurre con 1.000 usuarios?    → sin degradación
¿Qué ocurre con 100k registros?    → consultas siguen indexadas
¿Qué ocurre con millones?          → paginación keyset, agregaciones en BD, read models si aplica
¿Qué ocurre con 100 req concurrentes? → sin contención
¿Qué ocurre con 1.000 req concurrentes? → pool, cache, colas, escala horizontal
```

## Presupuesto de recursos

- **Conexiones a BD**: presupuesto = max connections del RDS / instancias. Con `Max Pool Size` por app configurado (`MaxPoolSize=100` típico para default Npgsql 100); no exceder. Conexión abierta el menor tiempo posible — no abrir antes de operaciones externas.
- **Latencia**: objetivo p95 < 100ms (queries simples) / < 500ms (complejas/agregaciones). Medir y documentar.
- **Memoria**: no materializar conjuntos grandes en memoria (ver `linq`); DTOs pequeños; paginación.
- **CPU**: agregaciones/cálculos en BD (SQL) no en app; indexación correcta (skill `database-indexes`).
- **GC**: evitar alojamientos en hot paths (llamadas repetidas); ValueTask solo si se mide mejora; no boxes innecesarios en loops críticos.
- **Thread pool**: nunca bloquear hilos con `.Result`/`.Wait()`; async de punta a punta; `Task.Delay` con token en jobs.
- **Serialización**: DTOs planos (sin sobre-carga de navegaciones); System.Text.Json (default); evitar ciclos/entidades en respuestas.

## Conexiones y pool

- `UseNpgsql` con `MaxPoolSize` acorde al RDS; connection string nunca en código (skill `aws-production`).
- Queries largas (reportes) → réplica de lectura o consulta dedicada, no el pool del tráfico principal.
- Idle timeout de pool coherente con el RDS.

## Patrones de escala

- Lecturas: réplicas RDS + caché (skill `caching`).
- Escrituras concurrentes: ver `concurrency`, `transactions`.
- Procesos pesados/emitir correos/reportes: background service / cola (SQS), nunca en el request.
- HTTP: `CancellationToken` (skill `cancellation-token`).

## Observabilidad de rendimiento

- Medir en producción: duración de query por endpoint, p95, tasa de error, uso de conexiones, % locks.
- Dashboard mínimo: CloudWatch (latencia, errores 5xx, conexiones BD, CPU/mem RDS) + alarmas (ver skill `aws-production`).

## Regla de decisión

Si hay que elegir entre "funciona" y "funciona bien en producción con carga", elegir la segunda — siempre que el costo de complejidad no supere el beneficio (KISS, skill `architecture`).
