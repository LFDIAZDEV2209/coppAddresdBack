# Módulo de Autenticación y Autorización

Sistema completo de autenticación JWT con autorización basada en permisos granulares, implementado como servicio web standalone.

## Arquitectura

```
┌─────────────────────────────────────────────────────────────┐
│                    Auth Service (Puerto 5058)                 │
├─────────────────────────────────────────────────────────────┤
│  Controllers                                                │
│  ├── AuthController      → Login, Refresh, Logout, Password │
│  ├── MeController        → Info usuario actual              │
│  ├── UsersController     → CRUD usuarios                    │
│  ├── RolesController     → CRUD roles + asignación          │
│  └── PermissionsController → CRUD permisos + asignación     │
├─────────────────────────────────────────────────────────────┤
│  Services                                                   │
│  ├── AuthService         → Lógica de autenticación          │
│  ├── OtpService          → OTP por identificación (PHONE→Twilio, EMAIL local)│
│  ├── TwilioOtpService    → Único que conoce el SDK de Twilio Verify V2│
│  ├── OtpProtectionService→ Motor de protección OTP en memoria (IP/teléfono/documento, cooldown, lockout)│
│  ├── UserService         → Lógica de usuarios               │
│  ├── RoleService         → Lógica de roles                  │
│  ├── PermissionService   → Lógica de permisos               │
│  └── TokenService        → Generación JWT + Refresh         │
├─────────────────────────────────────────────────────────────┤
│  Authorization                                              │
│  ├── PermissionPolicyProvider  → Dynamic policy provider    │
│  ├── PermissionHandler       → Verifica permisos en DB      │
│  └── RequirePermissionAttribute → [RequirePermission("X")]  │
├─────────────────────────────────────────────────────────────┤
│  Security                                                   │
│  ├── TokenInvalidationService → Invalida via SecurityStamp  │
│  └── SecurityStampValidator   → Valida stamp en cada request│
├─────────────────────────────────────────────────────────────┤
│  Data                                                       │
│  ├── AuthDbContext           → IdentityDbContext<Guid>      │
│  └── Schema: auth.           → 12 tablas                    │
└─────────────────────────────────────────────────────────────┘
```

## Decisiones de diseño

| Decisión | Razón |
|----------|-------|
| **Servicio standalone** | Auth no depende de otros proyectos. Puede desplegarse independientemente. |
| **Permisos granulares** | `Users.View`, `Users.Create`, etc. Más flexible que solo roles. |
| **Permisos directos + via rol** | Usuario puede tener permisos directos (excepciones) o via rol. |
| **Refresh tokens en DB** | Rotación automática, revocación en logout/cambio password. |
| **Refresh token en cookie HttpOnly** | `copp_refresh_token` (Path `/api/auth`, SameSite=Lax, Secure en prod). El JS del navegador nunca ve el token: inmune a XSS persistente. El access token viaja en header Bearer (memoria del cliente). |
| **SecurityStamp invalidation** | Cambios de password/rol/permiso invalidan tokens inmediatamente. |
| **Schema `auth.` separado** | Consistencia con `audit.`. Previene contaminación de `public.` |
| **CORS whitelist configurable** | `Cors:Origins` (default `http://localhost:3000`) con `AllowCredentials`; expone `X-Refresh-Status` para distinguir "sin cookie" de "token inválido". |

## Modelo de datos (Schema `auth.`)

```sql
auth.Users                    → Usuarios (Identity)
auth.Roles                    → Roles (Identity)
auth.UserRoles                → Asignación usuario-rol (Identity)
auth.UserRoleAssignments      → Asignación con navegación (custom)
auth.Permissions              → Permisos granulares (custom)
auth.RolePermissions          → Permisos de rol (custom)
auth.UserPermissions          → Permisos directos de usuario (custom)
auth.RefreshTokens            → Refresh tokens con rotación (custom)
auth.OtpCodes                 → Códigos OTP EMAIL del primer inicio de sesión (hash + salt). El canal PHONE usa Twilio Verify y NO persiste filas en esta tabla (se conserva para EMAIL hasta la limpieza de la infraestructura OTP antigua).
auth.Applications             → Aplicaciones del ecosistema (ERP, App móvil)
auth.UserApplications         → Aplicaciones a las que el usuario tiene acceso
auth.UserClaims               → Claims de usuario (Identity)
auth.RoleClaims               → Claims de rol (Identity)
auth.UserLogins               → Logins externos (Identity)
auth.UserTokens               → Tokens 2FA (Identity)
```

## Aplicaciones y acceso (Application / UserApplication)

- **Application** (`auth.applications`): cada aplicación del ecosistema tiene un
  código estable (`erp`, `app`) que es el audience (`aud`) del JWT y se
  referencia en `UserApplication`.
- **UserApplication** (`auth.user_applications`): determina explícitamente a
  qué aplicaciones puede acceder un usuario. Es la ÚNICA fuente de acceso por
  aplicación: **no se asume** `Admin → ERP` ni `User → APP` en ningún lugar.
  Roles y permisos son globales y no determinan acceso a aplicaciones.
- El seeder crea `erp` y `app` (idempotente), asigna el usuario admin al ERP y
  retro-asigna los refresh tokens legacy al ERP (única aplicación existente
  antes de este cambio).

## Flujo de autenticación

### Login
```
1. POST /api/auth/login (documentNumber | email + password + rememberMe + application)
2. Resolver el usuario: por número de identificación (viaja por
   app.patient_profiles.document_number → user_id) o por correo (staff/ERP)
3. Validar credenciales con UserManager
4. Resolver la aplicación por código (auth.applications) — inactiva/desconocida → 401
5. Verificar UserApplication (usuario + aplicación) — sin acceso → 401
6. Si OK → generar AccessToken (JWT con aud = código de aplicación) + RefreshToken
   (ligado a la aplicación en DB)
7. Response: { accessToken, tokenType, expiresIn } — sin refresh en el body
8. Set-Cookie copp_refresh_token (HttpOnly): rememberMe=true → 7 días,
   rememberMe=false → 8 horas (cookie de sesión)
```

### Primer inicio de sesión por número de identificación (app móvil)

Los pacientes se autentican por su número de identificación (no por correo).
El flujo verifica la propiedad de la identidad vía OTP y aprovisiona la cuenta:

```
1. POST /api/auth/id-lookup  { documentNumber, application }
   → Busca el paciente en app.patient_profiles por document_number (LOWER).
   → Devuelve datos básicos + los correos/teléfonos asociados ENMASCARADOS
     (label) para que el usuario elija el canal. 404 si no existe.
2. POST /api/auth/send-otp    { documentNumber, contactId }
   → Resuelve el destino REAL en el servidor (el cliente nunca lo envía).
   → Canal PHONE (SMS):
       ResolveContact → formato E.164 → OtpProtectionService.CheckCanSend
       → ITwilioOtpService.SendAsync (Twilio Verify V2) → RegisterSend
     Twilio genera el código, envía el SMS y almacena/valida el estado de
     verificación. PHONE NO genera OTP local, NO usa auth.otp_codes y NO
     devuelve devCode (ni en Development). RegisterSend solo se ejecuta si
     Twilio aceptó el envío (un fallo del proveedor NO consume cuota local).
   → Canal EMAIL: flujo local sin cambios — OTP de 6 dígitos guardado SOLO como
     hash (SHA-256 + salt) en auth.otp_codes con expiración de 5 min. Invalida
     códigos previos pendientes del mismo documento+canal. En Development
     devuelve devCode en la respuesta para pruebas end-to-end.
3. POST /api/auth/verify-otp  { documentNumber, otp, application, rememberMe }
   → Canal PHONE:
       ResolveContact → E.164 → OtpProtectionService.CheckCanVerify
       → ITwilioOtpService.CheckAsync (Twilio Verify V2)
       → IsApproved=false → RegisterVerifyFailed → 401 (sin excepciones)
       → IsApproved=true  → RegisterVerifySucceeded → continúa
     Errores del proveedor (429, red, etc.) → TwilioOtpException mapeada a HTTP
     por el middleware global. Bloqueos locales (límites/lockout) → 429 antes
     de llamar a Twilio.
   → Canal EMAIL: valida el último código pendiente: no expirado, ≤ 5 intentos,
     hash con comparación en tiempo constante.
   → Aprovisiona la cuenta: si el paciente no tiene usuario Identity lo crea
     (password aleatorio inutilizable — el login futuro es por OTP), lo vincula
     a app.patient_profiles.user_id y le otorga acceso a la aplicación.
   → Emite AccessToken (aud = aplicación) + RefreshToken en cookie HttpOnly.
   → 401 si el código es inválido/expirado/sin intentos; 429 si el intento fue
     bloqueado por el motor de protección (límite o lockout).
```

El discriminador de canal en `verify-otp` es la presencia de un código EMAIL
pendiente en `auth.otp_codes` (el canal PHONE nunca persiste filas): con fila
pendiente → flujo EMAIL local; sin fila → verificación contra Twilio.

**Canal PHONE — responsabilidades**:
- **Twilio genera el OTP** (código de 6 dígitos), **lo envía por SMS**, **lo
  almacena** y **lo verifica** (Verification/VerificationCheck de Verify V2).
- PHONE **no genera OTP local** (sin `GenerateOtp`, sin hash, sin salt), **no
  usa `auth.otp_codes`**, **no devuelve devCode** (Twilio es la autoridad
  también en Development) y **usa exclusivamente SMS** (channel `"sms"`).
- El teléfono debe estar en **formato E.164** (sin espacios).

**Normalización a E.164**: el teléfono se convierte concatenando el código de
país (`app.patient_profiles.phone_country_code`) con los dígitos del número
(`phone_number`) y el prefijo `+`. No se agrega `+1` por defecto ni se adivina
el país. Ejemplos documentales:

| País | Teléfono | Country code | E.164 |
|---|---|---|---|
| Colombia | 3053924819 | 57 | +573053924819 |
| Estados Unidos | 5765550100 | 1 | +15765550100 |

El Auth Service NO conoce el SDK de Twilio: `OtpService` solo usa
`ITwilioOtpService` y `TwilioOtpService` es la única clase que conoce el SDK.
`OtpProtectionService` (motor de protección) no depende de HttpContext ni del
SDK de Twilio: recibe los identificadores (IP, documento, teléfono) como
parámetros explícitos.

Los correos/teléfonos se devuelven enmascarados (`di•••••••@gmail.com`,
`+57 30•••19`) para elegir el canal sin exponer el dato antes de verificar la
propiedad del número.

### Request autorizado
```
1. Request con Header: Authorization: Bearer <token>
2. JwtBearer valida firma, issuer, audience (aud ∈ ValidAudiences), expiración
3. SecurityStampValidator verifica stamp contra DB
4. PermissionHandler verifica permisos (directos + via rol)
5. Controller ejecuta acción
```

### Refresh token
```
1. POST /api/auth/refresh (sin body — el token viene de la cookie HttpOnly)
2. Buscar token en DB
3. Validar: no expirado, no revocado, aplicación ligada activa
4. Generar nuevo AccessToken con el MISMO aud de la aplicación ligada
   + nuevo RefreshToken (rotación, conserva la aplicación)
5. Marcar token viejo como reemplazado (ReplacedByTokenId)
6. Set-Cookie con el NUEVO refresh token (rota la cookie)
7. Response: { accessToken, tokenType, expiresIn }

Si la cookie está corrupta/expirada/revocada → 401 + Set-Cookie expirada
(limpia la cookie automáticamente; el usuario no debe borrarla a mano).
Header X-Refresh-Status: "missing" (nunca hubo cookie) | "invalid" (token
inválido) — el frontend decide si muestra el banner de sesión expirada.
```

### Logout
```
1. POST /api/auth/logout — se resuelve SOLO con la cookie de refresh
   (no requiere [Authorize]: funciona aunque el access token haya expirado)
2. Revoca TODOS los refresh tokens del usuario
3. Set-Cookie expirada (limpia la cookie del navegador)
4. Idempotente: sin cookie responde 200 igual
```

### Invalidación de tokens
```
Cambio de password/rol/permiso →
  UserManager.UpdateSecurityStampAsync() →
  SecurityStamp en DB cambia →
  SecurityStampValidator detecta mismatch →
  Token rechazado →
  Usuario debe hacer login de nuevo
```

## Permisos

### Módulo Users
- `Users.View` — Listar/obtener usuarios
- `Users.Create` — Crear usuarios
- `Users.Update` — Actualizar usuarios
- `Users.Delete` — Eliminar usuarios

### Módulo Roles
- `Roles.View` — Listar/obtener roles
- `Roles.Create` — Crear roles
- `Roles.Update` — Actualizar roles
- `Roles.Delete` — Eliminar roles
- `Roles.Assign` — Asignar/quitar roles a usuarios

### Módulo Permissions
- `Permissions.View` — Listar/obtener permisos
- `Permissions.Assign` — Asignar/quitar permisos a roles/usuarios

### Módulo Agents
- `Agents.View` — Ver agentes IA
- `Agents.Create` — Crear agentes
- `Agents.Update` — Actualizar agentes
- `Agents.Delete` — Eliminar agentes

## Configuración

```json
{
  "Jwt": {
    "Secret": "change-this-to-a-secure-secret-key-at-least-32-chars-long",
    "Issuer": "CoppAddresd.Auth",
    "ValidAudiences": ["erp", "app"],
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "Auth": {
    "AdminEmail": "admin@coppaddresd.com",
    "AdminPassword": "Test@1234",
    "AdminFirstName": "Admin",
    "AdminLastName": "System"
  },
  "Cors": {
    "Origins": "http://localhost:3000"
  },
  "Twilio": {
    "IsEnabled": false,
    "AccountSid": "",
    "ApiKeySid": "",
    "ApiKeySecret": "",
    "VerifyServiceSid": ""
  },
  "OtpSecurity": {
    "Enabled": true,
    "SendPerIpPerMinute": 5,
    "SendPerIpPerHour": 30,
    "SendPerPhonePerMinute": 3,
    "SendPerPhonePerHour": 10,
    "SendPerPhonePerDay": 20,
    "SendPhoneCooldownSeconds": 60,
    "SendPerDocumentPerHour": 5,
    "VerifyPerIpPerMinute": 30,
    "VerifyPerPhoneWindowMinutes": 5,
    "VerifyPerPhoneLimit": 10,
    "VerifyPhoneMaxFailedAttempts": 5,
    "VerifyPhoneLockoutSeconds": 300
  }
}
```

**Twilio Verify V2** (canal SMS del login por identificación): autenticación por
API Key (ApiKeySid + ApiKeySecret), nunca el Auth Token maestro. Si
`IsEnabled=false` (default) el servicio arranca sin credenciales y el flujo
PHONE devuelve un error controlado ("verificación por SMS no disponible").
Cuando se habilita, las cuatro propiedades son obligatorias y se validan al
arrancar (Program.cs). Los errores de Twilio se clasifican en
`TwilioOtpException` (`TwilioOtpErrorKind`) y el
`GlobalExceptionHandlerMiddleware` los mapea a HTTP: `InvalidPhone`/`InvalidParameter`
→ 400, `RateLimited` → 429, `ProviderUnavailable`/`Disabled`/`InvalidConfiguration`
→ 503, resto → 502. Nunca se exponen credenciales, stack trace ni la respuesta
cruda del SDK.

**Importante**: JWT debe ser idéntico entre Auth Service y API para que los
tokens funcionen. El `aud` del token es el código de la aplicación (`erp`,
`app`); `Jwt:ValidAudiences` (misma clave en ambos servicios) define qué
audiencias acepta la validación. Si no se configura, se usan los códigos
conocidos (`erp`, `app`).
## Startup

Al iniciar, Auth Service automáticamente:

1. Aplica migraciones pendientes
2. Seedea 15 permisos (idempotente)
3. Crea rol "Admin" si no existe
4. Crea usuario admin con todos los permisos
5. Seedea aplicaciones `erp` y `app` (idempotente)
6. Asigna acceso al ERP al usuario admin
7. Retro-asigna refresh tokens legacy al ERP

## Rate limiting

- **Rate limiter global** (`AddRateLimiter`): 100 requests/minuto por IP
  (FixedWindow, partición por `RemoteIpAddress`, QueueLimit 10), aplicado a
  todo `AuthController` (`[EnableRateLimiting("auth")]`). 429 con
  `{"message":"Demasiadas peticiones. Intenta más tarde."}`.
- **OtpSecurity** es ADICIONAL al rate limiter global (no lo reemplaza): actúa
  dentro de `OtpService` (canal PHONE) antes de consumir Twilio y produce el
  mismo shape 429.

## Protección OTP (OtpSecurity)

Motor interno en memoria (`OtpProtectionService`, Singleton) aplicado al canal
PHONE del flujo OTP. `CheckCanSend`/`CheckCanVerify` **no consumen cuota**; la
cuota se registra solo después de la operación real (`RegisterSend` tras
aceptación de Twilio, `RegisterVerifyFailed`/`RegisterVerifySucceeded` tras el
Check). Los bloqueos locales ocurren **siempre antes** de llamar a Twilio.

### Límites SEND (`send-otp`)

| Dimensión | Límite |
|---|---|
| IP | 5/min y 30/hora |
| Teléfono (E.164) | 3/min, 10/hora y 20/día |
| Cooldown por teléfono | 60 segundos |
| Documento | 5/hora |

### Límites VERIFY (`verify-otp`)

| Dimensión | Límite |
|---|---|
| IP | 30/min |
| Teléfono | 10 intentos por ventana de 5 minutos |
| Intentos fallidos PHONE | máximo 5 |
| Lockout tras superar fallos | 300 segundos |

Los fallos se registran con `RegisterVerifyFailed`; el intento que alcanza el
máximo activa el lockout y el siguiente `CheckCanVerify` lo detecta
(429 + `Retry-After`). `RegisterVerifySucceeded` limpia fallos y lockout.

### Variables de entorno (convención estándar ASP.NET Core, sección con `__`)

Twilio:

```
TWILIO__IS_ENABLED
TWILIO__ACCOUNT_SID
TWILIO__API_KEY_SID
TWILIO__API_KEY_SECRET
TWILIO__VERIFY_SERVICE_SID
```

OtpSecurity:

```
OTPSECURITY__ENABLED
OTPSECURITY__SEND_PER_IP_PER_MINUTE
OTPSECURITY__SEND_PER_IP_PER_HOUR
OTPSECURITY__SEND_PER_PHONE_PER_MINUTE
OTPSECURITY__SEND_PER_PHONE_PER_HOUR
OTPSECURITY__SEND_PER_PHONE_PER_DAY
OTPSECURITY__SEND_PHONE_COOLDOWN_SECONDS
OTPSECURITY__SEND_PER_DOCUMENT_PER_HOUR
OTPSECURITY__VERIFY_PER_IP_PER_MINUTE
OTPSECURITY__VERIFY_PER_PHONE_WINDOW_MINUTES
OTPSECURITY__VERIFY_PER_PHONE_LIMIT
OTPSECURITY__VERIFY_PHONE_MAX_FAILED_ATTEMPTS
OTPSECURITY__VERIFY_PHONE_LOCKOUT_SECONDS
```

No usar secretos hardcodeados: producción debe usar variables de entorno o
Secret Manager. No poner credenciales reales en Git.

### Errores HTTP

| Status | Caso |
|---|---|
| 400 | Teléfono inválido (no E.164), parámetros inválidos |
| 401 | OTP incorrecto o expirado |
| 429 | Rate limit local (IP/teléfono/documento), cooldown, lockout, o rate limit de Twilio (60203) |
| 502 | Error genérico del proveedor |
| 503 | Proveedor no disponible, configuración inválida, Twilio deshabilitado |

Nunca se exponen mensajes internos que revelen el límite exacto alcanzado, la
razón del bloqueo (IP/teléfono/documento/lockout) ni información sensible.

### Limitación multi-instancia

`OtpProtectionService` usa **almacenamiento en memoria** (ConcurrentDictionary
con limpieza oportunista de estados expirados):

- los contadores se **reinician al reiniciar** el Auth Service;
- los contadores **no se comparten entre múltiples réplicas**;
- funciona correctamente para una **única instancia**;
- si el servicio escala horizontalmente, migrar el estado de protección a
  Redis / `IDistributedCache` u otro almacenamiento distribuido (pendiente, no
  implementado en esta fase).

### Seguridad del flujo OTP

- La API Key de Twilio **no se almacena en código** (solo appsettings locales
  gitignored o variables de entorno).
- El SDK de Twilio está **encapsulado** en `TwilioOtpService` (único `using
  Twilio.*` del proyecto).
- Los logs nuevos del flujo OTP usan **datos enmascarados** (teléfono
  `+********4819`, documento `32****34`); el OTP nunca se registra; los tokens
  nunca se registran completos; las credenciales no se exponen en respuestas
  HTTP.
- `RegisterSend` solo ocurre después de que Twilio acepta el envío (un fallo
  del proveedor no consume cuota local).

### Pruebas reales realizadas (validación end-to-end)

- Envío SMS real exitoso (Twilio `Status=pending`).
- Cooldown de 60 s: segundo envío inmediato → **429** `{"message":...}` +
  `Retry-After`.
- Verificación real → Twilio `approved` → JWT + refresh token + cookie HttpOnly.
- Cinco códigos incorrectos → **401** cada uno; sexto intento → **429** por
  lockout (`VerifyPhoneLocked`) + `Retry-After`; **Twilio no fue llamado**
  durante el lockout (cero `CheckAsync` nuevos).

## Health checks

```
GET /health → 200 OK si DB está accesible
```

Configurado con `HealthChecks.NpgSql`.

## Seguridad

- **Password policy**: 8+ chars, mayúscula, minúscula, número, especial
- **Lockout**: 15 minutos después de 5 intentos fallidos
- **Refresh token rotation**: Cada refresh genera nuevo token, invalida el anterior
- **SecurityStamp**: Cambios críticos invalidan todos los tokens activos
- **Cookie HttpOnly**: `copp_refresh_token` — invisible para JS, `SameSite=Lax` (mitiga CSRF: las peticiones cross-site no envían la cookie; el bearer es independiente)
- **Rate limiting**: 100 requests/minuto por IP aplicado a `AuthController` (`[EnableRateLimiting("auth")]`)
- **Protección OTP**: límites por IP/teléfono/documento + cooldown (SEND) y ventana/intentos/lockout (VERIFY) en `OtpProtectionService` — ver sección *Protección OTP (OtpSecurity)*
- **Twilio Verify encapsulado**: el SDK solo vive en `TwilioOtpService`; `OtpService` usa `ITwilioOtpService`; `OtpProtectionService` no depende de HttpContext ni Twilio
- **Registro de bloqueos**: un OTP incorrecto se registra como fallo (no excepción); los límites/lockout responden 429 con el shape del rate limiter, sin revelar la razón interna
- **CORS**: Whitelist configurable (`Cors:Origins`) con `AllowCredentials`; expone `X-Refresh-Status`
- **HTTPS**: Requiere en producción (RequireHttpsMetadata = false solo en dev); cookie `Secure` solo fuera de desarrollo

## Testing

```bash
# Login — guarda la cookie en jar.txt
curl -c jar.txt -X POST http://localhost:5058/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@coppaddresd.com","password":"Test@1234","rememberMe":true,"application":"erp"}'

# Usar token
curl http://localhost:5058/api/me \
  -H "Authorization: Bearer <accessToken>"

# Refresh — usa la cookie del jar (sin body)
curl -b jar.txt -c jar.txt -X POST http://localhost:5058/api/auth/refresh \
  -H "Content-Type: application/json" -d '{}'

# Logout — revoca + limpia cookie
curl -b jar.txt -c jar.txt -X POST http://localhost:5058/api/auth/logout
```

## TODO / Mejoras futuras

- [ ] Integrar `HttpAuditActorContext` con Identity para capturar userId/email en auditoría
- [ ] Agregar 2FA (TwoFactorEnabled ya está en schema)
- [ ] Logins externos (Google, GitHub) — `UserLogins` ya existe
- [ ] Email confirmation (RequireConfirmedEmail = false actualmente)
- [ ] HTTPS obligatorio en producción
