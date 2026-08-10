---
name: security
description: 'Seguridad en CoppAddresd: secretos, autenticación (servicio Auth), autorización, validación, OWASP, hardening de producción AWS.'
---

# Seguridad — Reglas del proyecto

Regla de oro: **nada sensible versionado**. `appsettings.json`/`appsettings.*.json` están gitignoreados — crear localmente, nunca commitear.

## Secretos

- Prohibido en código/repo: passwords, connection strings con credenciales, AWS keys, tokens, PATs, API keys, cookies.
- Local: User Secrets o variables de entorno (no commitear).
- AWS: Secrets Manager + roles IAM (skill `aws-production`).
- No loguear secretos (skill `logging`).

## Autenticación y autorización (proyectado)

- Identity + JWT viven en **`src/Services/CoppAddresd.Auth`** (servicio standalone, sin referencias a proyectos del repo).
- La API principal valida JWT Bearer emitido por Auth (clave pública/issuer configurada por configuración — secreto en Secrets Manager).
- Tokens: expiración corta (15-60 min) + refresh token rotatorio (si se implementa); `sub`/claims mapeados a usuario; JWT firmado (HS256 con secreto largo o RS256 con clave asimétrica para multi-servicio — preferir RS256).
- Endpoints sensibles: política de autorización por rol/permiso; validar claims en cada uso, no confiar en strings de entidades.
- HTTPS siempre; HSTS en producción; cookies HttpOnly+Secure+SameSite si se usan cookies.

## Validación de input (OWASP top)

- FluentValidation en Application (skill `service-layer`): longitud, formato, rangos, whitelist de enums.
- No confiar en IDs/parámetros del cliente sin autorización (IDOR: verificar que el recurso pertenece al tenant/usuario).
- SQL injection: EF Core parametriza — prohibido SQL interpolado con `FromSqlRaw` salvo params tipados.
- XSS: respuestas JSON (System.Text.Json escapa); no renderizar HTML server-side.
- Uploads (si aplica): validar tipo/tamaño/magic bytes, almacenar en S3 con presigned URLs, nunca en la app.
- Rate limiting en endpoints de auth/login y endpoints públicos (evitar brute force/DoS); considerar `dotnet rate limiter` nativo o ALB WAF.

## Configuración segura

- CORS: orígenes whitelist por entorno, no `*` con credenciales.
- Headers: `X-Content-Type-Options`, `X-Frame-Options`, CSP si aplica; evitar leak de versiones (ocultar `Server`/`X-Powered-By`).
- Logs: redactar PII y secretos (skill `logging`).

## Errores y fuga de información

- Errores → ProblemDetails genéricos; sin stack/SQL/versiones al cliente (skill `error-handling`).
- Mensajes de login: no revelar si el email existe ("credenciales inválidas" genérico).
- Enum/errores de negocio: no exponer detalles de infraestructura.

## AWS hardening

- Security groups mínimos; RDS en subred privada; IAM mínimo privilegio (skill `aws-production`).
- S3: buckets privados por defecto, presigned URLs, sin listados públicos.
- Secrets rotados; backups cifrados; TLS en ALB.

## Dependencias

- Mantener paquetes actualizados (revisar `dotnet list package --vulnerable` en CI).
- No agregar paquetes sin necesidad (superficie de ataque).

## Pruebas de seguridad

- Tests de autorización: 401 sin token, 403 sin permiso, IDOR bloqueado.
- Validación: payloads malformados → 400.
- Revisar que ningún test ni fixture contenga secretos reales.
