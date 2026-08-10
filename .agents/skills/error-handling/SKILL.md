---
name: error-handling
description: 'Manejo de errores en CoppAddresd: excepciones por capa, middleware global, ProblemDetails, correlation ID. Prohibido tragar excepciones.'
---

# Error Handling — Reglas del proyecto

Nunca ocultar errores. Cada error: clasificado, logueado con contexto, traducido a HTTP seguro para el cliente.

## Jerarquía de excepciones

| Tipo | Capa | Ejemplo | HTTP resultante |
|---|---|---|---|
| `NotFoundException` | Domain/Application | Recurso no existe | 404 |
| `BusinessRuleViolationException` | Domain | Estado inválido de negocio | 409 o 422 |
| `ValidationException` | Application (FluentValidation pipeline) | Input inválido | 400 con detalles |
| `UnauthorizedAccessException` / auth | — | Sin permisos | 401/403 |
| `InfrastructureException` | Infrastructure | BD caída, AWS error | 500 (genérico) o 503 |
| `DbUpdateConcurrencyException` | Infrastructure | Conflicto optimista | 409 |

- Definir excepciones de dominio en `Domain/Exceptions/`, de aplicación en `Application/Common/` (o `Exceptions/`).

## Middleware global (a crear en Api)

- `Api/Middleware/ExceptionHandlingMiddleware.cs`: catch de TODA excepción no controlada.
- Traduce excepción conocida → ProblemDetails (RFC 7807) con status correcto.
- Desconocida → 500 genérico, log con `LogError` (excepción completa + Correlation ID), response sin detalles internos.
- `OperationCanceledException`/`TaskCanceledException` → no loguear como error; responder 499/400 (client closed).
- Registrar middleware antes de endpoints; en orden: `UseExceptionHandling` → `UseCorrelationId` → endpoints.

## Reglas

- **Prohibido** `catch (Exception) { }` vacío, `catch (Exception e) { return null; }`, tragar y seguir.
- `catch` solo para: traducir/tipar, agregar contexto, retry con decisión, cleanup — y siempre `throw;`/rethrow con contexto o retorno de error controlado.
- Nada de `throw new Exception("mensaje")` genérico: usar excepciones del dominio tipadas.
- No exponer: stack traces, SQL, connection strings, rutas internas, secretos → nunca en la respuesta HTTP.
- Logs: siempre `ILogger` con contexto (no `Console.WriteLine`).
- Request ID/Correlation ID en cada respuesta y log (skill `logging`).

## Respuesta de error estándar

```json
{
  "type": "https://httpstatuses.io/409",
  "title": "Conflict",
  "status": 409,
  "detail": "El pedido ya fue cancelado",
  "instance": "/api/v1/orders/123/cancel",
  "correlationId": "abc-123"
}
```

## Validación de input

- Errores de FluentValidation: 400 con la lista `{ property, error }` (el pipeline behavior los agrupa).
- Error de negocio con payload sintácticamente válido: 409 (conflicto) o 422 — elegir y ser consistente por módulo.

## Testing

- Tests del middleware: excepción conocida → status esperado y ProblemDetails; desconocida → 500 genérico sin stack; cancelación → sin log de error (skill `testing`).
