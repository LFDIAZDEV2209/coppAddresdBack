# Requirements — api-gateway-yarp

> Cambio: `api-gateway-yarp`. Spec: 3 capacidades nuevas (`api-gateway`, `gateway-internal-protection`, `gateway-aws-migration`). Conteo: **12 REQ-***.
> Contrato: RFC 2119 (MUST/SHOULD/MAY) + Given/When/Then. Idioma: español (docs), identificadores en inglés (código).

## REQ-GW-001 — Exposición en puerto único

El gateway MUST escuchar en **HTTP 5080** y ser el único punto de entrada público para los clientes (frontend, móvil, webhooks externos). MUST no exponer los puertos 5122/5123/5130 a Internet en producción.

#### Scenario: Arranque del gateway

- GIVEN el gateway es el único proceso publicado al exterior
- WHEN un cliente hace `GET http://api.example/health`
- THEN el gateway responde `200` con `Healthy`
- AND ningún puerto de backend (5122/5123/5130) es accesible directamente

## REQ-GW-002 — Ruteo por prefijo de path

El gateway MUST enrutar por prefijo a tres clusters (Auth `5123`, Api `5122`, Telemedicine `5130`) usando YARP `Routes` + `Clusters`. La tabla de ruteo MUST ser declarativa en `appsettings.json` (no hardcodeada en código). Las 4 reglas MUST ser:

| # | Path | Cluster | Destino |
|---|------|---------|---------|
| 1 | `/api/auth/{**catch-all}` (excepto `/api/auth/internal/*`) | `auth` | `http://localhost:5123` |
| 2 | `/api/v1/telemedicine/{**catch-all}` | `telemedicine` | `http://localhost:5130` |
| 3 | `/api/v1/{**catch-all}` (excepto `/api/v1/internal/*` y `telemedicine/*`) | `api` | `http://localhost:5122` |
| 4 | `/api/auth/internal/*` y `/api/v1/internal/*` | **drop** | `404` (REQ-GW-006) |

#### Scenario: Login OTP ruteado al cluster auth

- GIVEN el cliente envía `POST /api/auth/send-otp` al gateway
- WHEN la petición llega al gateway
- THEN se enruta al cluster `auth` (`5123`)
- AND la respuesta llega al cliente con el mismo status code

#### Scenario: Chat streaming ruteado al cluster api

- GIVEN el cliente envía `POST /api/v1/chat/stream` al gateway
- WHEN la petición llega al gateway
- THEN se enruta al cluster `api` (`5122`)
- AND el stream SSE se establece sin buffering (ver REQ-GW-007)

## REQ-GW-003 — Preservación de headers de autenticación

El gateway MUST preservar sin modificar los headers `Authorization` (Bearer JWT), `Cookie` (refresh) y `X-Refresh-Status`. MUST NOT inyectar, eliminar ni reescribir estos headers en tránsito.

#### Scenario: Bearer JWT preservado

- GIVEN un cliente con `Authorization: Bearer eyJ…`
- WHEN hace una petición autenticada al gateway
- THEN el backend destino recibe el mismo header `Authorization` intacto

## REQ-GW-004 — Preservación de cookies de refresh

El gateway MUST preservar la cookie `copp_refresh_token` (`HttpOnly`, `Path=/api/auth`, `SameSite=Lax`). MUST mantener un único dominio público para que la cookie siga siendo same-origin. MUST NO reescribir `Set-Cookie` del backend (el `Path` debe quedar `/api/auth`).

#### Scenario: Refresh con cookie

- GIVEN el cliente envía `POST /api/auth/refresh` con `Cookie: copp_refresh_token=…`
- WHEN el Auth Service rota el token y responde `Set-Cookie: copp_refresh_token=…; Path=/api/auth`
- THEN el gateway reenvía el `Set-Cookie` sin tocar el `Path`
- AND el navegador actualiza la cookie en el dominio del gateway

## REQ-GW-005 — CORS centralizado en el gateway

El gateway MUST configurar CORS para el frontend (`http://localhost:3000` por defecto) y la app móvil (origen nativo). `AllowCredentials` MUST estar habilitado para preservar cookies. Los backends SHOULD restringir CORS a un set mínimo de orígenes confiables (no `*` con credenciales).

#### Scenario: Preflight desde el frontend

- GIVEN el frontend en `http://localhost:3000`
- WHEN envía `OPTIONS /api/v1/chat` con `Origin`, `Access-Control-Request-Method: POST`
- THEN el gateway responde `204` con `Access-Control-Allow-Origin`, `Access-Control-Allow-Credentials: true`

## REQ-GW-006 — Bloqueo de endpoints internos

El gateway MUST devolver `404 Not Found` (no `401/403`) para cualquier request a `/api/auth/internal/*` o `/api/v1/internal/*` que NO incluya el header `X-Internal-Key`. La clave MUST leerse desde configuración (Secrets Manager en AWS) y SHOULD compararse en tiempo constante. Si el header está presente y la clave es válida, MUST reenviar al backend.

#### Scenario: Internal sin header → 404

- GIVEN una petición a `GET /api/auth/internal/authorize`
- WHEN llega al gateway SIN el header `X-Internal-Key`
- THEN el gateway responde `404 Not Found` con `Content-Type: application/problem+json`
- AND la petición NO se enruta al backend

#### Scenario: Internal con clave válida

- GIVEN Telemedicine envía `GET /api/v1/internal/telemedicine/professionals/123` con `X-Internal-Key: <válida>`
- WHEN llega al gateway
- THEN se reenvía al cluster `api` (`5122`) con el header intacto

## REQ-GW-007 — Streaming SSE sin cortes

El gateway MUST preservar el `Content-Type: text/event-stream` y NO bufferear respuestas del endpoint `POST /api/v1/chat/stream`. MUST configurar `ActivityTimeout` ≥ 60 s y `RequestBodyReadTimeout` consistente en YARP. Tests E2E MUST verificar al menos 60 s de stream continuo.

#### Scenario: Chat stream de 60 segundos

- GIVEN el cliente abre `POST /api/v1/chat/stream` con un `chatId`
- WHEN el AI Service emite eventos `data: {…}` durante 60 s
- THEN el gateway reenvía cada evento sin buffering ni corte
- AND la conexión se mantiene hasta que el backend cierra

## REQ-GW-008 — Webhook Twilio con firma HMAC intacta

El gateway MUST enrutar `POST /api/v1/telemedicine/webhooks/twilio` al cluster `telemedicine` SIN añadir headers que invaliden la firma HMAC (`X-Twilio-Signature`). MUST NO modificar el body de la request. Tests E2E MUST firmar el body con el secret real y verificar que Telemedicine acepta el webhook.

#### Scenario: Webhook Twilio aceptado

- GIVEN Twilio envía `POST /api/v1/telemedicine/webhooks/twilio` con `X-Twilio-Signature: <firma>` y body `application/x-www-form-urlencoded`
- WHEN la petición pasa por el gateway al cluster `telemedicine`
- THEN Telemedicine valida la firma con `RequestValidator` y responde `200`
- AND el gateway NO añade headers que cambien el body o el `Content-Type`

## REQ-GW-009 — Health check no autenticado

El gateway MUST exponer `GET /health` (no `200 OK` autenticado) en el puerto 5080, sin requerir `Authorization`. MUST incluir estado de los clusters (healthy/degraded/unhealthy) vía health checks pasivos de YARP.

#### Scenario: Health check del gateway

- GIVEN el gateway está corriendo
- WHEN un cliente hace `GET /health`
- THEN responde `200` con `{"status":"Healthy","clusters":{"auth":"Healthy","api":"Healthy","telemedicine":"Healthy"}}`

## REQ-GW-010 — Frontend usa una sola env var

El frontend (`coppaddresd-front`) MUST consumir exactamente una URL pública: `NEXT_PUBLIC_GATEWAY_URL` (default `http://localhost:5080`). MUST colapsar las 3 env vars previas (`NEXT_PUBLIC_AUTH_API_URL`, `NEXT_PUBLIC_API_URL`, `NEXT_PUBLIC_TELEMEDICINE_API_URL`) en una sola en `lib/config/env.ts`.

#### Scenario: Frontend con env var única

- GIVEN el frontend tiene `NEXT_PUBLIC_GATEWAY_URL=http://localhost:5080`
- WHEN el usuario hace login OTP
- THEN el cliente llama `POST http://localhost:5080/api/auth/send-otp`
- AND la respuesta llega con la cookie `Set-Cookie: copp_refresh_token`

## REQ-GW-011 — Móvil apunta al gateway

La app móvil (`antares-paciente`) MUST enrutar `/api/**` al gateway vía proxy Vite (`vite.config.ts`: `'/api' → http://localhost:5080`). En build de producción (Capacitor), `server.url` MUST apuntar al dominio del gateway. MUST NO mantener un proxy específico a `5123` para `/api/auth`.

#### Scenario: Móvil en dev

- GIVEN `vite.config.ts` define `proxy: { '/api': 'http://localhost:5080' }`
- WHEN la app móvil hace `POST /api/auth/login`
- THEN Vite proxy reenvía al gateway
- AND el gateway enruta al cluster `auth`

## REQ-GW-012 — Doc del plan de migración a AWS

`docs/architecture/gateway.md` MUST incluir: (1) mapeo YARP → AWS API Gateway REST (custom domain `api.coppaddresd.com` placeholder, cuenta AWS placeholder); (2) split: API Gateway REST para rutas no-streaming + ALB/CloudFront para `POST /api/v1/chat/stream`; (3) JWT authorizer reusando secreto/issuer del Auth Service; (4) usage plans + API keys; (5) cookie `copp_refresh_token` requiere un único dominio (`api.coppaddresd.com` en prod).

#### Scenario: ADR con split documentado

- GIVEN el equipo revisa `docs/architecture/gateway.md`
- WHEN busca el plan de AWS
- THEN encuentra el split API Gateway REST + ALB/CloudFront con la justificación técnica de por qué SSE no pasa por API Gateway
