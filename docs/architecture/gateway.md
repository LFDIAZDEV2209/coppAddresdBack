# Gateway — API Gateway YARP (local) y plan de migración a AWS

> Cambio: `api-gateway-yarp`. Documenta el gateway como **único punto de entrada pública** (REQ-GW-001) en desarrollo (YARP, puerto `5080`) y el plan de despliegue en AWS (REQ-GW-012). Cuenta AWS y dominio: **placeholders** (`api.coppaddresd.com`).

## Objetivo

Los clientes (frontend web, app móvil, webhooks externos) dejan de apuntar a los puertos de cada backend (`5122` Api, `5123` Auth, `5130` Telemedicine) y pasan por un único gateway que enruta por prefijo de path. Esto:

- Colapsa la configuración de clientes a **una sola URL** (`NEXT_PUBLIC_GATEWAY_URL` / `server.url` de Capacitor).
- Centraliza CORS, health checks y bloqueo de endpoints internos.
- Hace la migración a AWS casi 1:1 (las rutas YARP ≈ resource paths de API Gateway REST).

## Arquitectura local (YARP)

`CoppAddresd.Gateway` es un proyecto .NET 10 standalone (sin `ProjectReference`, igual que `CoppAddresd.Auth`/`CoppAddresd.Telemedicine`), solo paquetes NuGet `Yarp.ReverseProxy` + `Microsoft.Extensions.Http`. Configuración declarativa en `appsettings.json` (`ReverseProxy:Routes`/`ReverseProxy:Clusters`), nunca hardcodeada en código.

```
Cliente (web :3000 / móvil Vite :5173 / webhook Twilio / ERP)
   │  Authorization: Bearer <jwt>   Cookie: copp_refresh_token   X-Twilio-Signature
   ▼
┌──────────────────────────────────────────────────────────────────┐
│  CoppAddresd.Gateway  http://localhost:5080                       │
│  [1] InternalKeyMiddleware  ── /api/{auth,v1}/internal/* sin       │
│                                X-Internal-Key ──▶ 404 problem+json │
│  [2] UseCors "GatewayCors"  ── preflight 204 + AllowCredentials    │
│  [3] MapReverseProxy ── route match (Priority) ──▶ cluster         │
│        (health checks activos + pasivos, sonda /health por cluster)│
│  [4] MapHealthChecks("/health") ── JSON estado clusters            │
└───────┬──────────────┬────────────────┬───────────────────────────┘
        │              │                │
        ▼              ▼                ▼
   Auth :5123      Api :5122       Telemedicine :5130
   /api/auth/*     /api/v1/*       /api/v1/telemedicine/*
   (Auth consolida todos sus endpoints
    bajo /api/auth/*: me, users, roles,
    permissions, invitations e internal)
```

### Tabla de rutas (local)

| # | Path | Prioridad | Cluster | Destino dev | Notas |
|---|------|-----------|---------|-------------|-------|
| 1 | `/api/auth/{**catch-all}` | 100 | `auth` | `http://localhost:5123/` | Login OTP, refresh, logout, me, users, roles, permissions, invitations e internal. **El Auth Service consolida todos sus endpoints bajo este prefijo**, por eso no hacen falta rutas extra fuera de él |
| 2 | `/api/v1/telemedicine/{**catch-all}` | 300 | `telemedicine` | `http://localhost:5130/` | Incluye webhooks Twilio. Priority más alto que apiRoute: su catch-all también casa `/api/v1/telemedicine/*`, así que debe evaluarse primero (en YARP mayor prioridad se evalúa antes) |
| 3 | `/api/v1/{**catch-all}` | 200 | `api` | `http://localhost:5122/` | Chat, agents, professionals-catalog, internal API |
| 4 | `/api/auth/internal/*` y `/api/v1/internal/*` | — | **drop** | — | `404` sin `X-Internal-Key` válida (REQ-GW-006) |

> El gateway enruta todo el tráfico del Auth Service con **una sola ruta** (`authRoute`: `/api/auth/{**catch-all}`). Antes existían `authApis*Route` separadas (`/api/me`, `/api/users`, `/api/roles`, `/api/permissions`, `/api/invitations`) porque esos endpoints vivían fuera de `/api/auth/*`; tras consolidarlos bajo ese prefijo ya no se necesitan y se eliminaron. Los paths `/api/me|users|roles|permissions|invitations` sin el prefijo `/api/auth/` ya **no** se enrutan (404), coherente con el contrato del Auth Service. El `appsettings.Docker.json` solo sobreescribe `Clusters` (nombres de servicio); las rutas se heredan de `appsettings.json` por el merge del config pipeline.

> Orden YARP por `Priority` (mayor primero); a igual prefijo gana la ruta más específica. YARP reenvía `Authorization`, `Cookie`, `X-Twilio-Signature` y `X-Forwarded-*` sin modificarlos (`HttpTransformer.Default`) — REQ-GW-003/004/008. **No agregar transforms** que reescriban body o `Content-Type` para el webhook de Twilio ni para `/api/v1/chat/stream`.

### Health checks

- Sonda expuesta: `GET /health` → `{"status":"Healthy","clusters":{"auth":...,"api":...,"telemedicine":...}}` (REQ-GW-009).
- Activos: `Interval 30s`, `Timeout 5s`, `Path /health`, política `ConsecutiveFailures`.
- Pasivos: `TransportFailureRatePerRequest`, `ReactivationPeriod 30s`.
- Clusters `api` con `HttpClient.Timeout=00:05:00` y `ActivityTimeout` ≥ 300 s para SSE (REQ-GW-007).

## Decisiones de arquitectura (ADR)

| Fecha | Decisión | Contexto |
|---|---|---|
| 2026-08-24 | **Gateway YARP standalone** en `src/Services/CoppAddresd.Gateway` | Alternativas: YARP dentro de `CoppAddresd.Api` (rompe autonomía), Nginx/Caddy (rompe el modelo mental AWS), API Gateway en dev (diverge de prod). Migración a AWS casi 1:1; gateway "tonto" que delega JWT/validación a cada backend (no duplica Secret/Issuer/Audience). |
| 2026-08-24 | **Split SSE**: API Gateway REST para REST + ALB/CloudFront para `POST /api/v1/chat/stream` | API Gateway REST bufferiza y corta streams largos. Ver §Split SSE. |
| 2026-08-24 | **Dominio único** `api.coppaddresd.com` (path-based routing, no subdominios) | La cookie `copp_refresh_token` tiene `Path=/api/auth` y debe ser same-origin (REQ-GW-004). Ver §Cookie same-domain. |
| 2026-08-24 | **Webhook Twilio directo al ALB** (sin API Gateway) | Preservar la firma HMAC `X-Twilio-Signature`: ningún intermediario que reordene/agregue headers o cambie la URL que firma el validador. |
| 2026-08-24 | **JWT authorizer reutiliza el secret del Auth Service** | Mismo `Jwt:Secret`/`Issuer`/`Audience` vía Secrets Manager; los tokens emitidos por Auth son válidos en API Gateway y en cada backend (validación idéntica). |

## Plan AWS

Cuenta AWS y custom domain son placeholders: **`api.coppaddresd.com`** (registrado en Route 53, certificado en ACM `us-east-1`). Servicios objetivo según skill `aws-production`: ECS Fargate para los backends, RDS PostgreSQL para datos, S3 para objetos, Secrets Manager para secretos, CloudWatch para logs/métricas.

### Mapeo local ↔ AWS

| Aspecto | Local (YARP) | AWS |
|---|---|---|
| Path routing | `ReverseProxy:Routes` en `appsettings` | API Gateway REST API: resources anidados (`/api`, `/api/auth`, `/api/v1`, `/api/v1/telemedicine`, `/api/v1/chat`) con integración proxy (`ANY`) a cada ALB de servicio. **El resource `/api/auth` cubre todo el Auth Service** (me, users, roles, permissions, invitations e internal) e integra al ALB del Auth Service |
| `POST /api/v1/chat/stream` (SSE) | Cluster `api` con `ActivityTimeout=300s` | **NO pasa por API Gateway**: ALB (listener rule por path) → ECS `CoppAddresd.Api`, opcionalmente CloudFront sin buffering. Ver §Split SSE |
| Custom domain | `http://localhost:5080` | `api.coppaddresd.com` en Route 53 + certificado ACM, asociado al API Gateway (y al ALB/CloudFront para SSE) |
| JWT | Cada backend valida el Bearer | API Gateway JWT authorizer reutilizando `Jwt:Secret`/`Issuer`/`Audience` del Auth Service vía Secrets Manager (misma firma HS256) |
| Throttling | Rate limit en cada backend | API Gateway usage plans + API keys por cliente/integración |
| Cookie same-domain | `localhost:5080` (mismo origen que el cliente) | **Un único dominio** `api.coppaddresd.com` (path-based routing, NO subdominios) |
| Webhook Twilio | YARP pasa transparente al cluster `telemedicine` | `POST /api/v1/telemedicine/webhooks/twilio` **directo al ALB** de Telemedicine (no pasa por API Gateway) |
| Storage S3 | Backend genera presigned URLs | **Sin cambios**: el navegador habla directo con S3 (presigned URLs), el gateway no participa |
| Internals `X-Internal-Key` | `InternalKeyMiddleware` → 404 | API Gateway resource policy + WAF (regla que exige el header en `/api/{auth,v1}/internal/*`) o Lambda authorizer que responde 404; mismo valor de clave en Secrets Manager |
| Health checks | `GET /health` (sonda con estado de clusters) | Health check del target group del ALB contra `/health` de cada ECS task (no expuesto públicamente) |

### Diagrama AWS

```
                       api.coppaddresd.com (Route 53 + ACM)
                                   │
                  ┌────────────────┼─────────────────────────┐
                  ▼                ▼                          ▼
        API Gateway REST      ALB (path /api/v1/chat/stream)  ALB (webhook)
        (JWT authorizer,      └─▶ ECS CoppAddresd.Api         └─▶ ECS Telemedicine
        usage plans)            (streaming SSE sin buffer)        /webhooks/twilio
        │            │
        ▼            ▼
   ECS Auth      ECS Api
        └────────┬────────┘
                 ▼
        RDS PostgreSQL (Multi-AZ)      S3 (presigned URLs, browser directo)
```

### Split SSE — justificación

`POST /api/v1/chat/stream` emite Server-Sent Events durante ≥ 60 s (REQ-GW-007). API Gateway REST (v1) bufferiza respuestas y aplica timeouts/limites de payload que cortan streams largos, por lo que **SSE no pasa por API Gateway**:

- **ALB** reenvía la respuesta como stream sin bufferear y mantiene la conexión mientras el backend emita (adecuado para `text/event-stream`).
- **CloudFront** (opcional, delante del ALB) requiere deshabilitar el buffering para SSE (`CachePolicy` con `EnableAcceptEncodingGzip` y sin buffering de respuestas).
- El resto de rutas REST sigue por API Gateway con su JWT authorizer, throttling y usage plans.

**Alternativa a evaluar**: unificar todo en API Gateway REST con *Response Streaming* (`STREAM`, payload format 2.0). Si se adopta, validar: compatibilidad con `Content-Type: text/event-stream`, chunks no bufferizados, timeouts de integración y costos por duración de conexión. Decisión pendiente hasta prueba de concepto; mientras tanto, el split ALB/CloudFront es la opción documentada.

### Cookie same-domain

La cookie `copp_refresh_token` es `HttpOnly`, `SameSite=Lax`, `Secure` (fuera de Development) y `Path=/api/auth`. Para que el navegador la envíe en `POST /api/auth/refresh`:

- **Un único dominio público** `api.coppaddresd.com` con path-based routing (ruta `/api/auth/*` → Auth, `/api/v1/*` → Api, etc.).
- **NO usar subdominios** (`auth.coppaddresd.com`, `api-v1.coppaddresd.com`): sin atributo `Domain`, la cookie no viaja entre subdominios; con `Domain=.coppaddresd.com` se amplía el alcance (riesgo de seguridad). El ADR de dominio único lo prohíbe.
- El gateway no reescribe `Set-Cookie`: el `Path=/api/auth` del backend llega intacto al cliente (REQ-GW-004).

### JWT authorizer

- El Auth Service emite JWT HS256 con `sub`, roles, permisos y `aud` = código de aplicación (`erp`/`app`).
- En AWS, el **JWT authorizer** de API Gateway se configura con el mismo issuer y audiencia; el secreto vive en **Secrets Manager** (nunca en el repo — skill `security`). Los backends siguen validando el token igual que hoy (defensa en profundidad, sin duplicar el secreto en el gateway).
- Las rutas SSE por ALB no tienen authorizer de API Gateway: el backend `CoppAddresd.Api` conserva `[Authorize]` en `POST /api/v1/chat/stream` (validación JWT en la app).

### Internals con `X-Internal-Key`

- Local: `InternalKeyMiddleware` intercepta `/api/auth/internal/*` y `/api/v1/internal/*`; sin header o clave inválida → **404** `application/problem+json` (oculta la existencia del endpoint, REQ-GW-006); comparación en tiempo constante (`CryptographicOperations.FixedTimeEquals`); con clave válida reenvía al cluster.
- AWS: los paths internos no se exponen como resources públicos del API Gateway; si se definen, se protegen con resource policy + regla WAF que exige el header, y la clave vive en Secrets Manager. Para llamadas ERP → Auth se mantiene `X-Internal-Key` idéntico al local.

### Storage S3 fuera del gateway

Los uploads/downloads usan **presigned URLs** generadas por el backend (bucket `cooppadresd-storage-prod`, `us-east-2`). El navegador/móvil interactúa directo con S3; el gateway no proxya tráfico de objetos (no hay equivalente local que migrar).

### Usage plans y API keys

- Un usage plan por tipo de cliente (frontend web, app móvil) con throttling (p. ej. burst/rate por plan) y quota.
- API keys para integraciones externas (ERP, socios) en endpoints REST no sensibles; los clientes propios (web/móvil) se autentican con JWT + la cookie, sin API key.

## Operación

- **Dev**: `dotnet run --project src/Services/CoppAddresd.Gateway` (puerto `5080`) o `docker-compose up gateway`. Los clientes apuntan a `http://localhost:5080`.
- **Regla de red (dev local vs compose)**: fuera de Docker, los clusters del gateway apuntan a `localhost:5122/5123/5130` (`appsettings.json` base + `appsettings.Development.json`). Dentro de la red de compose apuntan a los **nombres de servicio** `auth`/`api`/`telemedicine` y al puerto interno `8080` de sus Dockerfiles — configurado en `appsettings.Docker.json`, que se carga con `ASPNETCORE_ENVIRONMENT=Docker` (definido en `docker-compose.yaml`). Nunca mezclar: localhost son puertos de dev fuera de Docker; los nombres de servicio solo existen dentro de la red de compose.
- **Despliegue AWS**: imagen Docker por servicio con tag inmutable, ECS Fargate detrás de ALB; despliegue del gateway en AWS = desplegar API Gateway + listener rules del ALB (no hay contenedor de YARP en prod).
- **Rollback local** (< 15 min): detener gateway → revertir `env.ts` (3 URLs) y `vite.config.ts` → re-exponer CORS en backends → remover `CoppAddresd.Gateway` del slnx.

## Riesgos y mitigaciones

| Riesgo | Prob. | Mitigación |
|---|---|---|
| SSE cortado por buffering/timeout en AWS | M | Split ALB/CloudFront (sin API Gateway); test E2E de 60 s obligatorio; validar alternativa Response Streaming antes de unificar |
| Webhook Twilio: headers extra invalidan HMAC | B | Ruta directa al ALB sin API Gateway; local: YARP no añade headers al body; test E2E con `RequestValidator` real |
| Cookie no viaja entre subdominios | M | Dominio único `api.coppaddresd.com`; ADR lo prohíbe |
| CORS multi-origen (web + Capacitor) | M | `Cors:Origins` del gateway con `AllowCredentials`; backends agregan `http://localhost:5080` a su whitelist |
| Quinto proceso en dev | B | `docker-compose.yaml` con servicio `gateway` + `launchSettings.json` propio |
| `BackendServiceSettings.BaseUrl` en Telemedicine | B | Servicios internos NO pasan por el gateway; se mantiene la URL directa entre backends |

## Referencias

- Cambio OpenSpec: `openspec/changes/api-gateway-yarp/` (requirements.md, design.md, tasks.md)
- Skills: `architecture`, `aws-production`, `security`, `documentation`
- `docs/architecture/README.md` — mapa y ADR generales del backend
