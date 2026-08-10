---
name: logging
description: 'Logging y observabilidad en CoppAddresd: logging estructurado, Correlation ID, niveles, medición, redacción de datos sensibles.'
---

# Logging y Observabilidad — Reglas del proyecto

Todo proceso crítico debe poder diagnosticarse en producción. Logs estructurados con contexto, no líneas sueltas.

## Estándares

- **Structured logging**: `ILogger` con mensajes y parámetros tipados (`LogInformation("Order {OrderId} created", id)`), nunca interpolación de strings a mano (pierde estructura/costos).
- **Correlation ID / Request ID**: middleware genera/recibe `X-Correlation-Id`, se incluye en cada log del request (scope de logging) y en la respuesta de error (skill `error-handling`).
- Scopes: `BeginScope` por request (correlation id, usuario, tenant si existe) para que todo log del request lo lleve.
- Niveles: `Debug` (detalle dev, deshabilitado en prod salvo diagnóstico), `Information` (eventos de negocio: created, updated, deleted), `Warning` (recuperable: retry, timeout, 4xx de clientes no deseados), `Error` (fallo real: excepción no controlada, BD caída, AWS error).

## Qué loguear (mínimo obligatorio)

```text
¿Qué pasó? ¿Cuándo? ¿En qué request (correlation id)?
¿Con qué usuario/proceso? ¿Cuánto demoró?
¿Dónde falló? ¿Por qué falló?
```

- Duración por operación importante: `Stopwatch` → `LogInformation("Query {Name} took {Ms}ms", ...)` o métricas.
- Errores con la excepción completa en `LogError(ex, "contexto")`, no solo mensaje.
- Eventos de negocio: logear idempotencia del evento (qué recurso, qué acción).

## Qué NO loguear

- **Secretos**: passwords, tokens, connection strings, headers de auth, claves API, datos PII innecesarios.
- SQL con parámetros en producción (solo duración); `EnableSensitiveDataLogging` solo dev (skill `entity-framework`).
- Logs en bucles de alta frecuencia sin throttling (costo + ruido en CloudWatch).
- Stack traces completos en logs de nivel Information.

## Herramientas (proyectadas)

- Dev: consola. Producción: CloudWatch Logs (JSON) — ver skill `aws-production`.
- Métricas: duración de query, latencia de endpoint, tasa de error — dashboard + alarmas.
- No es necesario agregar Serilog mientras `ILogger` + formateador JSON satisfaga; decidir con criterio de costo (KISS).

## Patrón en código

```csharp
using var scope = logger.BeginScope(new { CorrelationId = correlationId, UserId = userId });
logger.LogInformation("Order {OrderId} cancelled by user {UserId}", orderId, userId);
```

## Testing

- Verificar que operaciones críticas loguean con contexto; errores loguean con excepción; nada sensible aparece en logs (tests con captura de ILogger si aplica).
