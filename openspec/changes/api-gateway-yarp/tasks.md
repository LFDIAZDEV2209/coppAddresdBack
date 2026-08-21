# Tasks: API Gateway YARP

## Review Workload Forecast

| Campo | Valor |
|-------|-------|
| Líneas estimadas totales | 580–720 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (gateway scaffold + config + CORS + internals) → PR 2 (frontend + móvil + docker) → PR 3 (doc AWS + tests E2E + verificación) |
| Delivery strategy | exception-ok |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Gateway project scaffold, routes, CORS, internals, health check, solution + docker | PR 1 | base: main; incluye tests unitarios de middleware |
| 2 | Frontend collapse + móvil proxy + backend CORS origins | PR 2 | base: PR 1; 24 archivos frontend, 2 móvil, 3 backends |
| 3 | Doc AWS + tests E2E + verificación final | PR 3 | base: PR 2; docs + tests de integración/E2E |

## Phase 1: Scaffold del proyecto Gateway

- [ ] 1.1 Crear `src/Services/CoppAddresd.Gateway/CoppAddresd.Gateway.csproj` — `net10.0`, paquetes `Yarp.ReverseProxy 2.3.0` + `Microsoft.Extensions.Http 10.0.10`, sin `ProjectReference`
- [ ] 1.2 Crear `src/Services/CoppAddresd.Gateway/Program.cs` — mínimo: `WebApplication.CreateBuilder`, `AddReverseProxy().LoadFromConfig(...)`, `MapReverseProxy()`, `MapHealthChecks("/health")`
- [ ] 1.3 Crear `src/Services/CoppAddresd.Gateway/Properties/launchSettings.json` — `applicationUrl: http://localhost:5080`, perfiles `http`/`https`
- [ ] 1.4 Crear `src/Services/CoppAddresd.Gateway/appsettings.Example.json` — template con `ReverseProxy` (routes + clusters), `Cors:Origins`, `InternalKey`
- [ ] 1.5 Crear `src/Services/CoppAddresd.Gateway/appsettings.Development.json` — orígenes dev + puertos cluster
- [ ] 1.6 Modificar `CoppAddresd.slnx` — insertar `<Project Path="src/Services/CoppAddresd.Gateway/CoppAddresd.Gateway.csproj" />` en `<Folder Name="/src/Services/">`
- [ ] 1.7 Verificar: `dotnet build src/Services/CoppAddresd.Gateway` pasa sin errores

## Phase 2: Configuración de rutas y clusters

- [ ] 2.1 Completar `appsettings.Example.json` con las 4 reglas de ruta: `authRoute` (Priority 100 → cluster `auth`), `telemedRoute` (Priority 200 → cluster `telemedicine`), `apiRoute` (Priority 300 → cluster `api`), internos drop
- [ ] 2.2 Configurar `HttpClient.Timeout: 00:05:00` en cluster `api` para SSE
- [ ] 2.3 Configurar health checks activos (`Interval: 30s`, `Timeout: 5s`, `Path: /health`) y pasivos en `ReverseProxy:HealthCheck`
- [ ] 2.4 Smoke test manual: `GET http://localhost:5080/health` → 200 con JSON de clusters

## Phase 3: CORS centralizado en gateway

- [ ] 3.1 Crear `src/Services/CoppAddresd.Gateway/Configuration/CorsSettings.cs` — `record CorsSettings(string[] Origins)` bind a `Cors:Origins`
- [ ] 3.2 Añadir `AddCors` + `UseCors("GatewayCors")` en `Program.cs` — `AllowCredentials`, `WithExposedHeaders("X-Refresh-Status")`, orígenes desde config
- [ ] 3.3 Modificar `src/Services/CoppAddresd.Auth/appsettings.Example.json` — añadir `http://localhost:5080` a `Cors:Origins`
- [ ] 3.4 Modificar `src/Services/CoppAddresd.Auth/appsettings.Development.json` — añadir `http://localhost:5080` a `Cors:Origins`
- [ ] 3.5 Modificar `src/Services/CoppAddresd.Telemedicine/Program.cs` — añadir `http://localhost:5080` a whitelist CORS
- [ ] 3.6 Modificar `src/CoppAddresd.Api/Extensions/ApplicationServiceExtensions.cs` — añadir `http://localhost:5080` a whitelist CORS
- [ ] 3.7 Test: `OPTIONS /api/v1/chat` desde `localhost:3000` → 204 con `Access-Control-Allow-Credentials: true`

## Phase 4: Bloqueo de internals con X-Internal-Key

- [ ] 4.1 Crear `src/Services/CoppAddresd.Gateway/Configuration/InternalKeySettings.cs` — `record InternalKeySettings(string Key, bool Enabled)`
- [ ] 4.2 Crear `src/Services/CoppAddresd.Gateway/Middleware/InternalKeyMiddleware.cs` — intercepta `/api/auth/internal/*` y `/api/v1/internal/*`, sin header → 404 `application/problem+json`, comparación `FixedTimeEquals`
- [ ] 4.3 Registrar middleware en `Program.cs` ANTES de `MapReverseProxy()` — `app.UseMiddleware<InternalKeyMiddleware>()`
- [ ] 4.4 Test unitario: `InternalKeyMiddleware` — sin header → 404; key inválida → 404; key válida → `_next` invocado; path no interno → `_next` sin verificar key
- [ ] 4.5 Test E2E: `GET /api/auth/internal/authorize` sin `X-Internal-Key` → 404; con key válida → reenvía al cluster `auth`

## Phase 5: Health checks con sonda activa

- [ ] 5.1 Crear `src/Services/CoppAddresd.Gateway/HealthChecks/HealthResponseWriter.cs` — JSON `{"status":"Healthy","clusters":{...}}`, sonda `HttpClient.GetAsync` por cluster con timeout 2 s
- [ ] 5.2 Configurar `MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync })` en `Program.cs`
- [ ] 5.3 Test unitario: `HealthResponseWriter` — todos healthy → 200 `Healthy`; uno Unhealthy → 503 `Degraded`
- [ ] 5.4 Test integración: gateway arranca, `GET /health` → 200 con JSON correcto

## Phase 6: Frontend — collapse de env vars

- [ ] 6.1 Modificar `coppaddresd-front/lib/config/env.ts` — colapsar a `apiUrl: process.env.NEXT_PUBLIC_GATEWAY_URL ?? "http://localhost:5080"`, eliminar `authApiUrl` y `telemedicineApiUrl`
- [ ] 6.2 Modificar `coppaddresd-front/.env` + `.env.example` — una sola línea `NEXT_PUBLIC_GATEWAY_URL=http://localhost:5080`
- [ ] 6.3 Actualizar `coppaddresd-front/lib/api/http.ts` — usar `env.apiUrl` como base
- [ ] 6.4 Actualizar `coppaddresd-front/lib/api/auth-service.ts` — reemplazar `${env.authApiUrl}` por `${env.apiUrl}`
- [ ] 6.5 Actualizar `coppaddresd-front/lib/api/invitation-service.ts` — reemplazar `${env.authApiUrl}` por `${env.apiUrl}`
- [ ] 6.6 Actualizar servicios en `coppaddresd-front/features/*/services/*.ts` (~21 archivos) — reemplazar `env.authApiUrl`/`env.telemedicineApiUrl` por `env.apiUrl` con paths completos
- [ ] 6.7 Verificar: `cd coppaddresd-front && npm run build` pasa sin errores de tipo

## Phase 7: Móvil — proxy y Capacitor

- [ ] 7.1 Modificar `antares-paciente/vite.config.ts` — proxy genérico `/api → http://localhost:5080`, eliminar proxy separado a `:5123`
- [ ] 7.2 Modificar `antares-paciente/capacitor.config.ts` — `server: { url: "https://api.example.com", androidScheme: "https" }` (placeholder)
- [ ] 7.3 Verificar: `cd antares-paciente && npm run build` pasa

## Phase 8: docker-compose

- [ ] 8.1 Modificar `coppAddresdBack/docker-compose.yaml` — servicio `gateway` con `build: ./src/Services/CoppAddresd.Gateway`, `ports: ["5080:5080"]`, `depends_on: [postgres]`, healthcheck `curl -f http://localhost:5080/health`
- [ ] 8.2 Verificar: `docker-compose up gateway` arranca y `/health` responde 200

## Phase 9: Tests E2E completos

- [ ] 9.1 Test E2E login OTP: `POST /api/auth/send-otp` vía `:5080` → 200 con cookie `Path=/api/auth` (escenario S-01)
- [ ] 9.2 Test E2E refresh cookie: `POST /api/auth/refresh` vía `:5080` → cookie rotada, `Set-Cookie` Path intacto; cookie corrupta → 401 + `X-Refresh-Status: invalid` (escenario S-02)
- [ ] 9.3 Test E2E SSE 60 s: backend emite 60 eventos a 1 s, gateway reenvía sin corte, assert todos los eventos en orden (escenario S-03). Requiere `HttpClient.Timeout=00:05:00`
- [ ] 9.4 Test E2E webhook Twilio: firmar body con `Twilio.Security.RequestValidator`, POST vía gateway → Telemedicine responde 200 (escenario S-04). Verificar que gateway NO añade headers que rompan HMAC
- [ ] 9.5 Test E2E internal sin header: `GET /api/auth/internal/authorize` → 404; con `X-Internal-Key` válida → reenvía (escenario S-05)
- [ ] 9.6 Test E2E CORS preflight: `OPTIONS /api/v1/chat` desde `localhost:3000` → 204 + headers correctos (escenario S-09)
- [ ] 9.7 Ejecutar `dotnet test` completo — todos los tests pasan

## Phase 10: Documentación AWS y verificación final

- [ ] 10.1 Crear `docs/architecture/gateway.md` — mapeo YARP → AWS API Gateway REST, split API Gateway REST + ALB/CloudFront para SSE, JWT authorizer, usage plans, cookie same-domain, webhook Twilio directo ALB
- [ ] 10.2 Actualizar `docs/architecture/README.md` — añadir referencia al gateway
- [ ] 10.3 Verificar: `dotnet build` solución completa pasa
- [ ] 10.4 Verificar: `dotnet test` completa pasa
- [ ] 10.5 Revisar matriz de escenarios S-01 a S-10 — todos cubiertos
