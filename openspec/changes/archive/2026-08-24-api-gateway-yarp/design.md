# Design: api-gateway-yarp

## Technical Approach

Nuevo proyecto .NET 10 standalone `CoppAddresd.Gateway` (YARP.ReverseProxy, puerto `5080`) que reenvía por prefijo a Auth (5123), Api (5122) y Telemedicine (5130). CORS centralizado en el gateway, middleware `X-Internal-Key` antes de `MapReverseProxy`, SSE con `ActivityTimeout` extendido, health checks pasivos + sonda activa por cluster, cookie `copp_refresh_token` preservada por mismo origen. Frontend colapsa 3 env vars a `NEXT_PUBLIC_GATEWAY_URL`; móvil apunta a `5080` en dev y al dominio del gateway en Capacitor. Plan AWS documentado en `docs/architecture/gateway.md` (split API Gateway REST + ALB/CloudFront para `/api/v1/chat/stream`).

## Architecture Decisions

### Decision: YARP standalone en `src/Services/CoppAddresd.Gateway`

**Choice**: Nuevo proyecto .NET 10 sin `ProjectReference` (igual que `CoppAddresd.Auth`/`CoppAddresd.Telemedicine`). Solo paquetes NuGet: `Yarp.ReverseProxy 2.3.0`, `Microsoft.Extensions.Http 10.0.10`.
**Alternatives**: YARP dentro de `CoppAddresd.Api` (descartado: rompe autonomía + skill `architecture`); Nginx/Caddy externo (descartado: rompe modelo mental AWS); AWS API Gateway en dev (descartado: diverge de prod).
**Rationale**: Migración a AWS casi 1:1 (Rutas YARP ≈ resource paths API Gateway); gateway "tonto" que delega JWT/validación a cada backend (sin duplicar Secret/Issuer/Audience).

### Decision: Middleware `X-Internal-Key` antes de `MapReverseProxy`

**Choice**: `InternalKeyMiddleware` registrado con `app.UseMiddleware<InternalKeyMiddleware>()` ANTES de `app.MapReverseProxy()`. Intercepta `/api/auth/internal/*` y `/api/v1/internal/*`. Sin header → `404 application/problem+json`. Comparación en tiempo constante (`CryptographicOperations.FixedTimeEquals`).
**Alternatives**: Política `[Authorize]` en YARP (no existe); respuesta `401` (rechazada por REQ-GW-006 que exige 404).
**Rationale**: Ocultar existencia del endpoint (404 = "no existe") no expone superficie de ataque.

### Decision: SSE con `ActivityTimeout` + `ForwarderRequestReadTimeout`

**Choice**: cluster `api` con `HttpClient.Timeout=00:05:00` (300 s) para SSE y `HttpRequest.ActivityTimeout=00:10:00` (600 s) — configurado en `appsettings.json` (base, heredado en Docker). YARP v2.3+ ya reenvía streams sin buffering por defecto con `SocketsHttpHandler`; no se añade `MetadataTransform` (se evita reescribir `Content-Type`).
**Alternatives**: WebSocket (descartado: backend usa SSE); buffering chunked (descartado: corta el stream).
**Rationale**: 60 s de stream continuo es el requisito mínimo (REQ-GW-007); `ActivityTimeout` de 10 min da margen amplio para respuestas largas del AI Service.

### Decision: Frontend colapsa a `NEXT_PUBLIC_GATEWAY_URL`

**Choice**: `lib/config/env.ts` redefine `apiUrl = process.env.NEXT_PUBLIC_GATEWAY_URL ?? "http://localhost:5080"`. `authApiUrl`/`telemedicineApiUrl` se eliminan; los 24 servicios que usaban `env.authApiUrl`/`env.apiUrl`/`env.telemedicineApiUrl` se actualizan a `env.apiUrl` (path completo incluido: `${env.apiUrl}/api/auth/...`, `${env.apiUrl}/api/v1/...`, `${env.apiUrl}/api/v1/telemedicine/...`).
**Rationale**: Punto único de cambio (REQ-GW-010). Mantiene paths absolutos hacia el gateway (no se reescriben paths).

## Data Flow

```
Cliente (web/móvil/webhook)
   │  Authorization: Bearer <jwt>  |  Cookie: copp_refresh_token
   ▼
[1] InternalKeyMiddleware  ── /api/{auth,v1}/internal/* SIN X-Internal-Key ──▶ 404
   │ (si pasa)
   ▼
[2] UseCors  ── preflight 204 + headers
   ▼
[3] MapReverseProxy  ── route match (priority) ──▶ cluster (con ActiveHealthCheck)
   │                                                        │
   │  Authorization / Cookie / X-Twilio-Signature ◀───────────┤  pasa transparente
   ▼
Backend (Auth 5123 / Api 5122 / Telemedicine 5130)
   │
   ▼ (response)
[4] Set-Cookie (Path=/api/auth preservado) ◀── ResponseHeadersCopy = true
   ▼
Cliente (cookie actualizada en dominio del gateway)
```

## File Changes

| Archivo | Acción | Descripción |
|---|---|---|
| `src/Services/CoppAddresd.Gateway/CoppAddresd.Gateway.csproj` | Crear | `<TargetFramework>net10.0</TargetFramework>` + `Yarp.ReverseProxy 2.3.0` + `Microsoft.Extensions.Http 10.0.10`. Sin `ProjectReference`. |
| `src/Services/CoppAddresd.Gateway/Program.cs` | Crear | `AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))`. Middleware pipeline: `UseMiddleware<InternalKeyMiddleware>()` → `UseCors` → `MapReverseProxy()` + `MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync })`. |
| `src/Services/CoppAddresd.Gateway/Configuration/CorsSettings.cs` | Crear | `record CorsSettings(string[] Origins)` bind a `Cors:Origins`. |
| `src/Services/CoppAddresd.Gateway/Configuration/InternalKeySettings.cs` | Crear | `record InternalKeySettings(string Key, bool Enabled)`. |
| `src/Services/CoppAddresd.Gateway/Middleware/InternalKeyMiddleware.cs` | Crear | Ver interfaces/contracts. |
| `src/Services/CoppAddresd.Gateway/HealthChecks/HealthResponseWriter.cs` | Crear | JSON `{"status":"Healthy","clusters":{...}}`. Sonda `HttpClient.GetAsync("http://localhost:{port}/health")` por cluster con timeout 2 s. |
| `src/Services/CoppAddresd.Gateway/appsettings.Example.json` | Crear | Template versionado (appsettings.json real está gitignoreado). |
| `src/Services/CoppAddresd.Gateway/appsettings.Development.json` | Crear | Origenes dev + cluster ports. |
| `src/Services/CoppAddresd.Gateway/Properties/launchSettings.json` | Crear | `applicationUrl: http://localhost:5080`, perfil `http`/`https`. |
| `coppAddresdBack/CoppAddresd.slnx` | Modificar | Insertar `<Project Path="src/Services/CoppAddresd.Gateway/CoppAddresd.Gateway.csproj" />` en `<Folder Name="/src/Services/">`. |
| `coppAddresdBack/docker-compose.yaml` | Modificar | Servicio `gateway` con `build: ./src/Services/CoppAddresd.Gateway`, `ports: ["5080:5080"]`, `depends_on: [postgres]`, healthcheck `curl /health`. |
| `coppaddresd-front/lib/config/env.ts` | Modificar | Colapsar a `apiUrl` único. Borrar `authApiUrl`/`telemedicineApiUrl`. |
| `coppaddresd-front/lib/api/{http,auth-service,invitation-service}.ts` + 21 servicios en `features/*/services/*.ts` | Modificar | Reemplazar `${env.authApiUrl}`/`${env.telemedicineApiUrl}` por `${env.apiUrl}` (path completo). |
| `coppaddresd-front/.env` + `.env.example` | Modificar | Una sola línea `NEXT_PUBLIC_GATEWAY_URL=http://localhost:5080`. |
| `antares-paciente/vite.config.ts` | Modificar | Proxy `/api` → `http://localhost:5080` (borrar `/api/auth`). |
| `antares-paciente/capacitor.config.ts` | Modificar | `server: { url: "https://api.example.com", androidScheme: "https" }` (placeholder). |
| `src/Services/CoppAddresd.Auth/appsettings.Example.json` + `.Development.json` | Modificar | Añadir `http://localhost:5080` a `Cors:Origins`. |
| `src/Services/CoppAddresd.Telemedicine/Program.cs` | Modificar | Añadir `http://localhost:5080` a whitelist. |
| `src/CoppAddresd.Api/Extensions/ApplicationServiceExtensions.cs` | Modificar | Añadir `http://localhost:5080` a whitelist CORS. |
| `docs/architecture/gateway.md` | Crear | Plan AWS (ver §AWS Plan). |
| `openspec/changes/api-gateway-yarp/design.md` | Crear | Este archivo. |

## Interfaces / Contracts

**`appsettings.Example.json`** (referencia; gitignoreado el real):

```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Yarp": "Warning" } },
  "AllowedHosts": "*",
  "Urls": "http://localhost:5080",
  "Cors": {
    "Origins": [ "http://localhost:3000", "capacitor://localhost", "ionic://localhost" ]
  },
  "InternalKey": {
    "Enabled": true,
    "Key": "change-me-internal-key-from-secrets-manager"
  },
  "Clusters": {
    "Auth": { "BaseUrl": "http://localhost:5123", "HealthCheckPath": "/health" },
    "Api": { "BaseUrl": "http://localhost:5122", "HealthCheckPath": "/health" },
    "Telemedicine": { "BaseUrl": "http://localhost:5130", "HealthCheckPath": "/health" }
  },
  "ReverseProxy": {
    "Routes": {
      "authRoute": {
        "ClusterId": "auth",
        "Priority": 100,
        "Match": { "Path": "/api/auth/{**catch-all}" }
      },
      "telemedRoute": {
        "ClusterId": "telemedicine",
        "Priority": 300,
        "Match": { "Path": "/api/v1/telemedicine/{**catch-all}" },
        "Transforms": [
          { "ResponseHeader": "X-Refresh-Status", "Append": "false" }
        ]
      },
      "apiRoute": {
        "ClusterId": "api",
        "Priority": 200,
        "Match": { "Path": "/api/v1/{**catch-all}" }
      }
    },
    "Clusters": {
      "auth": {
        "Destinations": { "primary": { "Address": "http://localhost:5123/" } },
        "HttpClient": { "DangerousAcceptAnyServerCertificate": false }
      },
      "api": {
        "Destinations": { "primary": { "Address": "http://localhost:5122/" } },
        "HttpClient": { "Timeout": "00:05:00" },
        "HttpRequest": { "ActivityTimeout": "00:10:00" }
      },
      "telemedicine": {
        "Destinations": { "primary": { "Address": "http://localhost:5130/" } }
      }
    },
    "HealthCheck": {
      "AvailableDestinationsPolicy": "HealthyOrDegraded",
      "Active": {
        "Enabled": true,
        "Interval": "00:00:30",
        "Timeout": "00:00:05",
        "Policy": "ConsecutiveFailures",
        "Path": "/health"
      },
      "Passive": {
        "Enabled": true,
        "Policy": "TransportFailureRatePerRequest",
        "ReactivationPeriod": "00:00:30"
      }
    }
  }
}
```

Notas: en YARP una ruta con `Priority` MÁS ALTO se evalúa PRIMERO. Para que `telemedRoute` venza al catch-all de `apiRoute` (que también casa `/api/v1/telemedicine/*`), `telemedRoute` lleva el priority más alto (300) y `apiRoute` 200, `authRoute` 100. YARP reenvía `Authorization`/`Cookie`/`X-Twilio-Signature`/`X-Forwarded-*` por defecto (`HttpTransformer.Default`). NO añadir transforms que reescriban el body o `Content-Type` para `/api/v1/telemedicine/webhooks/twilio`.

**`InternalKeyMiddleware.cs`** (núcleo):

```csharp
public async Task InvokeAsync(HttpContext context)
{
    var path = context.Request.Path.Value ?? "";
    var isInternal = path.StartsWith("/api/auth/internal/", OrdinalIgnoreCase)
                  || path.StartsWith("/api/v1/internal/", OrdinalIgnoreCase);
    if (!isInternal) { await _next(context); return; }

    if (!_settings.Enabled) { await RejectAsync(context); return; }

    if (!context.Request.Headers.TryGetValue("X-Internal-Key", out var v) ||
        string.IsNullOrEmpty(v.FirstOrDefault()))
    {
        _logger.LogWarning("Internal sin X-Internal-Key desde {Ip} a {Path}",
            context.Connection.RemoteIpAddress, path);
        await RejectAsync(context); return;
    }

    var provided = Encoding.UTF8.GetBytes(v.First()!);
    var expected = Encoding.UTF8.GetBytes(_settings.Key);
    if (provided.Length != expected.Length ||
        !CryptographicOperations.FixedTimeEquals(provided, expected))
    {
        await RejectAsync(context); return;
    }

    await _next(context);
}

private static async Task RejectAsync(HttpContext ctx)
{
    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
    ctx.Response.ContentType = "application/problem+json";
    await ctx.Response.WriteAsJsonAsync(new
    {
        type = "about:blank", title = "Not Found", status = 404,
        detail = "The requested resource was not found."
    });
}
```

**CORS en gateway** (Program.cs):

```csharp
builder.Services.AddCors(o =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
        ?? new[] { "http://localhost:3000" };
    o.AddPolicy("GatewayCors", p => p.WithOrigins(origins)
        .AllowCredentials().AllowAnyMethod().AllowAnyHeader()
        .WithExposedHeaders("X-Refresh-Status"));
});
// ...
app.UseCors("GatewayCors");
```

**Frontend** — `lib/config/env.ts` definitivo:

```ts
export const env = {
  apiUrl: process.env.NEXT_PUBLIC_GATEWAY_URL ?? "http://localhost:5080",
  applicationCode: process.env.NEXT_PUBLIC_APPLICATION_CODE ?? "erp",
  sessionIdleMinutes: Number(process.env.NEXT_PUBLIC_SESSION_IDLE_MINUTES ?? "30"),
} as const;
```

Reemplazo en cada servicio:
- `${env.authApiUrl}/api/auth/login` → `${env.apiUrl}/api/auth/login`
- `${env.authApiUrl}/api/users` → `${env.apiUrl}/api/users`
- `${env.apiUrl}/api/v1/chat/stream` → `${env.apiUrl}/api/v1/chat/stream`
- `${env.telemedicineApiUrl}/api/v1/telemedicine` → `${env.apiUrl}/api/v1/telemedicine`

## Testing Strategy

| Capa | Qué probar | Cómo |
|---|---|---|
| Unit | `InternalKeyMiddleware` (sin header, con key inválida, con key válida, paths no internos) | xUnit + `DefaultHttpContext` + `IOptions<InternalKeySettings>`. |
| Unit | `HealthResponseWriter` (todos healthy, uno Unhealthy → 503) | xUnit + mock `IHealthChecksBuilder`. |
| Integration | Gateway arranca, `/health` → 200 con JSON correcto | `WebApplicationFactory<Program>` + `HttpClient`. |
| E2E | Login OTP → cookie con `Path=/api/auth` (S-01, S-02) | Playwright: `POST /api/auth/login` vía `:5080`, inspeccionar `Set-Cookie`. |
| E2E | Refresh con cookie corrupta → 401 + `X-Refresh-Status: invalid` (S-02) | Playwright + cookie manipulada. |
| E2E | Chat stream 60 s sin corte (S-03) | Backend que emite 60 eventos a 1 s; leer vía `EventSource`; assert todos los eventos en orden. |
| E2E | Webhook Twilio con HMAC válido aceptado (S-04) | Firmar body real con `Twilio.Security.RequestValidator`, POST vía gateway, assert 200. |
| E2E | Internal sin `X-Internal-Key` → 404 (S-05) | `curl /api/auth/internal/authorize` sin header → 404. |
| E2E | CORS preflight desde `localhost:3000` (S-09) | `OPTIONS /api/v1/chat` con `Origin` → 204 + headers. |
| E2E | Móvil Vite proxy → gateway → cluster (S-07) | Playwright contra `:5173` con `proxy: /api → :5080`. |

## Migration / Rollout

**Fases de implementación** (orden estricto):

1. **Scaffold**: csproj, Program.cs (mínimo), `CoppAddresd.slnx`, `launchSettings.json`, `appsettings.Development.json`. `dotnet build` debe pasar.
2. **Config rutas/clusters**: `appsettings.Example.json` con tabla completa (ver §Interfaces). Smoke test: `GET http://localhost:5080/health` → 200; `GET http://localhost:5080/api/me` con Bearer → respuesta de Auth.
3. **CORS gateway + backends**: añadir `http://localhost:5080` a `Cors:Origins` de los 3 backends. Test preflight desde frontend contra `:5080`.
4. **Bloqueo internals**: `InternalKeyMiddleware` + `InternalKeySettings`. Test E2E sin header → 404; con key válida (config local) → reenvía.
5. **Frontend**: refactor `env.ts` + 24 servicios. Build del frontend verde; login OTP funcional vía `:5080`.
6. **Móvil**: `vite.config.ts` proxy genérico `/api`; `capacitor.config.ts` `server.url` placeholder.
7. **docker-compose**: servicio `gateway` + healthcheck.
8. **Doc AWS**: `docs/architecture/gateway.md` (ver §AWS Plan).
9. **Tests E2E completos**: login OTP, refresh, SSE 60 s, Twilio webhook, internal 404, CORS preflight.
10. **Verificación final**: `dotnet test` verde + matriz de escenarios S-01 a S-10 pasada.

**Rollback** (< 15 min): detener gateway → revertir `env.ts` (3 URLs) + `vite.config.ts` → re-exponer CORS en backends → remover `CoppAddresd.Gateway` de `slnx` → verificar OTP, refresh, SSE y webhook Twilio.

## AWS Plan (resumen; detalle en `docs/architecture/gateway.md`)

| Aspecto | Local (YARP) | AWS |
|---|---|---|
| Path routing | `Routes` en `appsettings` | API Gateway REST API con resources anidados (`/api`, `/api/auth`, `/api/v1`, `/api/v1/telemedicine`, `/api/v1/chat`) |
| SSE `/api/v1/chat/stream` | YARP cluster `api` con `HttpClient.Timeout=00:05:00` + `HttpRequest.ActivityTimeout=00:10:00` | **NO API Gateway**: ALB/CloudFront → ECS `CoppAddresd.Api` (API Gateway corta streams largos) |
| Custom domain | `localhost:5080` | `api.coppaddresd.com` (placeholder) en Route 53 + ACM |
| JWT authorizer | Backend valida JWT | API Gateway JWT authorizer reutilizando Secret/Issuer/Audience del Auth Service vía Secrets Manager |
| Throttling | Rate limit en backend | API Gateway usage plans + API keys |
| Cookie same-domain | `localhost:5080` | Único dominio `api.coppaddresd.com` (Path-based routing, NO subdominios) |
| Twilio webhook | YARP pasa transparente | POST `/api/v1/telemedicine/webhooks/twilio` directo al ALB (NO API Gateway para preservar firma HMAC) |
| Storage S3 | Backend → presigned URLs | Sin cambios (browser ↔ S3 directo, bypass gateway) |
| Alternativa SSE | YARP `ActivityTimeout` | API Gateway REST `Response Streaming STREAM` (payload format 2.0) si se prefiere unificar; validar compatibilidad SSE |

## Riesgos técnicos con mitigación

| Riesgo | Prob. | Mitigación concreta |
|---|---|---|
| SSE cortado por buffering/timeout YARP | M | `HttpClient.Timeout=00:05:00` por cluster; `ActiveHealthCheck.Interval=30s` no interfiere con stream; test E2E 60 s obligatorio en fase 9. |
| Webhook Twilio: header extra invalida HMAC | B | YARP no añade headers al body; test E2E con `RequestValidator` real (secret de Twilio) — debe devolver 200. Documentar NO añadir transforms que reescriban body. |
| Cookie `copp_refresh_token` no viaja (subdominio) | M | AWS usa **un único dominio** `api.coppaddresd.com` con path-based routing. ADR en `gateway.md` lo prohíbe. |
| CORS multi-origen (capacitor://localhost, ionic://localhost) | M | Lista en `Cors:Origins` del gateway; backends añaden `http://localhost:5080` a su whitelist. `AllowCredentials` solo en gateway (backends mantienen `AllowAnyMethod` pero el navegador usa `Origin` del gateway). |
| Quinto proceso en dev (gateway + 4 backends + postgres) | B | `docker-compose.yaml` con `gateway` + script `scripts/dev-up.sh` documentado. `launchSettings.json` separado permite levantar solo el gateway para debug. |
| `BackendServiceSettings.BaseUrl` rompe en Telemedicine | B | Mantener `http://localhost:5122` directo (servicios internos NO pasan por el gateway público). Verificar `BackendReferenceDataService` usa esa URL. |

## Open Questions

- ¿`/api/v1/agents/*` debe ir por gateway o seguir directo al AI Service? (Hoy proxya desde `CoppAddresd.Api`.) Asumido: va por gateway (`/api/v1/{**catch-all}` → cluster `api`).
- ¿El path `/api/v1/professionals-catalog` consumido por la UI móvil debe entrar por gateway o seguir por backend? Asumido: va por gateway (cluster `api`).
- Versión exacta de `Yarp.ReverseProxy` compatible con .NET 10 GA: usar `2.3.0` (confirmar al lockear dependencias).
