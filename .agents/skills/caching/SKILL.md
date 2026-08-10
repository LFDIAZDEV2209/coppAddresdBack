---
name: caching
description: 'Cache en CoppAddresd: cuándo, qué tipo (memory vs Redis), TTL, invalidación, consistencia. No cache por moda.'
---

# Caching — Reglas del proyecto

Cache es una optimización con costo de consistencia. Se agrega solo con justificación medida y diseño de invalidación.

## Cuándo cachear (checklist)

- Lectura de alta frecuencia y baja volatilidad (catálogos, config, datos de referencia).
- Query costosa repetida (reportes agregados con TTL corto y tolerancia a datos viejos).
- Resultado medido como cuello de botella (latencia p95 alto, carga BD alta).

**No cachear** "por si acaso", datos sensibles sin cifrar, datos por-usuario altamente volátiles (saldo, stock), ni para ocultar queries mal diseñadas — primero: índice, proyección, paginación (skills `query-performance`, `database-indexes`).

## Tipos

| Tipo | Uso | Lifetime |
|---|---|---|
| `IMemoryCache` | Datos por instancia, alta volatilidad, cache de hot path | Default |
| Redis (distribuido) | Datos compartidos entre instancias ECS, sesiones, rate limit, invalidación global | Cuando hay >1 instancia |
| HTTP Cache | `Cache-Control` en respuestas públicas (catálogos, docs) | Si aplica |

- Regla: **una sola instancia** → `IMemoryCache` bien; múltiples instancias → Redis (memoria local por instancia queda inconsistente).
- Clientes Redis: `StackExchange.Redis` (no agregar sin necesidad).

## Cada cache documentado

```text
Qué se almacena | TTL | Invalidación | Consistencia | Tamaño estimado | Impacto esperado
```

en `docs/performance/` o README del módulo.

## Patrones y trampas

- Clave con versionado: `orders:{tenantId}:{id}:v1` — invalidar por prefijo o versión.
- **Stampede**: TTL bajo + valor recién expirado con muchos hits → usar lock/`GetOrCreateAsync` con duplicación mínima, o TTL de refresco en background.
- Invalidación: al escribir el dato subyacente (update en el mismo flujo del handler), no por temporizador arbitrario salvo tolerancia documentada.
- Nunca cachear el objeto de negocio con referencias mutables compartidas (clonar o DTO inmutable).
- Medir hit rate e impacto; si no mejora nada medible, quitarlo.
- Redis como cache y no como BD principal: TTL por defecto en toda clave.

## Reglas

- Cache solo en la capa que lo consume (Application vía interfaz `ICacheService` si aplica; no esparcir `IMemoryCache` por todos lados).
- `CancellationToken` en operaciones async de cache (skill `cancellation-token`).
- Fallo de cache no debe romper la request: degradar a fuente de datos con log Warning (fail-open).
- Consistentemente: TTL por clave, no infinito, salvo datos inmutables.
