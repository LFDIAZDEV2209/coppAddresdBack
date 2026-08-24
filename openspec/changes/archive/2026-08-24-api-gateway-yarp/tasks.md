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

- [x] 1.1 Crear `src/Services/CoppAddresd.Gateway/CoppAddresd.Gateway.csproj` — `net10.0`, paquetes `Yarp.ReverseProxy 2.3.0` + `Microsoft.Extensions.Http 10.0.10`, sin `ProjectReference`
- [x] 1.2 Crear `src/Services/CoppAddresd.Gateway/Program.cs` — mínimo: `WebApplication.CreateBuilder`, `AddReverseProxy().LoadFromConfig(...)`, `MapReverseProxy()`, `MapHealthChecks("/health")`
- [x] 1.3 Crear `src/Services/CoppAddresd.Gateway/Properties/launchSettings.json` — `applicationUrl: http://localhost:5080`, perfiles `http`/`https`
- [x] 1.4 Crear `src/Services/CoppAddresd.Gateway/appsettings.Example.json` — template con `ReverseProxy` (routes + clusters), `Cors:Origins`, `InternalKey`
- [x] 1.5 Crear `src/Services/CoppAddresd.Gateway/appsettings.Development.json` — orígenes dev + puertos cluster
- [x] 1.6 Modificar `CoppAddresd.slnx` — insertar `<Project Path="src/Services/CoppAddresd.Gateway/CoppAddresd.Gateway.csproj" />` en `<Folder Name="/src/Services/">`
- [x] 1.7 Verificar: `dotnet build src/Services/CoppAddresd.Gateway` pasa sin errores

## Phase 2: Configuración de rutas y clusters

- [x] 2.1 Completar `appsettings.Example.json` con las 3 reglas de ruta aplicadas: `authRoute` (Priority 100 → cluster `auth`), `telemedRoute` (Priority 300 → cluster `telemedicine`), `apiRoute` (Priority 200 → cluster `api`). Nota: el design original decía api=300/telemed=200; el apply corrigió las prioridades a telemed=300/api=200 para que `telemedRoute` venza al catch-all de `apiRoute` (YARP evalúa primero la Priority más alta).
- [x] 2.2 Configurar `HttpClient.Timeout: 00:05:00` en cluster `api` para SSE
- [x] 2.3 Configurar health checks activos (`Interval: 30s`, `Timeout: 5s`, `Path: /health`) y pasivos en `ReverseProxy:HealthCheck`
- [ ] 2.4 Smoke test manual: `GET http://localhost:5080/health` → 200 con JSON de clusters

## Phase 3: CORS centralizado en gateway

- [x] 3.1 Crear `src/Services/CoppAddresd.Gateway/Configuration/CorsSettings.cs` — `record CorsSettings(string[] Origins)` bind a `Cors:Origins`
- [x] 3.2 Añadir `AddCors` + `UseCors("GatewayCors")` en `Program.cs` — `AllowCredentials`, `WithExposedHeaders("X-Refresh-Status")`, orígenes desde config
- [x] 3.3 Modificar `src/Services/CoppAddresd.Auth/appsettings.Example.json` — añadir `http://localhost:5080` a `Cors:Origins`
- [x] 3.4 Modificar `src/Services/CoppAddresd.Auth/appsettings.Development.json` — añadir `http://localhost:5080` a `Cors:Origins`
- [x] 3.5 Modificar `src/Services/CoppAddresd.Telemedicine/Program.cs` — añadir `http://localhost:5080` a whitelist CORS
- [x] 3.6 Modificar `src/CoppAddresd.Api/Extensions/ApplicationServiceExtensions.cs` — añadir `http://localhost:5080` a whitelist CORS
- [ ] 3.7 Test: `OPTIONS /api/v1/chat` desde `localhost:3000` → 204 con `Access-Control-Allow-Credentials: true`

## Phase 4: Bloqueo de internals con X-Internal-Key

- [x] 4.1 Crear `src/Services/CoppAddresd.Gateway/Configuration/InternalKeySettings.cs` — `record InternalKeySettings(string Key, bool Enabled)`
- [x] 4.2 Crear `src/Services/CoppAddresd.Gateway/Middleware/InternalKeyMiddleware.cs` — intercepta `/api/auth/internal/*` y `/api/v1/internal/*`, sin header → 404 `application/problem+json`, comparación `FixedTimeEquals`
- [x] 4.3 Registrar middleware en `Program.cs` ANTES de `MapReverseProxy()` — `app.UseMiddleware<InternalKeyMiddleware>()`
- [x] 4.4 Test unitario: `InternalKeyMiddleware` — sin header → 404; key inválida → 404; key válida → `_next` invocado; path no interno → `_next` sin verificar key
- [ ] 4.5 Test E2E: `GET /api/auth/internal/authorize` sin `X-Internal-Key` → 404; con key válida → reenvía al cluster `auth`

## Phase 5: Health checks con sonda activa

- [x] 5.1 Crear `src/Services/CoppAddresd.Gateway/HealthChecks/HealthResponseWriter.cs` — JSON `{"status":"Healthy","clusters":{...}}`, sonda `HttpClient.GetAsync` por cluster con timeout 2 s
- [x] 5.2 Configurar `MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync })` en `Program.cs`
- [x] 5.3 Test unitario: `HealthResponseWriter` — todos healthy → 200 `Healthy`; uno Unhealthy → 503 `Degraded`
- [ ] 5.4 Test integración: gateway arranca, `GET /health` → 200 con JSON correcto

## Phase 6: Frontend — collapse de env vars

- [x] 6.1 Modificar `coppaddresd-front/lib/config/env.ts` — colapsar a `apiUrl: process.env.NEXT_PUBLIC_GATEWAY_URL ?? "http://localhost:5080"`, eliminar `authApiUrl` y `telemedicineApiUrl`
- [x] 6.2 Modificar `coppaddresd-front/.env` + `.env.example` — una sola línea `NEXT_PUBLIC_GATEWAY_URL=http://localhost:5080`
- [x] 6.3 Actualizar `coppaddresd-front/lib/api/http.ts` — usar `env.apiUrl` como base
- [x] 6.4 Actualizar `coppaddresd-front/lib/api/auth-service.ts` — reemplazar `${env.authApiUrl}` por `${env.apiUrl}`
- [x] 6.5 Actualizar `coppaddresd-front/lib/api/invitation-service.ts` — reemplazar `${env.authApiUrl}` por `${env.apiUrl}`
- [x] 6.6 Actualizar servicios en `coppaddresd-front/features/*/services/*.ts` (~21 archivos) — reemplazar `env.authApiUrl`/`env.telemedicineApiUrl` por `env.apiUrl` con paths completos
- [x] 6.7 Verificar: `cd coppaddresd-front && npm run build` pasa sin errores de tipo

## Phase 7: Móvil — proxy y Capacitor

- [x] 7.1 Modificar `antares-paciente/vite.config.ts` — proxy genérico `/api → http://localhost:5080`, eliminar proxy separado a `:5123`
- [x] 7.2 Modificar `antares-paciente/capacitor.config.ts` — `server: { url: "https://api.coppaddresd.com", androidScheme: "https" }` (placeholder, comentado)
- [x] 7.3 Verificar: `cd antares-paciente && npm run build` pasa

## Phase 8: docker-compose

- [x] 8.1 Modificar `coppAddresdBack/docker-compose.yaml` — servicio `gateway` con `build: ./src/Services/CoppAddresd.Gateway`, `ports: ["5080:5080"]`, `depends_on: [postgres]`, healthcheck `curl -f http://localhost:5080/health`
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

- [x] 10.1 Crear `docs/architecture/gateway.md` — mapeo YARP → AWS API Gateway REST, split API Gateway REST + ALB/CloudFront para SSE, JWT authorizer, usage plans, cookie same-domain, webhook Twilio directo ALB
- [x] 10.2 Actualizar `docs/architecture/README.md` — añadir referencia al gateway
- [ ] 10.3 Verificar: `dotnet build` solución completa pasa
- [ ] 10.4 Verificar: `dotnet test` completa pasa
- [ ] 10.5 Revisar matriz de escenarios S-01 a S-10 — todos cubiertos

## Pendientes de verificación manual

Tareas que están implementadas en disco pero cuya verificación requiere entorno
(gateway corriendo, Docker, servicios reales o PostgreSQL). No son defectos del
código; quedan pendientes de validación manual/E2E.

| Tarea | Descripción | Razón de pendiente |
|-------|-------------|--------------------|
| 2.4 | Smoke test manual `GET http://localhost:5080/health` → 200 | Requiere gateway corriendo. |
| 3.7 | Test CORS preflight `OPTIONS /api/v1/chat` desde `localhost:3000` → 204 | Requiere gateway + frontend corriendo. |
| 4.5 | Test E2E internal sin header → 404 / con key → reenvía | **Ya cubierto** por tests de integración `InternalEndpoint_WithoutKey_Returns404BeforeProxy` y `InternalEndpoint_WithValidKey_ForwardsToAuthCluster` (8/8 verdes); sin E2E manual adicional. |
| 5.4 | Test integración `/health` → 200 | Solo existe cobertura unitaria (`HealthResponseWriterTests`, 3 casos); falta E2E del endpoint. |
| 8.2 | `docker compose up gateway` arranca y `/health` responde 200 | Requiere Docker. |
| 9.1 | Test E2E login OTP vía `:5080` (S-01) | Requiere Auth real + flujo OTP completo. |
| 9.2 | Test E2E refresh cookie (S-02) | Requiere Auth real; rotación y cookie corrupta → 401 manual. |
| 9.3 | Test E2E SSE 60 s (S-03) | Solo cobertura no-buffering (~500 ms); duración 60 s sin test. |
| 9.4 | Test E2E webhook Twilio HMAC (S-04) | Requiere secret real de Twilio para `RequestValidator`. |
| 9.6 | Test E2E CORS preflight (S-09) | Requiere gateway + frontend corriendo. |
| 10.3 | `dotnet build` solución completa | Build verificado en verify-report (0 errores); pendiente re-ejecución formal. |
| 10.4 | `dotnet test` completa | Bloqueado por `Telemedicine.IntegrationTests` (0/25) que exige PostgreSQL / `COP_TEST_DB_CONNECTION`. |
| 10.5 | Revisar matriz de escenarios S-01 a S-10 | Cubiertos: S-03 (no-buffering), S-05 (automated); parciales S-07/S-08; sin cubrir S-04, S-09, S-10. |

> **Nota al pie — bugfix post-archivo (2026-08-24, cambio `api-gateway-yarp`)**: el gateway
> respondía **404 a `GET /api/me`** (y a `/api/users/*`, `/api/roles/*`, `/api/permissions/*`,
> `/api/invitations/*`), rompiendo la restauración de sesión del frontend. **Root cause**: la
> regla para enrutar los endpoints de Auth que viven FUERA de `/api/auth/*` estaba prevista
> en el design (§Interfaces: "Smoke test `GET /api/me` con Bearer → respuesta de Auth") pero
> se perdió en la config aplicada — la fase 2.1 solo dejó las 3 rutas originales. **Fix**:
> se agregaron 5 rutas explícitas al cluster `auth` (`authApisMeRoute` `/api/me`,
> `authApisUsersRoute`, `authApisRolesRoute`, `authApisPermissionsRoute`,
> `authApisInvitationsRoute`, todas Priority 100) en `appsettings.json`/`.Example`/`.Development`
> (`appsettings.Docker.json` las hereda por merge de `appsettings.json`; solo sobreescribe
> clusters). Se descartó el alternation `{a|b|c}` del enunciado porque los route templates de
> ASP.NET Core (que YARP usa para matchear) NO lo soportan: se parsearía como nombre de
> parámetro = comodín de un segmento, equivalente a un catch-all de `/api/*` no deseado.
> Cobertura: 7 tests de integración nuevos en `GatewayRoutingTests` (me/users/roles POST/
> permissions/invitations → AUTH, + no-catch-all-unknown y no-shadow-v1). Regresiones
> `/api/v1/chat`, `/api/v1/telemedicine/*` y `/api/auth/login` verificadas.
>
> ---
>
> > **Nota al pie — correcciones del gatekeeper (no son tareas numeradas):** el re-run
> > correctivo de apply implementó en disco y verificó: `Dockerfile`
> > (multi-stage, expone :5080, instala curl), `appsettings.json` base completo
> > (con `ActivityTimeout` SSE 00:10:00 en cluster `api`) y `appsettings.Docker.json`
> > (clusters → nombres de servicio `auth/api/telemedicine:8080`), tests de
> > integración del gateway (8, contrato routing/SSE/internals), middleware
> > `InternalKeyMiddleware` endurecido (fail-closed + anclaje de prefijo exacto),
> > corrección de la tabla de prioridades (telemed=300 / api=200) en
> > `docs/architecture/gateway.md`, y la documentación de la regla de red local vs
> > nombres de servicio. Todo verificado en el verify-report (gateway unit 14/14,
> > integration 8/8).
