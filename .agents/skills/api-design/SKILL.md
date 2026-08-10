---
name: api-design
description: 'Diseño de API REST en CoppAddresd: verbos, status codes, DTOs, listas con paginación/filtros/orden, validación, versionado, errores.'
---

# API Design — Reglas del proyecto

Minimal APIs sobre el patrón REST. Lecturas separadas conceptualmente de mutaciones (CQRS: Queries vs Commands).

## Verbos y semántica

| Verbo | Uso | Idempotente |
|---|---|---|
| GET | Leer (queries, paginadas) | Sí |
| POST | Crear + acciones que no son actualización de recurso | No |
| PUT | Reemplazo completo | Sí |
| PATCH | Actualización parcial (opcional) | No |
| DELETE | Borrar | Sí (o 404 en segunda llamada) |

- Nombres de rutas en plural, kebab-case: `/api/v1/users/{id}/orders`.
- Acciones de negocio: POST a sub-recurso con nombre de acción (`/api/v1/orders/{id}/cancel`), no verbos en la URL.

## Status codes

- 200 OK (GET/PUT), 201 Created (POST con `Location`), 204 No Content (DELETE exitoso).
- 400 validación/input inválido (detalle de errores), 401 no autenticado, 403 no autorizado, 404 no existe, 409 conflicto (unique violation, concurrencia optimista, estado inválido de negocio), 422 para errores de negocio con payload válido (opcional), 500 error no manejado, 503 degradado (BD caída).
- Nunca exponer 500 con detalles internos (skill `error-handling`).

## Contratos (DTOs)

- Requests y Responses = DTOs (records), nunca entidades de dominio (evita sobre-exposición y acoplamiento).
- Listas → `PagedResult<T>` (skill `pagination`).
- Propiedades en camelCase (default System.Text.Json).
- Validar input: FluentValidation en Application (pipeline behavior), ver skill `service-layer`.

## Listas: paginación, filtros, orden, búsqueda

Convención de query string:

```text
?page=1&pageSize=20
?cursor=...            (keyset)
?filter=status:active,name:co~   (o params explícitos: ?status=active&search=co)
?sort=-createdAt        (prefix - = desc)
?search=coppaddresd
```

- Siempre paginado con límite máximo (default 20 / max 100, skill `pagination`).
- Filtros acotados a columnas indexadas o documentadas; nunca filtros libres sobre columnas no indexadas sin evaluación (skill `query-performance`).
- Orden por columnas permitidas (whitelist en código, nunca interpolación directa).

## Versionado

- `/api/v1/...` en la ruta. Romper compatibilidad → v2; mantener v1 el tiempo acordado.

## Errores (envelope consistente)

```json
{
  "type": "https://httpstatuses.io/409",
  "title": "Conflict",
  "status": 409,
  "detail": "El pedido ya fue cancelado",
  "instance": "/api/v1/orders/123/cancel",
  "correlationId": "a1b2..."
}
```

- Basado en RFC 7807 (ProblemDetails) — soporte nativo en ASP.NET Core.
- Sin stack traces, sin SQL, sin datos de infraestructura al cliente (skill `error-handling`).

## Reglas de endpoints

- Endpoints delgados: validar input → invocar MediatR → mapear → responder. Sin lógica de negocio (skill `architecture`).
- `CancellationToken` en cada delegado (skill `cancellation-token`).
- Autorización por política en endpoints sensibles (Identity/JWT desde el servicio Auth).
- OpenAPI documentado (`.WithName`, `[EndpointSummary]`/`[EndpointDescription]` en español).

## Anti-patrones

- Endpoints que devuelven listas sin límite.
- Devolver entidades de dominio/DbContext.
- Lógica de negocio o queries directas en el endpoint.
- Varios verbos en una ruta para "conveniencia" sin semántica.
