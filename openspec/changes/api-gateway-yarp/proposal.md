# Proposal: api-gateway-yarp

## Intent

Frontend y móvil consumen **3 URLs de backend** (Auth `5123`, Api `5122`, Telemedicine `5130`) vía `lib/config/env.ts` y proxy Vite/Capacitor. Cambio: **API Gateway local con YARP** (`src/Services/CoppAddresd.Gateway`, puerto `5080`) + plan de migración a AWS API Gateway. Beneficio: colapsar 3 env vars en `NEXT_PUBLIC_GATEWAY_URL`, centralizar CORS y bloquear internals.

## Scope

**In**: proyecto .NET standalone `CoppAddresd.Gateway` (YARP, puerto 5080) + config `ReverseProxy` en `appsettings.json`. CORS centralizado. Filtro `X-Internal-Key` para internals (`404` sin header). Forwarding transparente webhook Twilio. `/health` no autenticado. Frontend colapsa 3 URLs. Móvil proxy `/api`→5080. `docker-compose.yaml` con 5 servicios. Docs: `gateway.md` + ADR + routing en `aws/production.md`.

**Out**: inter-servicios al gateway público; contratos/JWT/refresh/OTP de backends; S3 presigned por gateway; AWS CDK/Terraform; JWT validation en gateway.

## Capabilities

**New**: `api-gateway` (rutas, clusters, SSE, CORS, health check); `gateway-internal-protection` (filtro `X-Internal-Key`); `gateway-aws-migration` (mapping YARP→AWS, split SSE→ALB, webhook Twilio, cookie `copp_refresh_token`).

**Modified**: None.

## Approach

YARP standalone. `Routes` (`Path=/api/auth/{**catch-all}`, etc.) y `Clusters` (`http://localhost:5123/5122/5130`). Health checks pasivos. SSE con `ActivityTimeout` extendido en `/api/v1/chat/stream`. Internals filtrados por `AuthorizationPolicy` con `X-Internal-Key`; sin key → `404`. AWS API Gateway (documentado): REST con custom domain `api.coppaddresd.com` (placeholder), JWT authorizer reusando secreto/issuer del Auth Service, usage plans + API keys. SSE en ALB/CloudFront ante cluster ECS.

## Decisions (user-locked)

1. Frontend: 3 envs → `NEXT_PUBLIC_GATEWAY_URL ?? "http://localhost:5080"`.
2. CORS centralizado en gateway; móvil incluida.
3. AWS doc con placeholders (`api.coppaddresd.com`, cuenta).
4. SSE en AWS: split — API Gateway REST + ALB/CloudFront para `/api/v1/chat/stream`.

## Risks

- Cookie `copp_refresh_token` no llega si gateway en otro subdominio (M) → URL única `api.coppaddresd.com` en prod.
- SSE cortado por buffering/timeout YARP (M) → `ActivityTimeout` extendido + test 60 s.
- Webhook Twilio: gateway añade headers que invalidan firma HMAC (B) → `Transforms` neutros + test E2E con firma real.
- Internals quedan expuestos (A) → filtro `X-Internal-Key`; `404` sin header.
- Quinto proceso en dev (B) → `docker-compose` y `README.md` documentan.
- `BackendServiceSettings.BaseUrl` rompe (B) → mantener Telemedicine→Api directo.

## Rollback Plan

Detener gateway → revertir `env.ts` (3 URLs) + `vite.config.ts` → re-exponer CORS en backends → remover `CoppAddresd.Gateway` de `slnx` → verificar OTP, refresh, SSE y webhook Twilio. < 15 min.

## Dependencies

`Yarp.ReverseProxy` (NuGet, .NET 10). Acción futura: crear `coppAddresdBack/.engram/config.json` (memorias hoy bajo proxy `antares-paciente`). AWS: `api.coppaddresd.com` + cuenta antes del deploy.

## Success Criteria

Build/test verde; gateway `5080` con `/health`→`200`; login OTP devuelve cookie `Path=/api/auth`; internal sin `X-Internal-Key`→`404`; SSE `/api/v1/chat/stream` ≥ 60 s sin corte; webhook Twilio con firma HMAC válido aceptado; frontend y móvil completan flujos clave con la única env var; `gateway.md` con diagrama y plan AWS.

## Non-Goals

Inter-servicios al gateway público; JWT/rate limiting en gateway; deploy AWS (solo plan); mTLS/service mesh; S3 presigned vía gateway; IaC.
