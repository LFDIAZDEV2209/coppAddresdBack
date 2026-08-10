---
name: cancellation-token
description: 'Regla OBLIGATORIA del proyecto: CancellationToken propagado de extremo a extremo. Nunca ignorar cancelación.'
---

# CancellationToken — Regla obligatoria del proyecto

Todo proceso asíncrono cancelable acepta y propaga `CancellationToken`. No es opcional.

## Cadena de propagación

```
HTTP Request
→ Endpoint (CancellationToken del request)
→ Handler MediatR (token en la request del command/query)
→ Servicio de aplicación
→ Repositorio/Interfaz (firma: GetXAsync(..., CancellationToken ct))
→ EF Core (ToListAsync(ct), SaveChangesAsync(ct), ...)
→ Base de datos (el driver cancela la query)
```

Ejemplo:

```csharp
public async Task<List<ItemDto>> GetItemsAsync(
    ItemQuery query, CancellationToken ct)
{
    return await db.Items.AsNoTracking()
        .Where(...)
        .Select(...)
        .ToListAsync(ct);
}
```

- En Minimal APIs el token se obtiene: `(CancellationToken ct)` en el delegado o `HttpContext.RequestAborted`.
- En MediatR: `IRequest<T>` + `CancellationToken` en el handler; si el command/query transporta el token, propagarlo al repositorio.

## Reglas

- **Nunca `CancellationToken.None`** cuando existe un token real del request (pierde cancelación del cliente: conexión cortada = query sigue corriendo = recursos/locks retenidos).
- `CancellationToken.None` solo en: operaciones que deben completar sí o sí (persistencia final crítico, jobs que ya no aceptan cancelación) — documentar por qué.
- Capturar cancelación: `OperationCanceledException` / `TaskCanceledException` → respond 499 (client closed request) o 400 según convención de error-handling; no loguear como error 500.
- `ThrowIfCancellationRequested()` para validar estado antes de trabajo largo.
- `Register(Action)` para cleanup en operaciones largas (liberar recursos, marcar estado intermedio).
- Operaciones compuestas (transacción + externo + guardado): cancelar deja la transacción abortada limpia — ver skill `concurrency`/`transactions`.

## Áreas que también exigen el token

| Área | Cómo |
|---|---|
| HttpClient | `client.GetAsync(url, ct)` |
| AWS SDK | `client.FooAsync(request, ct)` — todos los métodos async lo aceptan |
| Background services | `IHostedService.StopAsync(ct)`; dentro del loop usar `_stoppingToken`; `WaitAsync(ct)`/`Delay(ct)` |
| File I/O | `ReadAsync`/`WriteAsync(ct)` de streams |
| Redis/cache distribuida | APIs async con ct cuando la librería lo soporte |
| MediatR pipeline | `IPipelineBehavior` recibe el token |
| Tests | pasar `CancellationToken.None` explícito o probar cancelación (ver skill `testing`) |

## Anti-patrones

- Firma sin parámetro `ct` en repositorios/servicios de E/S.
- `Task.Run`/`Task.Delay` sin token.
- `.Result`/`.Wait()` (además de deadlock, inunda el thread pool).
- Ignorar `OperationCanceledException` y dejar recursos abiertos.
