# Scenarios — api-gateway-yarp

> Scenarios operativos E2E (Given/When/Then) derivados de los 12 REQ-* de `requirements.md`. Cobertura: happy paths, edge cases y error states para cada flujo crítico.

## S-01 — Login OTP vía gateway

**Cubre**: REQ-GW-001, REQ-GW-002, REQ-GW-003, REQ-GW-005, REQ-GW-010

- GIVEN el frontend en `http://localhost:3000` con `NEXT_PUBLIC_GATEWAY_URL=http://localhost:5080`
- AND el usuario tiene un documento registrado en el Auth Service
- WHEN envía `POST http://localhost:5080/api/auth/send-otp` con `{application:"app", contact:"+573053924819"}`
- THEN el gateway responde `200` con `{channel:"PHONE", expiresInSeconds:600}`
- AND la request viajó al cluster `auth` (`5123`) con `Origin: http://localhost:3000`
- AND el backend recibió el body intacto y Twilio envió el SMS

## S-02 — Refresh con cookie de rotación

**Cubre**: REQ-GW-003, REQ-GW-004

- GIVEN el cliente tiene la cookie `copp_refresh_token=<válida>` en el dominio del gateway
- WHEN envía `POST http://localhost:5080/api/auth/refresh` SIN body, con `Cookie: copp_refresh_token=<válida>`
- THEN el gateway reenvía la cookie al cluster `auth` sin modificarla
- AND el Auth Service rota el token y responde `200` con `Set-Cookie: copp_refresh_token=<nueva>; Path=/api/auth; HttpOnly; SameSite=Lax`
- AND el gateway reenvía el `Set-Cookie` al cliente SIN tocar el `Path`
- AND el navegador reemplaza la cookie vieja por la nueva en el dominio del gateway

**Edge case**: cookie corrupta (firma inválida)

- GIVEN el cliente envía `Cookie: copp_refresh_token=corrupto`
- WHEN la request llega al gateway
- THEN el Auth Service responde `401` con `X-Refresh-Status: invalid`
- AND el gateway reenvía ese header al cliente
- AND el frontend limpia la cookie local

## S-03 — Chat stream SSE de 60 segundos

**Cubre**: REQ-GW-002, REQ-GW-007, REQ-GW-003

- GIVEN el cliente autenticado abre `POST http://localhost:5080/api/v1/chat/stream` con `Authorization: Bearer <jwt>`
- WHEN el AI Service empieza a emitir eventos `data: {"text":"…"}` durante 60 s
- THEN el gateway reenvía cada evento sin buffering
- AND el `Content-Type: text/event-stream` se preserva
- AND el cliente recibe todos los eventos en orden
- AND la conexión se cierra solo cuando el backend envía el evento final

**Edge case**: cliente cancela el stream a los 10 s

- GIVEN el stream lleva 10 s abierto
- WHEN el cliente aborta la conexión
- THEN el gateway cancela la request al backend (`CancellationToken`)
- AND el AI Service deja de generar

## S-04 — Webhook Twilio con firma HMAC

**Cubre**: REQ-GW-002, REQ-GW-008

- GIVEN Twilio envía `POST http://api.example/api/v1/telemedicine/webhooks/twilio`
- AND el body es `application/x-www-form-urlencoded` con `CallSid=CAxxxx&Status=completed`
- AND `X-Twilio-Signature` está firmada con el `AuthToken` de Twilio sobre `url + body`
- WHEN la request llega al gateway
- THEN el gateway la enruta al cluster `telemedicine` (`5130`) SIN añadir headers extras
- AND Telemedicine valida la firma con `RequestValidator` y responde `200`
- AND el evento queda persistido en `tele.telemedicine_webhook_events` (idempotente)

**Edge case**: gateway añade header que rompe la firma

- GIVEN el gateway añade por error `X-Forwarded-For: <ip>` sin firmar
- WHEN Telemedicine valida la firma
- THEN Telemedicine responde `403 Forbidden` (firma inválida)
- AND el evento NO se procesa (alerta en logs)

## S-05 — Request a internal sin header → 404

**Cubre**: REQ-GW-006

- GIVEN un cliente externo (frontend o curl) sin header `X-Internal-Key`
- WHEN envía `GET http://localhost:5080/api/auth/internal/authorize?userId=1&permissionCode=Users.View`
- THEN el gateway responde `404 Not Found` con `Content-Type: application/problem+json`
- AND la request NO se enruta al cluster `auth`
- AND el log del gateway registra el intento con la IP de origen

**Variante**: `/api/v1/internal/telemedicine/*`

- GIVEN un cliente externo sin `X-Internal-Key`
- WHEN envía `GET http://localhost:5080/api/v1/internal/telemedicine/professionals/123`
- THEN el gateway responde `404 Not Found` (mismo comportamiento)

## S-06 — Frontend con env var única colapsada

**Cubre**: REQ-GW-010

- GIVEN `lib/config/env.ts` define `apiUrl: process.env.NEXT_PUBLIC_GATEWAY_URL ?? "http://localhost:5080"`
- AND todos los servicios (`auth-service.ts`, `chat-service.ts`, `telemedicine-service.ts`, etc.) usan `env.apiUrl` como base
- WHEN el usuario navega por el ERP
- THEN cada request sale con prefijo `http://localhost:5080/api/...`
- AND el gateway la enruta al cluster correcto según el path

**Edge case**: env var no definida (cae al default)

- GIVEN `NEXT_PUBLIC_GATEWAY_URL` no está seteada
- WHEN el frontend arranca
- THEN usa `http://localhost:5080` (default)
- AND el log de build NO muestra un warning crítico

## S-07 — Móvil viajando por el gateway

**Cubre**: REQ-GW-011

- GIVEN `antares-paciente/vite.config.ts` define `proxy: { '/api': { target: 'http://localhost:5080', changeOrigin: true } }`
- AND la app móvil está en modo dev
- WHEN el usuario hace login OTP (`POST /api/auth/send-otp`)
- THEN Vite resuelve el proxy al gateway (no a `5123` directo)
- AND el gateway enruta al cluster `auth`

**Edge case**: Capacitor en build de producción

- GIVEN `capacitor.config.ts` tiene `server.url: "https://api.example.com"`
- WHEN la app móvil arranca en el dispositivo
- THEN todas las requests `/api/...` van a `https://api.example.com` (dominio del gateway en AWS)
- AND NO hay proxy Vite (es build nativa)

## S-08 — Health check del gateway

**Cubre**: REQ-GW-009

- GIVEN el gateway está corriendo con los 3 clusters configurados
- WHEN un balanceador (ALB en prod) hace `GET http://localhost:5080/health`
- THEN el gateway responde `200` con `{"status":"Healthy","clusters":{"auth":"Healthy","api":"Healthy","telemedicine":"Healthy"}}`
- AND la respuesta NO requiere `Authorization`

**Edge case**: un cluster caído

- GIVEN el cluster `api` (`5122`) está caído
- WHEN el balanceador hace `GET /health`
- THEN el gateway responde `503` con `{"status":"Degraded","clusters":{"auth":"Healthy","api":"Unhealthy","telemedicine":"Healthy"}}`
- AND el ALB marca la tarea como `unhealthy` y la reemplaza

## S-09 — CORS preflight desde frontend

**Cubre**: REQ-GW-005

- GIVEN el frontend en `http://localhost:3000`
- WHEN envía `OPTIONS http://localhost:5080/api/v1/chat` con `Origin: http://localhost:3000`, `Access-Control-Request-Method: POST`, `Access-Control-Request-Headers: authorization,content-type`
- THEN el gateway responde `204` con `Access-Control-Allow-Origin: http://localhost:3000`, `Access-Control-Allow-Credentials: true`, `Access-Control-Allow-Methods: POST`, `Access-Control-Allow-Headers: authorization,content-type`
- AND la request preflight NO se reenvía al backend (YARP responde directo)

**Edge case**: origen no whitelisted

- GIVEN el origen `http://evil.example.com` NO está en `Cors:Origins`
- WHEN envía `OPTIONS /api/v1/chat` con `Origin: http://evil.example.com`
- THEN el gateway responde `403` (CORS rechaza)
- AND la request NO se enruta al backend

## S-10 — Móvil + cookie de refresh en Capacitor (producción)

**Cubre**: REQ-GW-004, REQ-GW-011

- GIVEN la app móvil en build de Capacitor con `server.url: "https://api.example.com"`
- WHEN el usuario hace login (`POST https://api.example.com/api/auth/login`)
- THEN la respuesta incluye `Set-Cookie: copp_refresh_token=…; Path=/api/auth; Secure; HttpOnly; SameSite=Lax`
- AND Capacitor + WebView persisten la cookie para `https://api.example.com`
- AND la siguiente request (`GET /api/me`) lleva la cookie automáticamente

**Edge case**: cookie expira durante el uso

- GIVEN la cookie tiene `Max-Age=28800` (8 h, sin `rememberMe`)
- WHEN pasan 8 h
- THEN el cliente intenta `POST /api/auth/refresh` → `401` con `X-Refresh-Status: invalid`
- AND el frontend redirige al login
