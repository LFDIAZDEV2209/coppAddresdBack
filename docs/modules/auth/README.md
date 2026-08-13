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
1. POST /api/auth/login (email + password + rememberMe + application)
2. Validar credenciales con UserManager
3. Resolver la aplicación por código (auth.applications) — inactiva/desconocida → 401
4. Verificar UserApplication (usuario + aplicación) — sin acceso → 401
5. Si OK → generar AccessToken (JWT con aud = código de aplicación) + RefreshToken
   (ligado a la aplicación en DB)
6. Response: { accessToken, tokenType, expiresIn } — sin refresh en el body
7. Set-Cookie copp_refresh_token (HttpOnly): rememberMe=true → 7 días,
   rememberMe=false → 8 horas (cookie de sesión)
```

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
  }
}
```

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

- 100 requests/minuto por IP
- Configurado en `Program.cs` con `AddRateLimiter()`

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
